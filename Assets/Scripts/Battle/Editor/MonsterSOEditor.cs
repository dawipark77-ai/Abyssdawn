using UnityEngine;
using UnityEditor;
using Abyssdawn;   // MonsterSO (namespace Abyssdawn)

namespace AbyssdawnBattle
{
    [CustomEditor(typeof(MonsterSO))]
    public class MonsterSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            MonsterSO monster = (MonsterSO)target;

            if (monster.BasicAttackOverride != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    $"본능 사용 시 '{monster.BasicAttackOverride.skillName}'의 HP 코스트({monster.BasicAttackOverride.hpCostPercent}%)는 면제됩니다.\n" +
                    $"단, 다른 몬스터가 같은 스킬을 액티브로 배우면 정상 차감됩니다.",
                    MessageType.Info);
            }
        }
    }
}
