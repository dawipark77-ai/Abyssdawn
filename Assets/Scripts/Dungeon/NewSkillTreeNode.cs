using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 새 스킬트리 UI의 개별 노드 표시 컴포넌트.
/// 로직은 NewSkillTreeUI에서 관리하며, 이 클래스는 순수 시각/이벤트 담당.
/// </summary>
public class NewSkillTreeNode : MonoBehaviour
{
    [Header("UI References")]
    public Image bgImage;
    public Image borderImage;
    public Image iconImage;
    public GameObject learnedBadge;
    public GameObject lockedOverlay;
    public TextMeshProUGUI nameLabel;
    public Button button;

    // ── 상태 색상 ──────────────────────────────────────────────────
    private static readonly Color BgLocked    = new Color(0.15f, 0.15f, 0.18f, 0.9f);
    private static readonly Color BgAvailable = new Color(0.18f, 0.18f, 0.22f, 0.95f);
    private static readonly Color BgLearned   = new Color(0.10f, 0.28f, 0.14f, 0.95f);

    private static readonly Color BorderLocked    = new Color(0.35f, 0.35f, 0.40f, 1f);
    private static readonly Color BorderAvailable = new Color(0.92f, 0.80f, 0.30f, 1f);
    private static readonly Color BorderLearned   = new Color(0.25f, 0.90f, 0.45f, 1f);

    private static readonly Color IconLocked    = new Color(0.35f, 0.35f, 0.35f, 0.6f);
    private static readonly Color IconAvailable = Color.white;
    private static readonly Color IconLearned   = new Color(0.85f, 1f, 0.85f, 1f);
    // ───────────────────────────────────────────────────────────────

    public SkillData SkillData { get; private set; }
    public NewSkillTreeUI.NodeState State { get; private set; }

    public void Setup(SkillData data, Action<NewSkillTreeNode> onClicked)
    {
        SkillData = data;

        if (iconImage != null)
            iconImage.sprite = data.skillIcon;

        if (nameLabel != null)
            nameLabel.text = data.skillName;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(this));
        }
    }

    public void SetState(NewSkillTreeUI.NodeState state)
    {
        State = state;

        bool locked  = state == NewSkillTreeUI.NodeState.Locked;
        bool learned = state == NewSkillTreeUI.NodeState.Learned;

        if (bgImage != null)
            bgImage.color = locked ? BgLocked : learned ? BgLearned : BgAvailable;

        if (borderImage != null)
            borderImage.color = locked ? BorderLocked : learned ? BorderLearned : BorderAvailable;

        if (iconImage != null)
            iconImage.color = locked ? IconLocked : learned ? IconLearned : IconAvailable;

        if (learnedBadge != null)
            learnedBadge.SetActive(learned);

        if (lockedOverlay != null)
            lockedOverlay.SetActive(locked);
    }
}
