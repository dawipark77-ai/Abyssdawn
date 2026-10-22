using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 파티 상태 카드(PartyBar) 공용 도우미 — 전투(BattleManager)와 던전(DungeonPartyBar)이 같은 카드 프리팹을 같은 규칙으로 쓴다.
///  - 카드 찾기: "PartyBar" 아래 PartySlot_1~4 (또는 PartySlot1~4 / PartySlot_0~3)
///  - 글자: NameText(Nametext) / HPText / MPText, 상태이상 아이콘: StatusIconRow 아래 StatusIcon_1~4
///  - 사망: 카드에 해골 그림 (Resources/UI/DeathSkull)
/// </summary>
public static class PartyCardVisuals
{
    public const string PartyBarName = "PartyBar";
    private const string DeathSkullResource = "UI/DeathSkull";
    private const string DeathSkullObjectName = "DeathSkull";
    private static Sprite _deathSkullSprite;

    /// <summary>카드의 레벨 숫자 글자 이름 ("Lv." 글자 옆의 숫자 칸).</summary>
    public static readonly string[] LevelTextNames = { "LvText", "LevelText" };

    /// <summary>카드에 보일 레벨 — 영입 동료는 몬스터 데이터의 레벨(동료는 레벨업 없음), 그 외는 캐릭터 레벨.</summary>
    public static int LevelOf(PlayerStats member)
    {
        if (member == null) return 0;
        return member.companionSource != null ? member.companionSource.MonsterLevel : member.level;
    }

    private const string LevelBadgeName = "LvBadge";
    private static readonly Color LevelBadgeColor = new Color(0.96f, 0.87f, 0.58f, 1f); // #F5DE94 옅은 황금색

    /// <summary>
    /// [2026-10-22] 이름 줄 왼쪽의 "Lv2" — 이름과 따로 둔 글자. 오른쪽 끝을 바로 위 "No.2" 의 숫자가 끝나는 곳에 맞춘다
    /// (예전엔 이름 앞에 붙어 가운데 정렬이라 이름 길이마다 위치가 달랐다). level &lt;= 0 이면 숨김.
    /// </summary>
    public static void SetLevelBadge(TextMeshProUGUI numberText, TextMeshProUGUI nameText, int level)
    {
        if (nameText == null) return;
        var parent = (RectTransform)nameText.transform.parent;
        var t = parent.Find(LevelBadgeName);
        TextMeshProUGUI badge;
        if (t == null)
        {
            var go = new GameObject(LevelBadgeName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            badge = go.GetComponent<TextMeshProUGUI>();
            badge.font = nameText.font;
            badge.alignment = TextAlignmentOptions.MidlineRight;
            badge.textWrappingMode = TextWrappingModes.NoWrap;
            badge.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>(); le.ignoreLayout = true;
        }
        else badge = t.GetComponent<TextMeshProUGUI>();

        bool show = level > 0 && numberText != null;
        badge.gameObject.SetActive(show);
        if (!show) { var m0 = nameText.margin; m0.x = 0f; nameText.margin = m0; return; }
        badge.text = "Lv" + level;
        badge.color = LevelBadgeColor;
        badge.fontSize = nameText.fontSize * 0.72f;

        // 위 "No." 글자가 시작하는 x — 글자 모양 기준 (칸 크기가 아니라). numberText(숫자)는 "No." 글자의 자식
        var label = numberText.transform.parent != null ? numberText.transform.parent.GetComponent<TextMeshProUGUI>() : null;
        if (label == null) label = numberText;
        label.ForceMeshUpdate();
        float labelLeft = label.textInfo.characterCount > 0
            ? label.transform.TransformPoint(new Vector3(label.textBounds.min.x, 0f, 0f)).x
            : label.rectTransform.TransformPoint(new Vector3(label.rectTransform.rect.xMin, 0f, 0f)).x;
        var nrt = nameText.rectTransform;
        Vector3 nameCenter = nrt.TransformPoint(nrt.rect.center);

        badge.alignment = TextAlignmentOptions.MidlineLeft;
        var brt = badge.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0f, 0.5f);
        brt.sizeDelta = new Vector2(160f, nrt.rect.height);
        brt.position = new Vector3(labelLeft, nameCenter.y, nrt.position.z);

        // 이름은 카드 기준 가운데 그대로 (Lv 때문에 밀지 않는다)
        var m = nameText.margin; m.x = 0f; nameText.margin = m;
    }

    /// <summary>[2026-10-22] 카드 왼쪽 위의 "Lv." 글자를 "No." 로 — 그 옆 숫자 칸(LvText)에는 자리 번호가 들어간다.</summary>
    public static void SetNumberLabel(Transform slot)
    {
        if (slot == null) return;
        foreach (var t in slot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (System.Array.IndexOf(LevelTextNames, t.name) >= 0) continue;
            string s = t.text != null ? t.text.Trim() : "";
            if (s != "Lv." && s != "Lv" && s != "LV." && s != "LV") continue;
            // "No." 는 "Lv." 보다 넓어 점이 다음 줄로 넘어갔다 — 줄바꿈을 끄고, 옆 숫자(LvText 자식)를 넓어진 만큼 민다
            float wider = t.GetPreferredValues("No.").x - t.GetPreferredValues(t.text).x;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.text = "No.";
            if (wider > 0f)
                foreach (Transform c in t.transform)
                    if (System.Array.IndexOf(LevelTextNames, c.name) >= 0) ((RectTransform)c).anchoredPosition += new Vector2(wider, 0f);
        }
    }

    public static Transform FindSlot(Transform root, int index)
    {
        if (root == null) return null;
        string[] names = { $"PartySlot_{index + 1}", $"PartySlot{index + 1}", $"PartySlot_{index}", $"PartySlot{index}" };
        foreach (string n in names)
        {
            Transform t = root.Find(n);
            if (t != null) return t;
        }
        return null;
    }

    public static TextMeshProUGUI FindText(Transform slot, params string[] names)
    {
        foreach (string n in names)
        {
            Transform t = FindDescendant(slot, n);
            if (t == null) continue;
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = t.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) return tmp;
        }
        return null;
    }

    public static List<Image> FindStatusIcons(Transform slot)
    {
        var icons = new List<Image>();
        Transform row = FindDescendant(slot, "StatusIconRow");
        if (row == null) return icons;
        for (int i = 1; i <= 4; i++)
        {
            Transform t = FindDescendant(row, $"StatusIcon_{i}") ?? FindDescendant(row, $"StatusIcon{i}");
            Image img = t != null ? t.GetComponent<Image>() : null;
            if (img != null) icons.Add(img);
        }
        return icons;
    }

    public static Transform FindDescendant(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            Transform f = FindDescendant(c, name);
            if (f != null) return f;
        }
        return null;
    }

    /// <summary>
    /// 상태이상 아이콘 표시 (없으면 모두 숨김). 아이콘: flatIcon 우선, 없으면 itemIcon.
    /// 오른쪽 칸부터 걸린 순서대로 채우고, 칸보다 많으면 1.5초마다 한 칸씩 돌아가며 보인다 (StatusIconRotator).
    /// icons = 왼쪽→오른쪽 순 (StatusIcon_1~4).
    /// </summary>
    public static void ShowStatusIcons(List<Image> icons, PlayerStats member, int companionId = 0)
    {
        if (icons == null || icons.Count == 0) return;

        var sprites = new List<Sprite>();
        if (member != null && member.activeStatusEffects != null)
        {
            foreach (var status in member.activeStatusEffects)
            {
                if (status == null || status.data == null) continue;
                Sprite s = status.data.flatIcon != null ? status.data.flatIcon : status.data.itemIcon;
                if (s != null) sprites.Add(s);
            }
        }
        // [2026-10-08] 던전(걷는 중) 상태이상 — 함정이 건 출혈·독 등은 주인공에게만. 전투 중이면 이미 위에 들어 있으므로 같은 종류는 건너뜀
        if (member != null && !member.IsRecruitedCompanion)
            foreach (var kv in DungeonFieldStatus.ActiveIcons())
            {
                bool shown = member.activeStatusEffects != null &&
                             member.activeStatusEffects.Exists(se => se != null && se.data != null && se.data.effectType == kv.Key);
                if (!shown && kv.Value != null) sprites.Add(kv.Value);
            }
        // [2026-10-21] 던전 카드의 동료 (PlayerStats 없음) — 맵에서 이어진 상태이상
        if (member == null && companionId != 0)
            foreach (var kv in DungeonFieldStatus.CompanionIcons(companionId))
                if (kv.Value != null) sprites.Add(kv.Value);

        Transform host = null;
        foreach (var img in icons) if (img != null) { host = img.transform.parent; break; }
        if (host == null) return;
        var rotator = host.GetComponent<StatusIconRotator>();
        if (rotator == null) rotator = host.gameObject.AddComponent<StatusIconRotator>();
        rotator.Set(icons, sprites);
    }

    /// <summary>
    /// 쓰러진 아군 카드에 해골 그림을 깐다 (카드 배경 위, 이름·HP 글자 아래). 살아나면 숨긴다.
    /// card = 글자들의 부모(카드 안쪽 배경). 그림이 없으면 아무것도 하지 않음.
    /// </summary>
    public static void SetDeathSkull(Transform card, bool dead)
    {
        if (card == null) return;
        Transform existing = card.Find(DeathSkullObjectName);
        if (!dead)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }
        if (existing == null)
        {
            if (_deathSkullSprite == null)
            {
                Texture2D tex = Resources.Load<Texture2D>(DeathSkullResource);
                if (tex == null)
                {
                    Debug.LogWarning($"[PartyCardVisuals] 사망 해골 그림 'Resources/{DeathSkullResource}' 을(를) 찾지 못했습니다.");
                    return;
                }
                _deathSkullSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            var go = new GameObject(DeathSkullObjectName, typeof(RectTransform));
            go.transform.SetParent(card, false);
            go.transform.SetAsFirstSibling(); // 카드 배경 위, 글자 아래
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, -6f);
            var img = go.AddComponent<Image>();
            img.sprite = _deathSkullSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0.9f);
            existing = go.transform;
        }
        existing.gameObject.SetActive(true);
    }
}
