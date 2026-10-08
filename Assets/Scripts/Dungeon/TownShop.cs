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
        "Item_Equipments/Items/Medicinal_Herb", // [2026-10-08] 약초가 맨 위
        "Item_Equipments/Items/HP_Potion",
        "Item_Equipments/Items/Mana_Potion",
        "Item_Equipments/Items/Antidote",
        "Item_Equipments/Items/Bandage",
        "Item_Equipments/Items/Coolant",
        // "Item_Equipments/Items/Purification_Water", // [2026-10-08] 모든 상태이상 치료라 너무 강해 일단 뺌
        "Item_Equipments/Items/Whetstone",
        "Item_Equipments/Items/Stimulant",
        "Item_Equipments/Items/Smoke_Bomb",
        // [2026-10-08] 조잡한 검은 무기점(CatalogArms)으로 옮김
    };

    /// <summary>[2026-10-08] 상점 종류: 잡화점(소비 아이템) / 무기점(장비).</summary>
    public enum Kind { General, Arms }

    /// <summary>
    /// [2026-10-08] 무기점 진열 — 등급순 (허접 → 조잡), 같은 등급 안에서는 무기 → 방패 → 방어구 → 장신구.
    /// 가격은 각 장비 에셋의 buyPrice (0 이면 진열돼도 안 보임).
    /// </summary>
    public static readonly string[] CatalogArms =
    {
        // 허접 (조잡한 등급 아래 — 스킬을 쓰기 위한 무기)
        "Item_Equipments/Equipments/Junk/Junk_WoodenStick",
        "Item_Equipments/Equipments/Junk/Junk_BoneShiv",
        "Item_Equipments/Equipments/Junk/Junk_BoneClub",
        "Item_Equipments/Equipments/Junk/Junk_RustyHatchet",
        "Item_Equipments/Equipments/Junk/Junk_BrokenSpearShaft",
        // 조잡 — 무기
        "Item_Equipments/Equipments/Crude/CrudeSword",
        "Item_Equipments/Equipments/Crude/Crude_Dagger",
        "Item_Equipments/Equipments/Crude/Crude_Axe",
        "Item_Equipments/Equipments/Crude/Crude_Hammer",
        "Item_Equipments/Equipments/Crude/Crude_Spear",
        "Item_Equipments/Equipments/Crude/Crude_Polearm",
        "Item_Equipments/Equipments/Crude/Crude_Katana",
        "Item_Equipments/Equipments/Crude/Crude_Greatsword",
        "Item_Equipments/Equipments/Crude/Crude_Bow",
        "Item_Equipments/Equipments/Crude/Crude_Crossbow",
        "Item_Equipments/Equipments/Crude/Crude_Staff",
        "Item_Equipments/Equipments/Crude/Crude_Wand",
        // 조잡 — 방패
        "Item_Equipments/Equipments/Crude/Crude_Buckler",
        "Item_Equipments/Equipments/Crude/Crude_Shield",
        "Item_Equipments/Equipments/Crude/CrudeShield",
        "Item_Equipments/Equipments/Crude/Crude_Greatshield",
        // 조잡 — 방어구 · 장신구
        "Item_Equipments/Equipments/Crude/CrudeArmor",
        "Item_Equipments/Equipments/Crude/CrudeBoots",
        "Item_Equipments/Equipments/Crude/CrudeBracelet",
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
        // [2026-10-08] 소비 아이템 그림은 itemIcon 칸에 있다 (icon 칸은 비어 있어 상점에 그림이 안 나왔음)
        public Sprite Icon { get { return consumable != null ? (consumable.itemIcon != null ? consumable.itemIcon : consumable.icon != null ? consumable.icon : consumable.flatIcon)
                                          : equipment != null ? (equipment.flatIcon != null ? equipment.flatIcon : equipment.equipmentIcon) : null; } }
    }

    public static List<Entry> BuyList(Kind kind = Kind.General)
    {
        var list = new List<Entry>();
        foreach (string path in kind == Kind.Arms ? CatalogArms : Catalog)
        {
            var so = Resources.Load<ScriptableObject>(path);
            if (so == null) { Debug.LogWarning($"[TownShop] 진열 품목 'Resources/{path}' 을(를) 찾지 못했습니다."); continue; }
            var e = new Entry { asset = so, consumable = so as ConsumableItemSO, equipment = so as EquipmentData };
            if (e.BaseBuyPrice > 0) list.Add(e);
        }
        return list;
    }

    /// <summary>파는 목록: 잡화점 = 소비 아이템, 무기점 = 장착하지 않은 장비 (같은 장비는 한 줄 — 여분이 있을 때만).</summary>
    public static List<Entry> SellList(Kind kind = Kind.General)
    {
        var list = new List<Entry>();
        var inv = ConsumableInventory.Instance;
        if (kind == Kind.General && inv != null)
            foreach (var s in inv.slots)
                if (s != null && s.item != null && s.quantity > 0 && !s.item.isDawnChalice && s.item.sellPrice > 0)
                    list.Add(new Entry { asset = s.item, consumable = s.item });
        if (kind == Kind.Arms)
        {
            var seen = new HashSet<EquipmentData>();
            foreach (var eq in EquipmentBag.Items)
                if (eq != null && eq.sellPrice > 0 && seen.Add(eq) && EquipmentBag.Count(eq) > WornCount(eq))
                    list.Add(new Entry { asset = eq, equipment = eq });
        }
        return list;
    }

    /// <summary>장비 등급 표시 (허접 / 조잡).</summary>
    public static string GradeLabel(EquipmentData q)
    {
        if (q == null) return "";
        if (q.isJunkWeapon) return "<color=#9A8F7A>Junk</color>";
        if (!string.IsNullOrEmpty(q.equipmentName) && q.equipmentName.StartsWith("Crude")) return "<color=#C9A86A>Crude</color>";
        return "";
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
            string grade = GradeLabel(q);
            if (grade != "") parts.Add(grade);
            if (q.attackBonus != 0) parts.Add("ATK " + (q.attackBonus > 0 ? "+" : "") + q.attackBonus);
            if (q.defenseBonus != 0) parts.Add("DEF " + (q.defenseBonus > 0 ? "+" : "") + q.defenseBonus);
            if (q.magicBonus != 0) parts.Add("MAG " + (q.magicBonus > 0 ? "+" : "") + q.magicBonus);
            if (q.hpBonus != 0) parts.Add("HP " + (q.hpBonus > 0 ? "+" : "") + q.hpBonus);
            if (q.agiBonus != 0) parts.Add("AGI " + (q.agiBonus > 0 ? "+" : "") + q.agiBonus);
            if (q.isTwoHanded) parts.Add("Two-handed");
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
        if (e.equipment != null) { int n = EquipmentBag.Count(e.equipment); return n > 0 ? "x" + n : ""; } // [2026-10-09] 같은 장비도 더 살 수 있음 — 가진 개수만
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
            // [2026-10-09] 같은 장비도 최대 30개까지 (쌍수용). 자동 장착 없음 — 상점 창이 "장착하시겠습니까?"를 묻는다
            if (!EquipmentBag.CanAdd(e.equipment)) return "<color=#FF6B6B>You cannot carry more.</color>";
            PlayerWallet.TrySpend(price);
            EquipmentBag.Add(e.equipment);
            return $"Bought <color=#FFD24A>{e.Name}</color>  (-{price} G)";
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
            if (EquipmentBag.Count(e.equipment) <= WornCount(e.equipment)) return "Unequip it first.";
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

    /// <summary>장착 칸 몇 곳에 이 장비가 끼워져 있는지 (같은 무기 양손이면 2).</summary>
    public static int WornCount(EquipmentData eq)
    {
        var d = HeroData();
        if (d == null || eq == null) return 0;
        int n = 0;
        if (d.rightHand == eq) n++;
        if (d.leftHand == eq) n++;
        if (d.body == eq) n++;
        if (d.accessory1 == eq) n++;
        if (d.accessory2 == eq) n++;
        return n;
    }

    /// <summary>[2026-10-09] 산 장비 장착 (상점의 "장착하시겠습니까?"에서 예). 한손 무기: 오른손 → 비었으면 왼손 (EquipmentManager 규칙).</summary>
    public static bool Equip(EquipmentData eq)
    {
        var d = HeroData();
        if (d == null || eq == null || EquipmentBag.Count(eq) <= WornCount(eq)) return false; // 여분이 있어야 함
        var mgr = Object.FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
        if (mgr != null) return mgr.EquipItem(eq);
        if (d.rightHand == null) d.rightHand = eq; // 장비 관리자가 없으면 데이터에 직접
        else if (eq.equipmentType == EquipmentType.Hand && d.leftHand == null) d.leftHand = eq;
        else d.rightHand = eq;
        return true;
    }
}
