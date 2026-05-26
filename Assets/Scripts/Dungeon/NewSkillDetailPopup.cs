using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 스킬 노드 탭 시 하단에서 올라오는 디테일 팝업.
/// NewSkillTreeUI에서 Show() / Hide()를 호출한다.
/// </summary>
public class NewSkillDetailPopup : MonoBehaviour
{
    [Header("Panel")]
    public RectTransform panelRect;
    public Button dimBgButton;

    [Header("Skill Info")]
    public Image skillIconImage;
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI skillTypeText;
    public TextMeshProUGUI descriptionText;

    [Header("Stats Row")]
    public TextMeshProUGUI damageText;
    public TextMeshProUGUI mpCostText;
    public TextMeshProUGUI lpCostText;
    public TextMeshProUGUI hitCountText;

    [Header("Prerequisites")]
    public TextMeshProUGUI prerequisitesText;
    public GameObject prerequisitesRow;

    [Header("Actions")]
    public Button learnButton;
    public TextMeshProUGUI learnButtonLabel;
    public Button closeButton;

    [Header("Animation")]
    [Tooltip("슬라이드 애니메이션 시간 (초)")]
    public float slideTime = 0.22f;

    // ── Private ────────────────────────────────────────────────────
    private float _panelHeight;
    private Coroutine _slideCoroutine;
    private Action _onLearnCallback;
    private bool _isVisible;

    private static readonly Color LearnableColor   = new Color(0.25f, 0.80f, 0.45f, 1f);
    private static readonly Color UnlearnableColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    private static readonly Color AlreadyLearnedColor = new Color(0.25f, 0.60f, 0.90f, 1f);

    private void Awake()
    {
        gameObject.SetActive(false);

        if (dimBgButton != null)
            dimBgButton.onClick.AddListener(Hide);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    private void Start()
    {
        if (panelRect != null)
            _panelHeight = panelRect.rect.height;
    }

    /// <summary>
    /// 팝업을 표시한다.
    /// </summary>
    /// <param name="data">표시할 스킬 데이터</param>
    /// <param name="state">현재 노드 상태</param>
    /// <param name="onLearn">습득 버튼 클릭 시 호출할 콜백</param>
    public void Show(SkillData data, NewSkillTreeUI.NodeState state, Action onLearn)
    {
        Debug.Log($"[Learn-DIAG] [Popup] Show 진입 — skill='{(data != null ? data.skillName : "NULL")}', state={state}, onLearn callback={(onLearn == null ? "NULL" : "OK")}");
        _onLearnCallback = onLearn;
        PopulateInfo(data, state);

        gameObject.SetActive(true);
        _isVisible = true;

        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(Slide(true));
        Debug.Log("[Learn-DIAG] [Popup] Show 완료 — 슬라이드 애니메이션 시작");
    }

    public void Hide()
    {
        if (!_isVisible) return;
        _isVisible = false;

        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(Slide(false));
    }

    // ── Private helpers ────────────────────────────────────────────

    private void PopulateInfo(SkillData data, NewSkillTreeUI.NodeState state)
    {
        if (skillIconImage != null)
        {
            skillIconImage.sprite = data.skillIcon;
            skillIconImage.enabled = data.skillIcon != null;
        }

        if (skillNameText != null)  skillNameText.text  = data.skillName;
        if (skillTypeText != null)  skillTypeText.text  = $"{data.damageType} / {data.usageType}";
        if (descriptionText != null) descriptionText.text = data.description;

        // Stats
        if (damageText != null)
            damageText.text = data.minMult > 0
                ? $"배율 {data.minMult:F1}~{data.maxMult:F1}x"
                : "—";

        if (mpCostText != null)
            mpCostText.text = data.mpCost > 0 ? $"MP {data.mpCost}" : "MP 0";

        if (lpCostText != null)
            lpCostText.text = $"SP {data.requiredLorePoints}";

        if (hitCountText != null)
            hitCountText.text = data.hitCount > 1 ? $"{data.hitCount}타" : "단타";

        // Prerequisites
        bool hasPrereqs = data.prerequisiteSkills != null && data.prerequisiteSkills.Count > 0;
        if (prerequisitesRow != null)
            prerequisitesRow.SetActive(hasPrereqs);

        if (prerequisitesText != null && hasPrereqs)
        {
            var sb = new StringBuilder("필요: ");
            for (int i = 0; i < data.prerequisiteSkills.Count; i++)
            {
                if (data.prerequisiteSkills[i] == null) continue;
                if (i > 0) sb.Append(", ");
                sb.Append(data.prerequisiteSkills[i].skillName);
            }
            prerequisitesText.text = sb.ToString();
        }

        // Learn button
        SetLearnButton(state);
    }

    private void SetLearnButton(NewSkillTreeUI.NodeState state)
    {
        Debug.Log($"[Learn-DIAG] [Popup] SetLearnButton 진입 — state={state}, learnButton={(learnButton == null ? "NULL" : learnButton.name)}");
        if (learnButton == null)
        {
            Debug.LogError("[Learn-DIAG] [Popup] ✗ learnButton == NULL → Inspector에 Learn Button 연결 필요. 학습 불가");
            return;
        }

        bool learned   = state == NewSkillTreeUI.NodeState.Learned;
        bool available = state == NewSkillTreeUI.NodeState.Available;
        Debug.Log($"[Learn-DIAG] [Popup] state 분석: learned={learned}, available={available} → button interactable={available}");

        learnButton.interactable = available;

        if (learnButtonLabel != null)
            learnButtonLabel.text = learned ? "습득됨" : "습득";

        var colors = learnButton.colors;
        colors.normalColor   = learned ? AlreadyLearnedColor : available ? LearnableColor : UnlearnableColor;
        colors.disabledColor = learned ? AlreadyLearnedColor : UnlearnableColor;
        learnButton.colors   = colors;

        learnButton.onClick.RemoveAllListeners();
        if (available)
        {
            learnButton.onClick.AddListener(() =>
            {
                Debug.Log($"[Learn-DIAG] [Popup] *** Learn 버튼 onClick lambda 실행 *** callback={(_onLearnCallback == null ? "NULL" : "OK")}");
                _onLearnCallback?.Invoke();
                Debug.Log("[Learn-DIAG] [Popup] callback 호출 완료 → Hide() 호출");
                Hide();
            });
            Debug.Log("[Learn-DIAG] [Popup] ✓ Learn 버튼 onClick listener 등록됨 (available=true)");
        }
        else
        {
            Debug.LogWarning($"[Learn-DIAG] [Popup] ⚠ available=false → Learn 버튼 listener 등록 안 됨. 클릭해도 아무 동작 안 함. state={state}");
        }
    }

    private IEnumerator Slide(bool slideIn)
    {
        if (panelRect == null) yield break;

        if (_panelHeight <= 0)
            _panelHeight = panelRect.rect.height > 0 ? panelRect.rect.height : 500f;

        float startY = slideIn ? -_panelHeight : 0f;
        float endY   = slideIn ? 0f : -_panelHeight;
        float elapsed = 0f;

        while (elapsed < slideTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideTime);
            // Ease-out quad
            t = 1f - (1f - t) * (1f - t);
            panelRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, t));
            yield return null;
        }

        panelRect.anchoredPosition = new Vector2(0f, endY);

        if (!slideIn)
            gameObject.SetActive(false);
    }
}
