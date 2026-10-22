using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Abyssdawn;   // MonsterSO

/// <summary>
/// 던전 화면 아래의 파티 상태 카드 (위저드리 다프네식). 전투 씬의 PartyBar 카드를 그대로 가져다 놓으면 자동 연결된다.
///  - 카드 찾기: partyBar 칸이 비어 있으면 이름 "PartyBar" 로 찾음 → 그 아래 PartySlot_1~4 (전투와 같은 이름 규칙).
///  - 순서: 전투와 동일 — Hero 는 CompanionPartyPersistence.heroSlotIndex 칸, 나머지 칸은 ActiveRoster 순서.
///  - 주인공: 씬의 PlayerStats 실시간 (HP/MP, 상태이상 아이콘). 함정·샘·레벨업 즉시 반영 (PlayerStats.OnStatusChanged).
///  - 동료: ActiveRoster 의 현재 HP/MP + MonsterSO 의 최대치·이름. 던전에서는 동료 상태이상 기록이 없어 아이콘은 비움.
///  - 쓰러진 캐릭터: 글자 붉게 + 해골 그림 (전투와 동일, PartyCardVisuals).
///  - 카드를 누르면 스테이터스 창이 그 캐릭터의 탭으로 열림 (같은 카드를 다시 누르면 닫힘).
/// 배치(위치·크기)는 씬에서 직접 — 이 스크립트는 값만 채운다. MapManager 가 실행 시 자동으로 붙인다.
/// </summary>
public class DungeonPartyBar : MonoBehaviour
{
    [Tooltip("파티 카드 묶음 (PartySlot_1~4 의 부모). 비워 두면 이름 'PartyBar' 로 찾는다")]
    public Transform partyBar;

    [Tooltip("동료 교체·순서 변경 등 이벤트가 없는 변화를 반영하는 주기 (초)")]
    public float refreshInterval = 0.25f;

    private static readonly Color AliveTextColor = Color.white;
    private static readonly Color DownedTextColor = new Color(1f, 0.5f, 0.5f, 1f); // 전투와 같은 기절 색

    private class Card
    {
        public Transform slot;
        public Transform skullParent;
        public TextMeshProUGUI nameText, hpText, mpText, levelText;
        public int number; // [2026-10-22] 자리 번호 1~4
        public List<Image> icons;
    }

    private readonly List<Card> _cards = new List<Card>();
    private PlayerStats _hero;
    private float _nextRefresh;
    private float _nextBindTry;
    private bool _warned;
    private int _lastSignature;

    /// <summary>[2026-10-15] 카드 연출(CardFx)이 주인공 카드를 찾을 때 쓴다.</summary>
    public static DungeonPartyBar Instance { get; private set; }

    // ─────────────────────────────────────────
    // [2026-10-18] 아이템 대상 고르기 — 화면을 살짝 어둡게 하고 카드만 위로 올려 빛나게. 카드를 누르면 그 칸 번호로 콜백.
    // ─────────────────────────────────────────
    private System.Action<int> _pickCallback;
    private System.Func<int, bool> _pickFilter;
    private GameObject _pickDim;
    private Canvas _pickRaise;
    private GraphicRaycaster _pickRaiseRay;
    private readonly List<GameObject> _pickGlows = new List<GameObject>();
    private bool _pickAllowEmpty;
    public bool IsPicking => _pickCallback != null;

    /// <param name="canPick">[2026-10-22] 고를 수 있는 칸 (없으면 사람이 있는 칸 전부). 부활 아이템은 쓰러진 동료 칸만.</param>
    /// <param name="allowEmpty">[2026-10-22] 빈 칸도 고를 수 있게 (대기 동료를 파티의 원하는 자리에 넣을 때)</param>
    public void BeginTargetPick(string caption, System.Action<int> onPick, System.Func<int, bool> canPick = null, bool allowEmpty = false)
    {
        if (!EnsureBound() || onPick == null) return;
        EndPick();
        _pickCallback = onPick;
        _pickFilter = canPick;
        _pickAllowEmpty = allowEmpty;

        // 어두운 막 (맨 위 화면 캔버스) — 누르면 취소
        _pickDim = new GameObject("ItemTargetDim", typeof(RectTransform));
        var dimCanvas = _pickDim.AddComponent<Canvas>();
        dimCanvas.renderMode = RenderMode.ScreenSpaceOverlay; dimCanvas.sortingOrder = 900;
        var scaler = _pickDim.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080f, 1920f); scaler.matchWidthOrHeight = 0f;
        _pickDim.AddComponent<GraphicRaycaster>();
        var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
        shade.transform.SetParent(_pickDim.transform, false);
        var srt = (RectTransform)shade.transform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
        var simg = shade.GetComponent<Image>(); simg.color = new Color(0f, 0f, 0f, 0.55f); simg.raycastTarget = true;
        var cancel = shade.AddComponent<Button>(); cancel.transition = Selectable.Transition.None;
        cancel.onClick.AddListener(EndPick);

        // 안내 글 (카드 바로 위)
        var capGo = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
        capGo.transform.SetParent(_pickDim.transform, false);
        var cap = capGo.GetComponent<TextMeshProUGUI>();
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f != null && f.name == "NotoSansKR SDF") { cap.font = f; break; }
        cap.text = caption; cap.fontSize = 44f; cap.fontStyle = FontStyles.Bold; cap.alignment = TextAlignmentOptions.Center;
        cap.color = new Color(0.96f, 0.87f, 0.58f, 1f); cap.raycastTarget = false;
        var crt = cap.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f); crt.pivot = new Vector2(0.5f, 0f);
        crt.sizeDelta = new Vector2(1000f, 70f); crt.anchoredPosition = new Vector2(0f, 540f); // 메뉴 줄 위

        // 카드 묶음을 막 위로 올린다
        _pickRaise = partyBar.gameObject.GetComponent<Canvas>();
        bool added = false;
        if (_pickRaise == null) { _pickRaise = partyBar.gameObject.AddComponent<Canvas>(); added = true; }
        _pickRaise.overrideSorting = true; _pickRaise.sortingOrder = 901;
        _pickRaiseRay = partyBar.gameObject.GetComponent<GraphicRaycaster>();
        if (_pickRaiseRay == null) _pickRaiseRay = partyBar.gameObject.AddComponent<GraphicRaycaster>(); else _pickRaiseRay = null;
        if (!added) _pickRaise = null; // 원래 있던 캔버스는 지우지 않는다 (정렬만 되돌림 — 아래 EndPick)

        // 고를 수 있는 카드에 금테
        var sp = Resources.Load<Sprite>("UI/GlowBorder");
        var occ = CompanionPartyPersistence.BuildMainLineOccupancy();
        for (int i = 0; i < _cards.Count && i < occ.Length; i++)
        {
            if (_cards[i] == null || (occ[i].IsEmpty && !_pickAllowEmpty) || (_pickFilter != null && !_pickFilter(i))) continue;
            var g = new GameObject("PickGlow", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(_cards[i].slot, false);
            var grt = (RectTransform)g.transform; grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = new Vector2(-6f, -6f); grt.offsetMax = new Vector2(6f, 6f);
            var gi = g.GetComponent<Image>(); gi.sprite = sp; gi.type = Image.Type.Sliced; gi.fillCenter = false; gi.raycastTarget = false;
            gi.color = new Color(1f, 0.85f, 0.45f, 1f);
            _pickGlows.Add(g);
        }
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.LockInput(this);
    }

    private void FinishPick(int cardIndex)
    {
        var occ = CompanionPartyPersistence.BuildMainLineOccupancy();
        if (cardIndex < 0 || cardIndex >= occ.Length || (occ[cardIndex].IsEmpty && !_pickAllowEmpty)) return; // 빈 칸은 무시 (허용할 때 빼고)
        if (_pickFilter != null && !_pickFilter(cardIndex)) return;                       // 고를 수 없는 칸도 무시
        var cb = _pickCallback;
        EndPick();
        cb(cardIndex);
        Refresh();
    }

    public void EndPick()
    {
        bool was = _pickCallback != null;
        _pickCallback = null;
        _pickFilter = null;
        if (_pickDim != null) Destroy(_pickDim);
        _pickDim = null;
        foreach (var g in _pickGlows) if (g != null) Destroy(g);
        _pickGlows.Clear();
        if (partyBar != null)
        {
            if (_pickRaiseRay != null) Destroy(_pickRaiseRay);
            if (_pickRaise != null) Destroy(_pickRaise);
            else { var c = partyBar.GetComponent<Canvas>(); if (c != null) c.overrideSorting = false; }
        }
        _pickRaise = null; _pickRaiseRay = null;
        if (was) { var player = FindFirstObjectByType<DungeonGridPlayer>(); if (player != null) player.UnlockInput(this); }
    }

    private void Update2Pulse()
    {
        if (_pickGlows.Count == 0) return;
        float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
        foreach (var g in _pickGlows) if (g != null) { var im = g.GetComponent<Image>(); var c = im.color; c.a = a; im.color = c; }
    }

    /// <summary>주인공 카드 (지금 주인공이 서 있는 칸).</summary>
    public RectTransform HeroCardRect()
    {
        if (!EnsureBound()) return null;
        var occupancy = CompanionPartyPersistence.BuildMainLineOccupancy();
        for (int i = 0; i < _cards.Count && i < occupancy.Length; i++)
            if (_cards[i] != null && occupancy[i].IsHero) return _cards[i].slot as RectTransform;
        return null;
    }

    /// <summary>[2026-10-15] 동료 카드 (ActiveRoster 순번 → 그 동료가 서 있는 칸).</summary>
    public RectTransform CompanionCardRect(int rosterIndex)
    {
        if (!EnsureBound()) return null;
        int slot = CompanionPartyPersistence.RosterIndexToMainLineSlot(rosterIndex);
        return slot >= 0 && slot < _cards.Count && _cards[slot] != null ? _cards[slot].slot as RectTransform : null;
    }

    private void OnEnable()
    {
        Instance = this;
        PlayerStats.OnStatusChanged += Refresh;
        DungeonFieldStatus.OnChanged += Refresh; // [2026-10-08] 함정 상태이상(출혈 등) 아이콘
        _nextRefresh = 0f;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
        PlayerStats.OnStatusChanged -= Refresh;
        DungeonFieldStatus.OnChanged -= Refresh;
    }

    // ── [2026-10-22] 한글 글자 아틀라스(NotoSansKR SDF, Dynamic)가 바뀌면 카드 글자를 다시 그린다 ──
    //   서약 창 등 다른 창이 새 한글을 쓰면 아틀라스에 글자·페이지가 추가되는데, 그때 카드의 한글 이름(폴백 서브메시)이
    //   사라지는 일이 있었다 (영문 "hero"·"Lv2"는 멀쩡). 재현이 일정하지 않아 원인 확정 전 — 바뀔 때마다 다시 그려 막는다.
    private TMP_FontAsset _krFont;
    private int _atlasSig = -1;

    private int AtlasSignature()
    {
        if (_krFont == null)
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f != null && f.name == "NotoSansKR SDF") { _krFont = f; break; }
        if (_krFont == null) return 0;
        int pages = _krFont.atlasTextures != null ? _krFont.atlasTextures.Length : 0;
        int glyphs = _krFont.glyphTable != null ? _krFont.glyphTable.Count : 0;
        return glyphs * 16 + pages;
    }

    private void RemeshCardTexts()
    {
        foreach (var c in _cards)
        {
            if (c == null || c.slot == null) continue;
            foreach (var t in c.slot.GetComponentsInChildren<TextMeshProUGUI>(false)) t.ForceMeshUpdate(true, true);
        }
    }

    private void LateUpdate()
    {
        int a = AtlasSignature();
        if (a == _atlasSig) return;
        bool first = _atlasSig < 0;
        _atlasSig = a;
        if (!first) RemeshCardTexts();
    }

    private void Update()
    {
        Update2Pulse(); // [2026-10-18] 대상 고르기 금테 깜빡임
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        // 바뀐 게 있을 때만 다시 채운다 (주인공 최대 HP 계산은 로그를 많이 남겨서 매번 부르지 않음)
        int sig = PartySignature();
        if (sig == _lastSignature && _cards.Count > 0) return;
        Refresh();
    }

    /// <summary>파티 구성·현재 HP/MP 요약값. 이벤트 없이 바뀌는 것(동료 교체·순서 변경 등) 감지용.</summary>
    private int PartySignature()
    {
        unchecked
        {
            int h = 17 + CompanionPartyPersistence.heroSlotIndex;
            if (_hero == null) _hero = FindFirstObjectByType<PlayerStats>();
            if (_hero != null) h = h * 31 + _hero.currentHP * 7919 + _hero.currentMP * 131 + _hero.level;
            foreach (var e in CompanionPartyPersistence.ActiveRoster)
                h = h * 31 + (e == null ? -1 : e.id * 7919 + e.currentHP * 131 + e.currentMP);
            return h;
        }
    }

    /// <summary>카드 전체를 현재 파티 상태로 다시 채운다.</summary>
    public void Refresh()
    {
        if (!EnsureBound()) return;
        if (_hero == null) _hero = FindFirstObjectByType<PlayerStats>();
        _lastSignature = PartySignature();

        var occupancy = CompanionPartyPersistence.BuildMainLineOccupancy();
        for (int i = 0; i < _cards.Count; i++)
        {
            Card card = _cards[i];
            if (card == null || card.slot == null) continue;

            if (i >= occupancy.Length || occupancy[i].IsEmpty)
            {
                FillEmpty(card);
            }
            else if (occupancy[i].IsHero)
            {
                if (_hero == null) FillEmpty(card);
                else FillCard(card, _hero.playerName, _hero.level, _hero.currentHP, _hero.maxHP, _hero.currentMP, _hero.maxMP, _hero);
            }
            else
            {
                var entry = occupancy[i].entry;
                MonsterSO so = CompanionPartyPersistence.LoadCompanion(entry.resourcePath);
                if (so == null) { FillEmpty(card); continue; }
                FillCard(card, so.MonsterName, so.MonsterLevel, entry.currentHP, so.HP, entry.currentMP, so.MP, null, entry.id);
            }
        }
    }

    private void FillCard(Card card, string name, int level, int hp, int maxHp, int mp, int maxMp, PlayerStats statusSource, int companionId = 0)
    {
        bool dead = hp <= 0;
        Color c = dead ? DownedTextColor : AliveTextColor;
        SetText(card.nameText, name, c);
        SetText(card.levelText, card.number.ToString(), c);                  // 왼쪽 위 = 자리 번호 (No.)
        PartyCardVisuals.SetLevelBadge(card.levelText, card.nameText, level); // [2026-10-22] Lv 는 No. 숫자 끝에 맞춘 별도 글자
        SetText(card.hpText, $"HP {hp}/{maxHp}", c);
        SetText(card.mpText, $"MP {mp}/{maxMp}", c);
        PartyCardVisuals.SetDeathSkull(card.skullParent, dead);
        PartyCardVisuals.ShowStatusIcons(card.icons, statusSource, companionId); // [2026-10-21] 동료 상태이상 아이콘
    }

    private void FillEmpty(Card card)
    {
        SetText(card.nameText, "", AliveTextColor);
        SetText(card.levelText, card.number.ToString(), AliveTextColor); // [2026-10-22] 빈 칸도 "No.2" 처럼 번호는 보인다
        PartyCardVisuals.SetLevelBadge(null, card.nameText, 0);
        SetText(card.hpText, "", AliveTextColor);
        SetText(card.mpText, "", AliveTextColor);
        PartyCardVisuals.SetDeathSkull(card.skullParent, false);
        PartyCardVisuals.ShowStatusIcons(card.icons, null);
    }

    private static void SetText(TextMeshProUGUI tmp, string text, Color color)
    {
        if (tmp == null) return;
        if (tmp.text != text) tmp.text = text;
        tmp.color = color;
    }

    /// <summary>PartyBar 와 카드 4장을 찾아 둔다. 아직 씬에 없으면 1초마다 다시 찾는다.</summary>
    private bool EnsureBound()
    {
        if (_cards.Count > 0 && partyBar != null) return true;
        if (Time.unscaledTime < _nextBindTry) return false;
        _nextBindTry = Time.unscaledTime + 1f;

        if (partyBar == null)
        {
            GameObject go = GameObject.Find(PartyCardVisuals.PartyBarName);
            if (go != null) partyBar = go.transform;
        }
        if (partyBar == null)
        {
            if (!_warned)
            {
                Debug.Log($"[DungeonPartyBar] 씬에 '{PartyCardVisuals.PartyBarName}' 이(가) 아직 없어 파티 카드를 표시하지 않습니다 (배치하면 자동 연결).");
                _warned = true;
            }
            return false;
        }

        _cards.Clear();
        for (int i = 0; i < CompanionPartyPersistence.MainLineSlots; i++)
        {
            Transform slot = PartyCardVisuals.FindSlot(partyBar, i);
            if (slot == null) { _cards.Add(null); continue; }

            var card = new Card
            {
                slot = slot,
                nameText = PartyCardVisuals.FindText(slot, "Nametext", "NameText"),
                hpText = PartyCardVisuals.FindText(slot, "HPText"),
                mpText = PartyCardVisuals.FindText(slot, "MPText"),
                levelText = PartyCardVisuals.FindText(slot, PartyCardVisuals.LevelTextNames),
                icons = PartyCardVisuals.FindStatusIcons(slot),
            };
            // 해골은 전투와 같은 자리 — 글자의 부모(카드 안쪽 배경) 맨 아래 층
            TextMeshProUGUI anchor = card.hpText != null ? card.hpText : (card.nameText != null ? card.nameText : card.mpText);
            card.skullParent = anchor != null ? anchor.transform.parent : slot;
            card.number = i + 1;
            PartyCardVisuals.SetNumberLabel(slot); // [2026-10-22] "Lv." → "No."
            _cards.Add(card);
        }

        int found = 0;
        foreach (var c in _cards) if (c != null) found++;
        if (found == 0)
        {
            if (!_warned)
            {
                Debug.LogWarning($"[DungeonPartyBar] '{partyBar.name}' 아래에서 PartySlot_1~4 를 찾지 못했습니다.");
                _warned = true;
            }
            _cards.Clear();
            return false;
        }
        // 터치는 카드 자체만 받는다 — 카드 묶음의 투명 배경·글자·아이콘은 이동 버튼 등의 터치를 가로채지 않게
        foreach (Graphic g in partyBar.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        for (int i = 0; i < _cards.Count; i++)
            if (_cards[i] != null) MakeCardClickable(_cards[i].slot, i);

        Debug.Log($"[DungeonPartyBar] '{partyBar.name}' 연결 — 카드 {found}/{CompanionPartyPersistence.MainLineSlots}장");
        return true;
    }

    // ─────────────────────────────────────────
    // 카드 터치 → 스테이터스 창
    // ─────────────────────────────────────────

    /// <summary>카드(PartySlot) 전체를 누를 수 있게 — 카드 자신의 Image 만 터치를 받고, 누르면 OnCardClicked(index).</summary>
    private void MakeCardClickable(Transform slot, int index)
    {
        Graphic target = slot.GetComponent<Graphic>();
        if (target == null)
        {
            var img = slot.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            target = img;
        }
        target.raycastTarget = true;

        Button button = slot.GetComponent<Button>();
        if (button == null) button = slot.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None; // 눌러도 카드 색은 그대로
        button.targetGraphic = target;
        button.onClick.RemoveAllListeners(); // 카드 버튼은 이 스크립트 전용 (다시 연결될 때 중복 방지)
        button.onClick.AddListener(() => OnCardClicked(index));
    }

    /// <summary>
    /// 카드를 누르면 스테이터스 창을 그 카드의 캐릭터 탭으로 연다.
    /// 스테이터스 창의 탭 순서는 고정(Slot1 = 주인공, Slot2~4 = 동료 ActiveRoster 0~2)이라,
    /// 주인공 자리를 옮겨도 "누른 카드의 캐릭터"가 나오도록 카드 번호를 탭 번호로 바꾼다.
    /// (기본 배치 — 주인공이 1번 — 에서는 1번 카드 = 1번 슬롯 그대로)
    /// 같은 카드를 다시 누르면 창을 닫는다 (STATUS 버튼과 같은 토글).
    /// </summary>
    public void OnCardClicked(int cardIndex)
    {
        if (_pickCallback != null) { FinishPick(cardIndex); return; } // [2026-10-18] 아이템 대상 고르는 중

        var occupancy = CompanionPartyPersistence.BuildMainLineOccupancy();
        int tab = (cardIndex < occupancy.Length && occupancy[cardIndex].IsHero)
            ? 0
            : CompanionPartyPersistence.MainLineSlotToRosterIndex(cardIndex) + 1;
        if (tab < 0) return;

        var tabs = FindFirstObjectByType<StatusTabController>(FindObjectsInactive.Include);
        if (tabs == null)
        {
            Debug.LogWarning("[DungeonPartyBar] 스테이터스 창(StatusTabController)을 찾지 못했습니다.");
            return;
        }
        GameObject panel = tabs.gameObject;

        if (panel.activeInHierarchy && tabs.CurrentIndex == tab)
        {
            panel.SetActive(false);
            return;
        }
        if (!panel.activeSelf) panel.SetActive(true); // 열리면 기본 탭(주인공)으로 초기화되므로 아래에서 다시 고름
        tabs.SelectSlot(tab);
    }
}
