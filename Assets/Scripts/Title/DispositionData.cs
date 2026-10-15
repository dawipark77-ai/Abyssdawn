using UnityEngine;

/// <summary>
/// [2026-10-14] 성향 (새 게임에서 고름). 아직 기획·아이콘이 없어 틀만 만들어 둠 — 효과 없음.
/// 에셋을 Resources/Dispositions/ 에 만들면 새 게임 화면 목록에 나온다 (없으면 "준비 중" 한 줄로 지나감).
/// </summary>
[CreateAssetMenu(fileName = "Disposition_", menuName = "Abyssdawn/Disposition", order = 12)]
public class DispositionData : ScriptableObject
{
    public string dispositionName;
    public string dispositionNameKR;
    [Tooltip("없으면 이름 글자를 그림 칸 위에 대신 씀")]
    public Sprite icon;
    [TextArea(2, 4)] public string description;
    [TextArea(2, 4)] public string descriptionKR;
}
