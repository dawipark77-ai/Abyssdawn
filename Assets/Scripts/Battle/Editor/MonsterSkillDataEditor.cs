using UnityEngine;
using UnityEditor;

namespace AbyssdawnBattle
{
    [CustomEditor(typeof(MonsterSkillData))]
    public class MonsterSkillDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            MonsterSkillData skill = (MonsterSkillData)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("분류 (자동 판정 — 읽기 전용)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Category", skill.Category);
            }
        }
    }
}
