using UnityEngine;
using UnityEditor;
using System.IO;

namespace AbyssdawnBattle.Editor
{
    public class SkillAssetGenerator
    {
        [MenuItem("Tools/Game Setup/스킬 에셋 생성")]
        public static void GenerateSkillAssets()
        {
            string folderPath = "Assets/Resources/Skills";

            // Create folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Skills");
            }

            // Generate 7 skills
            CreateSkill_01_Slash(folderPath);
            CreateSkill_02_StrongSlash(folderPath);
            CreateSkill_03_Fireball(folderPath);
            CreateSkill_04_Meditation(folderPath);
            CreateSkill_05_MagicBolt(folderPath);
            CreateSkill_06_Quickhand(folderPath);
            CreateSkill_07_ShieldWall(folderPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("✅ 7개 스킬 에셋 생성 완료: " + folderPath);
        }

        private static void CreateSkill_01_Slash(string folderPath)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillID = "01";
            skill.skillName = "Slash";
            skill.description = "Quick physical attack dealing moderate damage.";
            skill.usageType = UsageType.Active;
            skill.damageType = DamageType.Physical;
            skill.scalingStat = ScaleStat.Attack;
            skill.hpCostPercent = 5f;
            skill.mpCost = 0;
            skill.minMult = 1.5f;
            skill.maxMult = 1.8f;
            skill.hitCount = 1;

            SaveSkillAsset(skill, folderPath, "Skill_01_Slash.asset");
        }

        private static void CreateSkill_02_StrongSlash(string folderPath)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillID = "02";
            skill.skillName = "Strong Slash";
            skill.description = "Powerful physical attack with higher damage but greater HP cost.";
            skill.usageType = UsageType.Active;
            skill.damageType = DamageType.Physical;
            skill.scalingStat = ScaleStat.Attack;
            skill.hpCostPercent = 10f;
            skill.mpCost = 0;
            skill.minMult = 2.0f;
            skill.maxMult = 2.5f;
            skill.hitCount = 1;

            SaveSkillAsset(skill, folderPath, "Skill_02_StrongSlash.asset");
        }

        private static void CreateSkill_03_Fireball(string folderPath)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillID = "03";
            skill.skillName = "Fireball";
            skill.description = "Launches a fireball. Has a chance to ignite both user and target.";
            skill.usageType = UsageType.Active;
            skill.damageType = DamageType.Magic;
            skill.scalingStat = ScaleStat.Magic;
            skill.hpCostPercent = 0f;
            skill.mpCost = 5;
            skill.minMult = 1.8f;
            skill.maxMult = 2.3f;
            skill.hitCount = 1;
            skill.selfStatusEffectChance = 10f;
            skill.effects.Add(new SkillEffect
            {
                effectType = EffectType.Damage,
                statusEffectChance = 10f
            });
            skill.skillIcon = LoadSpriteByGUID("c43880bdb1e15444a8fe104ce0204f40");

            SaveSkillAsset(skill, folderPath, "Skill_03_Fireball.asset");
        }

        private static void CreateSkill_04_Meditation(string folderPath)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillID = "04";
            skill.skillName = "Meditation";
            skill.description = "Restores 7% of maximum MP.";
            skill.usageType = UsageType.Active;
            skill.damageType = DamageType.Magic;
            skill.scalingStat = ScaleStat.Magic;
            skill.hpCostPercent = 0f;
            skill.mpCost = 0;
            skill.minMult = 0f;
            skill.maxMult = 0f;
            skill.hitCount = 0;
            skill.effects.Add(new SkillEffect
            {
                effectType = EffectType.Recovery,
                recoveryTarget = RecoveryTarget.MP,
                effectAmount = 7f
            });

            SaveSkillAsset(skill, folderPath, "Skill_04_Meditation.asset");
        }

        private static void CreateSkill_05_MagicBolt(string folderPath)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillID = "05";
            skill.skillName = "Magic Bolt";
            skill.description = "Low-cost magic attack with a risk of self-damage.";
            skill.usageType = UsageType.Active;
            skill.damageType = DamageType.Magic;
            skill.scalingStat = ScaleStat.Magic;
            skill.hpCostPercent = 0f;
            skill.mpCost = 3;
            skill.minMult = 1.2f;
            skill.maxMult = 1.3f;
            skill.hitCount = 1;
            skill.selfDmgPercent = 5f;
            skill.selfDmgChance = 10f;
            skill.skillIcon = LoadSpriteByGUID("5e22fb5704628704bb694d9eefe8dc40");

            SaveSkillAsset(skill, folderPath, "Skill_05_MagicBolt.asset");
        }

        private static void CreateSkill_06_Quickhand(string folderPath)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillID = "06";
            skill.skillName = "Quickhand";
            skill.description = "Quick double strike based on agility.";
            skill.usageType = UsageType.Active;
            skill.damageType = DamageType.Physical;
            skill.scalingStat = ScaleStat.Agility;
            skill.hpCostPercent = 0f;
            skill.mpCost = 0;
            skill.minMult = 0.6f;
            skill.maxMult = 0.8f;
            skill.hitCount = 2;
            skill.skillIcon = LoadSpriteByGUID("427edb27ad7da094b984c5f369e757da");

            SaveSkillAsset(skill, folderPath, "Skill_06_Quickhand.asset");
        }

        private static void CreateSkill_07_ShieldWall(string folderPath)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillID = "07";
            skill.skillName = "Shield Wall";
            skill.description = "Passive: Increases defense by 20% when defending.";
            skill.usageType = UsageType.Passive;
            skill.damageType = DamageType.Physical;
            skill.scalingStat = ScaleStat.Defense;
            skill.hpCostPercent = 0f;
            skill.mpCost = 0;
            skill.minMult = 0f;
            skill.maxMult = 0f;
            skill.hitCount = 0;
            skill.triggerCondition = TriggerCondition.OnDefense;
            skill.effects.Add(new SkillEffect
            {
                effectType = EffectType.BuffDefense,
                effectAmount = 20f
            });

            SaveSkillAsset(skill, folderPath, "Skill_07_ShieldWall.asset");
        }

        private static Sprite LoadSpriteByGUID(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning($"Could not find asset for GUID: {guid}");
                return null;
            }

            // Try direct load
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            // If direct load fails, try loading all sub-assets (for texture atlases)
            if (sprite == null)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var asset in assets)
                {
                    if (asset is Sprite s)
                    {
                        sprite = s;
                        break;
                    }
                }
            }

            if (sprite == null)
            {
                Debug.LogWarning($"Failed to load sprite at path: {assetPath}");
            }

            return sprite;
        }

        private static void SaveSkillAsset(SkillData skill, string folderPath, string fileName)
        {
            string fullPath = Path.Combine(folderPath, fileName);

            // Check if asset already exists
            var existingAsset = AssetDatabase.LoadAssetAtPath<SkillData>(fullPath);
            if (existingAsset != null)
            {
                Debug.Log($"Updating existing skill: {fileName}");
                EditorUtility.CopySerialized(skill, existingAsset);
                EditorUtility.SetDirty(existingAsset);
            }
            else
            {
                Debug.Log($"Creating new skill: {fileName}");
                AssetDatabase.CreateAsset(skill, fullPath);
            }
        }
    }
}
