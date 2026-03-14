#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using AbyssdawnBattle;

/// <summary>
/// EquipmentData 커스텀 인스펙터
/// ArmorBreakDataSO / BlockDataSO 끼우면 예상 수치를 즉시 표시합니다. (B안 미리보기)
/// </summary>
[CustomEditor(typeof(EquipmentData))]
public class EquipmentDataEditor : Editor
{
    // 미리보기 계산용 캐릭터 방어력 (에디터에서 직접 조정 가능)
    private int previewDefense = 10;
    private bool showPreview = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EquipmentData eq = (EquipmentData)target;

        bool hasArmorBreak = eq.armorBreakData != null || eq.armorBreakCoefficient > 0f;
        bool hasBlock = eq.blockData != null;

        if (!hasArmorBreak && !hasBlock) return;

        EditorGUILayout.Space(10);
        showPreview = EditorGUILayout.Foldout(showPreview, "⚔ 전투 수치 미리보기", true, EditorStyles.foldoutHeader);
        if (!showPreview) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        previewDefense = EditorGUILayout.IntSlider("기준 방어력", previewDefense, 0, 100);

        // ───── 방어구 파괴 미리보기 ─────
        if (hasArmorBreak)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("── 방어구 파괴 ──", EditorStyles.boldLabel);

            float coeff = eq.armorBreakData != null ? eq.armorBreakData.coefficient : eq.armorBreakCoefficient;

            float singleHit  = previewDefense * coeff * 1.0f;
            float dualHitMin = previewDefense * coeff * 0.35f;
            float dualHitMax = previewDefense * coeff * 0.45f;
            float critSingle = singleHit * 1.5f;

            EditorGUILayout.LabelField($"  계수: {coeff:F2}");
            EditorGUILayout.LabelField($"  한손 1타 파괴량:  {singleHit:F2}  (크리 {critSingle:F2})");
            EditorGUILayout.LabelField($"  쌍수 1타 파괴량:  {dualHitMin:F2} ~ {dualHitMax:F2}");

            // 쌍수 2타 누적 (중간값 0.40 기준)
            float f = 0.40f;
            float arm1 = previewDefense * coeff * f;
            float rem  = Mathf.Max(0f, previewDefense - arm1);
            float arm2 = rem * coeff * f;
            EditorGUILayout.LabelField($"  쌍수 2타 합산 (f=0.40 기준):  {arm1 + arm2:F2}");
        }

        // ───── 블록 미리보기 ─────
        if (hasBlock)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("── 방패 블록 ──", EditorStyles.boldLabel);

            BlockDataSO bd = eq.blockData;
            float chance   = bd.GetBlockChance(previewDefense);
            float reduction = bd.GetDamageReduction(previewDefense);

            EditorGUILayout.LabelField($"  기본 블록 확률:   {bd.baseBlockChance * 100f:F1}%");
            EditorGUILayout.LabelField($"  방어력 반영 계수: {bd.blockDefenseCoefficient}");
            EditorGUILayout.LabelField($"  ▶ 최종 블록 확률: {chance * 100f:F1}%", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  ▶ 피해 감소량:    {reduction:F1}", EditorStyles.boldLabel);
        }

        EditorGUILayout.EndVertical();
    }
}
#endif
