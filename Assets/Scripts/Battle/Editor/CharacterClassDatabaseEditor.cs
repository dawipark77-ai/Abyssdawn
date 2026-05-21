#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 6 기본 인스펙터가 <c>List&lt;CharacterClass&gt;</c>를 UI Toolkit ListView로 그릴 때
/// <see cref="SerializedObject.isEditingMultipleObjects"/> NRE가 나는 경우가 있어,
/// 순수 IMGUI로만 그려 기본 인스펙터 경로를 우회합니다.
/// </summary>
[CustomEditor(typeof(CharacterClassDatabase))]
public sealed class CharacterClassDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target == null)
            return;

        serializedObject.Update();

        SerializedProperty it = serializedObject.GetIterator();
        bool enterChildren = true;
        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (it.name == "m_Script")
                continue;

            EditorGUILayout.PropertyField(it, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
