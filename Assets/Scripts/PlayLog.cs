using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [2026-10-22] 플레이 로그 — Play 를 누른 순간부터 끌 때까지 플레이어가 한 모든 것을 시간순으로 모은다.
///  - 모으는 것: 씬 이동, 층 이동(층별 머문 시간·걸음 수), 걸음, 전투(시작·결과·전투 기록 전부), 마을 들름,
///    아이템 사용(포션 횟수 등), 상점 구입·판매, 저장, 게임 오버, 골드 변화
///  - Play 를 끄면(Application.quitting) 한 파일로 남긴다:
///      에디터: Assets/PlayLog/yyyy-MM-dd/PlayLog_HH-mm-ss.md
///      빌드  : persistentDataPath/PlayLog/yyyy-MM-dd/PlayLog_HH-mm-ss.md
///  - 각 시스템은 PlayLog.Xxx(...) 한 줄만 부른다 (기록이 실패해도 게임에는 영향 없음).
/// </summary>
public static class PlayLog
{
    private static DateTime _start;
    private static readonly StringBuilder _events = new StringBuilder();
    private static bool _written;

    // 집계
    private static int _steps, _battles, _wins, _flees, _defeats, _townVisits, _saves;
    private static int _goldStart, _goldSpent, _goldEarned;
    private static readonly Dictionary<string, int> _itemUses = new Dictionary<string, int>();
    private static readonly List<string> _purchases = new List<string>();
    private static readonly List<string> _sales = new List<string>();
    private static readonly List<string> _towns = new List<string>();
    private static readonly SortedDictionary<int, float> _floorSeconds = new SortedDictionary<int, float>();
    private static readonly SortedDictionary<int, int> _floorSteps = new SortedDictionary<int, int>();
    private static int _maxFloor;

    // 진행 중 상태
    private static int _floor = -1;
    private static float _floorMark;
    private static bool _inBattle;
    private static float _battleMark;
    private static float _battleSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        _start = DateTime.Now;
        _events.Clear(); _written = false;
        _steps = _battles = _wins = _flees = _defeats = _townVisits = _saves = 0;
        _goldSpent = _goldEarned = 0;
        _goldStart = PlayerWallet.Gold;
        _itemUses.Clear(); _purchases.Clear(); _sales.Clear(); _towns.Clear();
        _floorSeconds.Clear(); _floorSteps.Clear(); _maxFloor = 0;
        _floor = -1; _inBattle = false; _battleSeconds = 0f;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting -= Write;
        Application.quitting += Write;
        PlayerWallet.OnChanged -= OnGoldChanged;
        PlayerWallet.OnChanged += OnGoldChanged;
        _lastGold = PlayerWallet.Gold;

        var go = new GameObject("[PlayLog]");
        go.hideFlags = HideFlags.HideInHierarchy;
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();

        Event("시작", $"Play 시작 — 골드 {_goldStart}");
    }

    // ── 기록 (각 시스템에서 부름) ──

    public static void Event(string kind, string text)
    {
        _events.Append("- `").Append(Clock()).Append("` **").Append(kind).Append("** ").Append(Plain(text)).Append('\n');
    }

    public static void Step(Vector2Int pos)
    {
        _steps++;
        int f = CurrentFloor();
        _floorSteps[f] = (_floorSteps.TryGetValue(f, out var n) ? n : 0) + 1;
    }

    public static void TownVisit(int floor)
    {
        _townVisits++;
        _towns.Add($"{Clock()} B{floor}");
        Event("마을", $"B{floor} 마을에 들어감");
    }

    public static void ItemUsed(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return;
        _itemUses[itemName] = (_itemUses.TryGetValue(itemName, out var n) ? n : 0) + 1;
        Event("아이템", $"{itemName} 사용" + (_inBattle ? " (전투 중)" : ""));
    }

    public static void Purchase(string itemName, int price)
    {
        _purchases.Add($"{Clock()} {itemName} ({price} G)");
        Event("구입", $"{itemName} — {price} G");
    }

    public static void Sale(string itemName, int price)
    {
        _sales.Add($"{Clock()} {itemName} (+{price} G)");
        Event("판매", $"{itemName} — +{price} G");
    }

    public static void Saved() { _saves++; Event("저장", $"B{CurrentFloor()}"); }

    /// <summary>저장을 불러옴 — 불러온 골드를 이번 플레이의 시작 골드로 (번 돈으로 치지 않는다).</summary>
    public static void Loaded(int gold)
    {
        _lastGold = gold;
        _goldStart = gold;
        _goldEarned = _goldSpent = 0;
        Event("불러오기", $"B{CurrentFloor()} · 골드 {gold}");
    }

    public static void BattleStart(string title)
    {
        if (_inBattle) BattleEnd("(끝 기록 없음)");
        _battles++; _inBattle = true; _battleMark = Time.realtimeSinceStartup;
        Event("전투 시작", $"#{_battles} {title}");
    }

    /// <summary>result = "승리" / "도망" / "패배" 등.</summary>
    public static void BattleEnd(string result)
    {
        if (!_inBattle) return;
        _inBattle = false;
        float sec = Time.realtimeSinceStartup - _battleMark;
        _battleSeconds += sec;
        if (result == "승리") _wins++; else if (result == "도망") _flees++; else if (result == "패배") _defeats++;
        Event("전투 끝", $"#{_battles} {result} ({Dur(sec)})");
    }

    /// <summary>전투 메시지 한 줄 (BattleLogArchive.Add 가 부름) — 전투 중 모든 행동.</summary>
    public static void BattleLine(string line)
    {
        _events.Append("    - ").Append(Plain(line).Replace("\n", " / ")).Append('\n');
    }

    // ── 내부 ──

    private static int _lastGold;
    private static void OnGoldChanged(int gold)
    {
        int d = gold - _lastGold;
        if (d > 0) _goldEarned += d; else _goldSpent -= d;
        _lastGold = gold;
    }

    private static int CurrentFloor() => Mathf.Max(1, DungeonPersistentData.currentFloor);

    private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Event("씬", s.name);

    /// <summary>층이 바뀌면 이전 층의 머문 시간을 닫는다 (전투 시간도 그 층에 포함).</summary>
    private static void TrackFloor()
    {
        float now = Time.realtimeSinceStartup;
        string scene = SceneManager.GetActiveScene().name;
        bool inDungeon = scene.Contains("Dungeon") || scene.Contains("Battle"); // 타이틀·프롤로그 시간은 층에 넣지 않음
        int f = inDungeon ? CurrentFloor() : -1;
        if (_floor == f) return;
        if (f < 0)
        {
            if (_floor > 0) _floorSeconds[_floor] = (_floorSeconds.TryGetValue(_floor, out var t0) ? t0 : 0f) + (now - _floorMark);
            _floor = -1;
            return;
        }
        if (_floor > 0) _floorSeconds[_floor] = (_floorSeconds.TryGetValue(_floor, out var t) ? t : 0f) + (now - _floorMark);
        if (_floor > 0) Event("층 이동", $"B{_floor} → B{f}");
        _floor = f; _floorMark = now;
        _maxFloor = Mathf.Max(_maxFloor, f);
    }

    private class Runner : MonoBehaviour
    {
        private void Update() => TrackFloor();
    }

    private static void Write()
    {
        if (_written) return;
        _written = true;
        try
        {
            if (_inBattle) BattleEnd("(Play 종료로 중단)");
            // 마지막 층 시간 닫기
            if (_floor > 0) _floorSeconds[_floor] = (_floorSeconds.TryGetValue(_floor, out var t) ? t : 0f) + (Time.realtimeSinceStartup - _floorMark);
            var end = DateTime.Now;
            Event("끝", $"Play 종료 — 골드 {PlayerWallet.Gold}");

            var sb = new StringBuilder();
            sb.Append("# 플레이 로그 ").Append(_start.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n').Append('\n');
            sb.Append("## 요약\n\n");
            sb.Append("| 항목 | 값 |\n|---|---|\n");
            sb.Append("| 플레이 시간 | ").Append(Dur((float)(end - _start).TotalSeconds)).Append(" |\n");
            sb.Append("| 걸음 수 | ").Append(_steps).Append(" |\n");
            sb.Append("| 가장 깊은 층 | B").Append(_maxFloor).Append(" |\n");
            sb.Append("| 전투 | ").Append(_battles).Append("번 (승리 ").Append(_wins).Append(" · 도망 ").Append(_flees).Append(" · 패배 ").Append(_defeats).Append(") |\n");
            sb.Append("| 전투에 쓴 시간 | ").Append(Dur(_battleSeconds)).Append(" |\n");
            sb.Append("| 마을 들름 | ").Append(_townVisits).Append("번 |\n");
            sb.Append("| 저장 | ").Append(_saves).Append("번 |\n");
            sb.Append("| 골드 | 시작 ").Append(_goldStart).Append(" → 끝 ").Append(PlayerWallet.Gold).Append(" (번 돈 +").Append(_goldEarned).Append(" · 쓴 돈 -").Append(_goldSpent).Append(") |\n\n");

            sb.Append("## 층별\n\n| 층 | 머문 시간 | 걸음 |\n|---|---|---|\n");
            foreach (var kv in _floorSeconds)
                sb.Append("| B").Append(kv.Key).Append(" | ").Append(Dur(kv.Value)).Append(" | ").Append(_floorSteps.TryGetValue(kv.Key, out var st) ? st : 0).Append(" |\n");
            sb.Append('\n');

            sb.Append("## 아이템 사용\n\n");
            if (_itemUses.Count == 0) sb.Append("(없음)\n");
            foreach (var kv in _itemUses) sb.Append("- ").Append(kv.Key).Append(" × ").Append(kv.Value).Append('\n');
            sb.Append('\n');

            sb.Append("## 구입 내역\n\n");
            if (_purchases.Count == 0) sb.Append("(없음)\n");
            foreach (var p in _purchases) sb.Append("- ").Append(p).Append('\n');
            sb.Append("\n## 판매 내역\n\n");
            if (_sales.Count == 0) sb.Append("(없음)\n");
            foreach (var p in _sales) sb.Append("- ").Append(p).Append('\n');
            sb.Append("\n## 마을 들른 때\n\n");
            if (_towns.Count == 0) sb.Append("(없음)\n");
            foreach (var p in _towns) sb.Append("- ").Append(p).Append('\n');

            sb.Append("\n## 전체 기록 (시간순)\n\n").Append(_events);

            string root = Application.isEditor ? Path.Combine(Application.dataPath, "PlayLog") : Path.Combine(Application.persistentDataPath, "PlayLog");
            string dir = Path.Combine(root, _start.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"PlayLog_{_start:HH-mm-ss}.md");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[PlayLog] 저장: {path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlayLog] 저장 실패: " + e.Message);
        }
    }

    private static string Clock() => (DateTime.Now - _start).ToString(@"hh\:mm\:ss");

    private static string Dur(float sec)
    {
        var t = TimeSpan.FromSeconds(Mathf.Max(0f, sec));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}시간 {t.Minutes}분 {t.Seconds}초" : $"{t.Minutes}분 {t.Seconds}초";
    }

    private static readonly Regex Tag = new Regex("<[^>]+>");
    private static string Plain(string s) => string.IsNullOrEmpty(s) ? "" : Tag.Replace(s, "");
}
