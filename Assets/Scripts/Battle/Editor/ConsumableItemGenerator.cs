#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AbyssdawnBattle;

/// <summary>
/// Tools > Abyssdawn > Generate Consumable Items
/// Assets/Items/Consumables/ 경로에 소비 아이템 SO 10개를 생성합니다.
/// </summary>
public static class ConsumableItemGenerator
{
    private const string OUTPUT_PATH = "Assets/Items/Consumables";

    [MenuItem("Tools/Abyssdawn/Generate Consumable Items", priority = 210)]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Items"))
            AssetDatabase.CreateFolder("Assets", "Items");

        if (!AssetDatabase.IsValidFolder(OUTPUT_PATH))
            AssetDatabase.CreateFolder("Assets/Items", "Consumables");

        int created = 0;
        int skipped = 0;

        foreach (var def in BuildDefinitions())
        {
            string path = $"{OUTPUT_PATH}/{def.fileName}.asset";

            if (AssetDatabase.LoadAssetAtPath<ConsumableItemSO>(path) != null)
            {
                Debug.Log($"[ConsumableItemGenerator] 이미 존재 — 건너뜀: {def.fileName}");
                skipped++;
                continue;
            }

            var so = ScriptableObject.CreateInstance<ConsumableItemSO>();
            def.apply(so);
            AssetDatabase.CreateAsset(so, path);
            created++;
            Debug.Log($"[ConsumableItemGenerator] 생성: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("ConsumableItemGenerator",
            $"완료!\n생성: {created}개  /  건너뜀(이미 존재): {skipped}개\n\n경로: {OUTPUT_PATH}",
            "확인");
    }

    // ═════════════════════════════════════════════════════════
    //  아이템 정의 목록
    // ═════════════════════════════════════════════════════════

    private struct ItemDef
    {
        public string                      fileName;
        public System.Action<ConsumableItemSO> apply;
    }

    private static IEnumerable<ItemDef> BuildDefinitions()
    {
        // ── 1. 새벽의 잔 ──────────────────────────────────────
        yield return new ItemDef
        {
            fileName = "Dawn_Chalice",
            apply = so =>
            {
                so.itemName           = "새벽의 잔";
                so.description        = "심연 속에서도 새벽은 온다. 아직은, 그렇게 믿어야 한다.";
                so.hpRecoveryPercent  = 0.4f;
                so.mpRecoveryPercent  = 0.4f;
                so.cureTypes          = AllCureTypes();
                so.maxStack           = 3;
                so.usableInBattle     = true;
                so.usableOnMap        = true;
                so.isDawnChalice      = true;
                so.stackGroup         = "";
            }
        };

        // ── 2. HP 포션 ────────────────────────────────────────
        yield return new ItemDef
        {
            fileName = "HP_Potion",
            apply = so =>
            {
                so.itemName           = "HP 포션";
                so.description        = "살아있으면 된다.";
                so.hpRecoveryPercent  = 0.3f;
                so.mpRecoveryPercent  = 0f;
                so.maxStack           = 5;
                so.usableInBattle     = true;
                so.usableOnMap        = true;
                so.stackGroup         = "HealthPotion";  // MP 포션과 합산 최대 5
            }
        };

        // ── 3. 마나 포션 ──────────────────────────────────────
        yield return new ItemDef
        {
            fileName = "Mana_Potion",
            apply = so =>
            {
                so.itemName           = "마나 포션";
                so.description        = "마력이 돌아온다. 잠시지만.";
                so.hpRecoveryPercent  = 0f;
                so.mpRecoveryPercent  = 0.3f;
                so.maxStack           = 5;
                so.usableInBattle     = true;
                so.usableOnMap        = true;
                so.stackGroup         = "HealthPotion";  // HP 포션과 합산 최대 5
            }
        };

        // ── 4. 해독제 ─────────────────────────────────────────
        yield return new ItemDef
        {
            fileName = "Antidote",
            apply = so =>
            {
                so.itemName       = "해독제";
                so.description    = "독은 시간이 지나면 죽인다. 이것은 그 시간을 빼앗는다.";
                so.cureTypes      = new List<StatusEffectType> { StatusEffectType.Poison };
                so.maxStack       = 5;
                so.usableInBattle = true;
                so.usableOnMap    = true;
            }
        };

        // ── 5. 숫돌 ───────────────────────────────────────────
        yield return new ItemDef
        {
            fileName = "Whetstone",
            apply = so =>
            {
                so.itemName           = "숫돌";
                so.description        = "날이 서면 덜 힘들다.";
                so.attackBuffPercent  = 0.15f;
                so.buffDuration       = 3;
                so.maxStack           = 5;
                so.usableInBattle     = true;
                so.usableOnMap        = false;
            }
        };

        // ── 6. 연막탄 ─────────────────────────────────────────
        yield return new ItemDef
        {
            fileName = "Smoke_Bomb",
            apply = so =>
            {
                so.itemName          = "연막탄";
                so.description       = "보이지 않으면 맞지 않는다.";
                so.evasionBuff       = 0.2f;
                so.escapeChanceBuff  = 0.4f;
                so.buffDuration      = 1;
                so.maxStack          = 5;
                so.usableInBattle    = true;
                so.usableOnMap       = false;
            }
        };

        // ── 7. 냉각수 ─────────────────────────────────────────
        yield return new ItemDef
        {
            fileName = "Coolant",
            apply = so =>
            {
                so.itemName       = "냉각수";
                so.description    = "불은 끌 수 있다. 그 흔적은 남지만.";
                so.cureTypes      = new List<StatusEffectType> { StatusEffectType.Ignite };
                so.maxStack       = 5;
                so.usableInBattle = true;
                so.usableOnMap    = true;
            }
        };

        // ── 8. 붕대 ───────────────────────────────────────────
        yield return new ItemDef
        {
            fileName = "Bandage",
            apply = so =>
            {
                so.itemName          = "붕대";
                so.description       = "거칠게 묶어도 피는 멈춘다.";
                so.hpRecoveryPercent = 0.2f;
                so.cureTypes         = new List<StatusEffectType> { StatusEffectType.Bleed };
                so.maxStack          = 5;
                so.usableInBattle    = true;
                so.usableOnMap       = true;
            }
        };

        // ── 9. 각성제 ─────────────────────────────────────────
        yield return new ItemDef
        {
            fileName = "Stimulant",
            apply = so =>
            {
                so.itemName          = "각성제";
                so.description       = "잠깐의 각성. 반드시 대가가 따른다.";
                so.attackBuffPercent = 0.1f;
                so.agilityBuff       = 10;
                so.buffDuration      = 2;
                so.mpPenaltyPercent  = 0.15f;
                so.maxStack          = 5;
                so.usableInBattle    = true;
                so.usableOnMap       = false;
            }
        };

        // ── 10. 정화수 ────────────────────────────────────────
        yield return new ItemDef
        {
            fileName = "Purification_Water",
            apply = so =>
            {
                so.itemName       = "정화수";
                so.description    = "심연에서 건져낸 물. 모든 어둠을 걷어낸다.";
                so.cureTypes      = AllCureTypes();
                so.maxStack       = 1;
                so.usableInBattle = true;
                so.usableOnMap    = true;
            }
        };
    }

    private static List<StatusEffectType> AllCureTypes() => new List<StatusEffectType>
    {
        StatusEffectType.Bleed,
        StatusEffectType.Poison,
        StatusEffectType.Ignite,
        StatusEffectType.Stun,
        StatusEffectType.Weakness,
        StatusEffectType.Slow,
        StatusEffectType.Silence
    };
}
#endif
