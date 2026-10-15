using System.Collections.Generic;
using System.Text;
using AbyssdawnBattle;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [에디터 전용] 플레이 중에 장착한 장비가 PlayerStatData 에셋(HeroData 등)에 저장되어
/// 다음 플레이에서 "기본으로 가지고 있는" 장비가 되는 문제를 막는다.
///
/// EquipmentManager 는 장착 정보를 씬 전환(던전 ↔ 전투) 사이에 유지하려고 PlayerStatData(ScriptableObject)에 직접 쓴다.
/// 에셋에 쓴 값은 플레이를 멈춰도 되돌아가지 않고 디스크에 저장되기까지 하므로,
/// 플레이 시작 직전 장비 칸 5개를 기록해 두었다가 플레이가 끝나면 그대로 되돌린다.
/// (빌드된 게임은 에셋을 저장하지 않으므로 해당 없음. 2026-05-07 HP/MP 분리와 같은 종류의 문제)
///
/// 도메인 리로드로 static 값이 지워지므로 기록은 SessionState(에디터 세션 동안 유지)에 둔다.
/// </summary>
[InitializeOnLoad]
public static class PlayerStatDataPlayModeGuard
{
    private const string Key = "Abyssdawn.PlayerStatDataEquipmentSnapshot";

    static PlayerStatDataPlayModeGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) { TakeSnapshot(); TakeIdentitySnapshot(); }
        else if (state == PlayModeStateChange.EnteredEditMode) { RestoreSnapshot(); RestoreIdentitySnapshot(); }
    }

    // ── [2026-10-14] 직업 · 종의 기억 3칸도 같은 방식으로 (새 게임 화면·불러오기가 에셋에 직접 쓰므로) ──
    // 한 줄 = 에셋 GUID | 직업 | 기억1 | 기억2 | 기억3
    private const string IdentityKey = "Abyssdawn.PlayerStatDataIdentitySnapshot";

    private static void TakeIdentitySnapshot()
    {
        var sb = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:PlayerStatData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<PlayerStatData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null) continue;
            sb.Append(guid).Append('|').Append(GuidOfAny(data.currentJob)).Append('|').Append(GuidOfAny(data.memorySlot1))
              .Append('|').Append(GuidOfAny(data.memorySlot2)).Append('|').Append(GuidOfAny(data.memorySlot3))
              .Append('|').Append(GuidOfAny(data.currentOath)).Append('|').Append(PathGuids(data)).Append('\n'); // [2026-10-14] 서약 · 패스
        }
        SessionState.SetString(IdentityKey, sb.ToString());
    }

    private static void RestoreIdentitySnapshot()
    {
        string snapshot = SessionState.GetString(IdentityKey, "");
        SessionState.EraseString(IdentityKey);
        if (string.IsNullOrEmpty(snapshot)) return;
        foreach (string line in snapshot.Split('\n'))
        {
            string[] p = line.Split('|');
            if (p.Length != 7) continue;
            var data = AssetDatabase.LoadAssetAtPath<PlayerStatData>(AssetDatabase.GUIDToAssetPath(p[0]));
            if (data == null) continue;
            var job = LoadAny<CharacterClass>(p[1]);
            var m1 = LoadAny<MemoryOfSpeciesData>(p[2]); var m2 = LoadAny<MemoryOfSpeciesData>(p[3]); var m3 = LoadAny<MemoryOfSpeciesData>(p[4]);
            var oath = LoadAny<AbyssdawnBattle.OathBindingData>(p[5]);
            if (data.currentJob == job && data.memorySlot1 == m1 && data.memorySlot2 == m2 && data.memorySlot3 == m3
                && data.currentOath == oath && PathGuids(data) == p[6]) continue;
            data.currentJob = job; data.memorySlot1 = m1; data.memorySlot2 = m2; data.memorySlot3 = m3;
            data.currentOath = oath;
            data.learnedPathSkills = new System.Collections.Generic.List<AbyssdawnBattle.PathSkillData>();
            foreach (var g in p[6].Split(',')) { var ps = LoadAny<AbyssdawnBattle.PathSkillData>(g); if (ps != null) data.learnedPathSkills.Add(ps); }
            data.CheckAndActivateTrait();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log($"[PlayerStatDataPlayModeGuard] '{data.name}' 직업·종의 기억을 플레이 전 상태로 되돌렸습니다.");
        }
    }

    private static string PathGuids(PlayerStatData data)
    {
        if (data.learnedPathSkills == null) return "";
        var ids = new System.Collections.Generic.List<string>();
        foreach (var ps in data.learnedPathSkills) if (ps != null) ids.Add(GuidOfAny(ps));
        return string.Join(",", ids);
    }

    private static string GuidOfAny(Object o)
    {
        if (o == null) return "";
        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long _) ? guid : "";
    }

    private static T LoadAny<T>(string guid) where T : Object
    {
        if (string.IsNullOrEmpty(guid)) return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
    }

    // 한 줄 = 에셋 GUID | 오른손 | 왼손 | 몸통 | 장신구1 | 장신구2 (장비 에셋 GUID, 없으면 빈칸)
    private static void TakeSnapshot()
    {
        var sb = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:PlayerStatData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<PlayerStatData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null) continue;
            sb.Append(guid);
            foreach (var e in Slots(data)) sb.Append('|').Append(GuidOf(e));
            sb.Append('\n');
        }
        SessionState.SetString(Key, sb.ToString());
    }

    private static void RestoreSnapshot()
    {
        string snapshot = SessionState.GetString(Key, "");
        SessionState.EraseString(Key);
        if (string.IsNullOrEmpty(snapshot)) return;

        foreach (string line in snapshot.Split('\n'))
        {
            string[] parts = line.Split('|');
            if (parts.Length != 6) continue;
            var data = AssetDatabase.LoadAssetAtPath<PlayerStatData>(AssetDatabase.GUIDToAssetPath(parts[0]));
            if (data == null) continue;

            EquipmentData[] before = new EquipmentData[5];
            for (int i = 0; i < 5; i++) before[i] = LoadEquipment(parts[i + 1]);

            EquipmentData[] now = Slots(data);
            var changes = new List<string>();
            string[] names = { "오른손", "왼손", "몸통", "장신구1", "장신구2" };
            for (int i = 0; i < 5; i++)
                if (now[i] != before[i]) changes.Add($"{names[i]} {NameOf(now[i])} → {NameOf(before[i])}");
            if (changes.Count == 0) continue;

            data.rightHand = before[0];
            data.leftHand = before[1];
            data.body = before[2];
            data.accessory1 = before[3];
            data.accessory2 = before[4];
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log($"[PlayerStatDataPlayModeGuard] '{data.name}' 장비를 플레이 전 상태로 되돌렸습니다: {string.Join(", ", changes)}");
        }
    }

    private static EquipmentData[] Slots(PlayerStatData d)
    {
        return new[] { d.rightHand, d.leftHand, d.body, d.accessory1, d.accessory2 };
    }

    private static string GuidOf(EquipmentData e)
    {
        if (e == null) return "";
        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(e, out string guid, out long _) ? guid : "";
    }

    private static EquipmentData LoadEquipment(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        return AssetDatabase.LoadAssetAtPath<EquipmentData>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private static string NameOf(EquipmentData e) => e != null ? e.name : "없음";
}
