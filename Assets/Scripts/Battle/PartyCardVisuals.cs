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
    public static void ShowStatusIcons(List<Image> icons, PlayerStats member)
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
