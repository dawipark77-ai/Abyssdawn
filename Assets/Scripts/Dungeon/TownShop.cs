using System.Collections.Generic;
using AbyssdawnBattle;
using UnityEngine;

/// <summary>
/// 마을 상점의 규칙 (화면은 DungeonHud 의 상점 창).
///  - 파는 물건: Catalog (Resources 경로). 가격은 각 에셋의 buyPrice / sellPrice — Inspector 에서 바꾸면 된다.
///  - 사기: 돈이 있고 들고 갈 자리가 있으면. 장비는 하나씩만 가지며, 오른손이 비어 있으면 바로 장착.
///  - 팔기: 가진 소비 아이템(새벽의 잔 제외), 장착하지 않은 장비. sellPrice 가 0 이면 팔 수 없음.
/// </summary>
public static class TownShop
{
    /// <summary>상점 진열 (Resources 경로). 2026-10-04: 포션·마나 포션·해독약·붕대·쿨런트 + 조잡한 검.
    /// 2026-10-07: 약초·정화수·숫돌·각성제·연막탄 추가 (함정 상태이상 대비·전투 보조 — 골드 소모처).</summary>
    public static readonly string[] Catalog =
    {
        "Item_Equipments/Items/HP_Potion",
        "Item_Equipments/Items/Medicinal_Herb",
        "Item_Equipments/Items/Mana_Potion",
        "Item_Equipments/Items/Antidote",
        "Item_Equipments/Items/Bandage",
        "Item_Equipments/Items/Coolant",
        "Item_Equipments/Items/Purification_Water",
        "Item_Equipments/Items/Whetstone",
        "Item_Equipments/Items/Stimulant",
        "Item_Equipments/Items/Smoke_Bomb",
        "Item_Equipments/Equipments/Crude/CrudeSword",
    };

    /// <summary>탐험 스킬 '자원 관리': 사는 값 -10%, 파는 값 +10%.</summary>
    public const float ResourceBuyMult = 0.9f, ResourceSellMult = 1.1f;

    public static bool HasResourceManagement()
    {
        var d = HeroData();
        return d != null && d.HasFieldSkill(FieldSkills.ResourceManagement);
    }

    public class Entry
    {
        public ScriptableObject asset;
        public ConsumableItemSO consumable;
        public EquipmentData equipment;
        public string Name { get { return consumable != null ? consumable.itemName : equipment != null ? equipment.equipmentName : asset.name; } }
        public int BaseBuyPrice { get { return consumable != null ? consumable.buyPrice : equipment != null ? equipment.buyPrice : 0; } }
        public int BaseSellPrice { get { return consumable != null ? consumable.sellPrice : equipment != null ? equipment.sellPrice : 0; } }
        public int BuyPrice { get { int p = BaseBuyPrice; return p > 0 && HasResourceManagement() ? Mathf.Max(1, Mathf.FloorToInt(p * ResourceBuyMult)) : p; } }
        public int SellPrice { get { int p = BaseSellPrice; return p > 0 && HasResourceManagement() ? Mathf.CeilToInt(p * ResourceSellMult) : p; } }
        public Sprite Icon { get { return consumable != null ? consumable.icon : equipment != null ? (equipment.flatIcon != null ? equipment.flatIcon : equipment.equipmentIcon) : null; } }
    }

    public static List<Entry> BuyList()
    {
        var list = new List<Entry>();
        foreach (string path in Catalog)
        {
            var so = Resources.Load<ScriptableObject>(path);
            if (so == null) { Debug.LogWarning($"[TownShop] 진열 품목 'Resources/{path}' 을(를) 찾지 못했습니다."); continue; }
            var e = new Entry { asset = so, consumable = so as ConsumableItemSO, equipment = so as EquipmentData };
            if (e.BaseBuyPrice > 0) list.Add(e);
        }
        return list;
    }

    public static List<Entry> SellList()
    {
        var list = new List<Entry>();
        var inv = ConsumableInventory.Instance;
        if (inv != null)
            foreach (var s in inv.slots)
                if (s != null && s.item != null && s.quantity > 0 && !s.item.isDawnChalice && s.item.sellPrice > 0)
                    list.Add(new Entry { asset = s.item, consumable = s.item });
        foreach (var eq in EquipmentBag.Items)
            if (eq != null && eq.sellPrice > 0 && !IsEquipped(eq))
                list.Add(new Entry { asset = eq, equipment = eq });
        return list;
    }

    /// <summary>표시용 효과 한 줄 (한글 글꼴이 없어 영어로 — 에셋 설명 대신 수치로 만든다).</summary>
    public static string InfoText(Entry e)
    {
        var parts = new List<string>();
        if (e.consumable != null)
        {
            var c = e.consumable;
            if (c.hpRecoveryPercent > 0f) parts.Add("HP +" + Mathf.RoundToInt(c.hpRecoveryPercent * 100f) + "%");
            if (c.mpRecoveryPercent > 0f) parts.Add("MP +" + Mathf.RoundToInt(c.mpRecoveryPercent * 100f) + "%");
            if (c.cureTypes != null && c.cureTypes.Count > 0)
            {
                var cures = new List<string>();
                foreach (var t in c.cureTypes) cures.Add(t.ToString());
                parts.Add(cures.Count >= 5 ? "Cures all ailments" : "Cures " + string.Join(", ", cures.ToArray()));
            }
            if (c.buffDuration > 0)
            {
                if (c.attackBuffPercent > 0f) parts.Add("ATK +" + Mathf.RoundToInt(c.attackBuffPercent * 100f) + "%");
                if (c.agilityBuff != 0) parts.Add("Speed +" + c.agilityBuff);
                if (c.evasionBuff > 0f) parts.Add("Evasion +" + Mathf.RoundToInt(c.evasionBuff * 100f) + "%");
                if (c.escapeChanceBuff > 0f) parts.Add("Escape +" + Mathf.RoundToInt(c.escapeChanceBuff * 100f) + "%");
                parts.Add(c.buffDuration + " turns");
            }
            if (c.mpPenaltyPercent > 0f) parts.Add("MP -" + Mathf.RoundToInt(c.mpPenaltyPercent * 100f) + "%");
            if (!c.usableOnMap) parts.Add("Battle only");
        }
        else if (e.equipment != null)
        {
            var q = e.equipment;
            if (q.attackBonus != 0) parts.Add("ATK " + (q.attackBonus > 0 ? "+" : "") + q.attackBonus);
            if (q.defenseBonus != 0) parts.Add("DEF " + (q.defenseBonus > 0 ? "+" : "") + q.defenseBonus);
            if (q.hpBonus != 0) parts.Add("HP " + (q.hpBonus > 0 ? "+" : "") + q.hpBonus);
        }
        return string.Join("  ·  ", parts.ToArray());
    }

    /// <summary>표시용: 가진 개수 / 장착 여부.</summary>
    public static string OwnedText(Entry e)
    {
        if (e.consumable != null)
        {
            int q = ConsumableInventory.Instance != null ? ConsumableInventory.Instance.GetQuantity(e.consumable) : 0;
            return "x" + q;
        }
        if (e.equipment != null) return IsEquipped(e.equipment) ? "Equipped" : EquipmentBag.Contains(e.equipment) ? "Owned" : "";
        return "";
    }

    /// <summary>사기. 결과 문구를 돌려준다 (알림용).</summary>
    public static string Buy(Entry e)
    {
        int price = e.BuyPrice;
        if (PlayerWallet.Gold < price) return "<color=#FF6B6B>Not enough gold.</color>";

        if (e.consumable != null)
        {
            var inv = ConsumableInventory.Instance;
            if (inv == null || inv.AddItem(e.consumable, 1) <= 0) return "<color=#FF6B6B>You cannot carry more.</color>";
            PlayerWallet.TrySpend(price);
            return $"Bought <color=#FFD24A>{e.Name}</color>  (-{price} G)";
        }
        if (e.equipment != null)
        {
            if (EquipmentBag.Contains(e.equipment)) return "You already own this.";
            PlayerWallet.TrySpend(price);
            EquipmentBag.Add(e.equipment);
            bool equipped = TryEquipIfHandEmpty(e.equipment);
            return $"Bought <color=#FFD24A>{e.Name}</color>  (-{price} G)" + (equipped ? "\n<size=80%>Equipped.</size>" : "");
        }
        return "";
    }

    /// <summary>하나 팔기. 결과 문구를 돌려준다.</summary>
    public static string Sell(Entry e)
    {
        int price = e.SellPrice;
        if (e.consumable != null)
        {
            var inv = ConsumableInventory.Instance;
            if (inv == null || !inv.RemoveItem(e.consumable, 1)) return "";
            PlayerWallet.Add(price);
            return $"Sold <color=#FFD24A>{e.Name}</color>  (+{price} G)";
        }
        if (e.equipment != null)
        {
            if (IsEquipped(e.equipment)) return "Unequip it first.";
            if (!EquipmentBag.Remove(e.equipment)) return "";
            PlayerWallet.Add(price);
            return $"Sold <color=#FFD24A>{e.Name}</color>  (+{price} G)";
        }
        return "";
    }

    // ─────────────────────────────────────────

    private static PlayerStatData HeroData()
    {
        foreach (var p in Object.FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
            if (p != null && !p.IsRecruitedCompanion && p.statData != null) return p.statData;
        return null;
    }

    public static bool IsEquipped(EquipmentData eq)
    {
        var d = HeroData();
        return d != null && (d.rightHand == eq || d.leftHand == eq || d.body == eq || d.accessory1 == eq || d.accessory2 == eq);
    }

    /// <summary>무기를 샀는데 오른손이 비어 있으면 바로 장착 (EquipmentManager 규칙 그대로).</summary>
    private static bool TryEquipIfHandEmpty(EquipmentData eq)
    {
        var d = HeroData();
        if (d == null || d.rightHand != null) return false;
        var mgr = Object.FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
        if (mgr != null) return mgr.EquipItem(eq);
        d.rightHand = eq; // 장비 관리자가 없으면 데이터에 직접
        return true;
    }
}
