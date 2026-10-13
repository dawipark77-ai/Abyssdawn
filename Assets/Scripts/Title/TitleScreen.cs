using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// [2026-10-14] 타이틀 화면 (Claude Design ui_kits/title 을 옮김).
///   - "TAP TO BEGIN" 이 2.4초마다 천천히 밝아졌다 어두워진다 (투명도 0.55 ↔ 1).
///   - 화면 아무 데나 누르면 글자가 크림색으로 번쩍였다가 (0.5초) 던전 씬으로 넘어간다.
///   - 버전 글자는 Player Settings 의 버전을 보여 준다.
/// 다음 단계(세이브 선택 · 직업/성향/종의 기억 고르기)가 생기면 nextScene 만 바꾸면 된다.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    public TMP_Text tapText;
    public TMP_Text versionText;
    public string nextScene = "Abyssdawn_Dungeon_2D 08";

    [Header("TAP TO BEGIN 깜빡임")]
    public float pulsePeriod = 2.4f;
    public float pulseMin = 0.55f;

    private static readonly Color Gold = new Color(0.831f, 0.655f, 0.380f, 1f);  // #d4a761
    private static readonly Color Cream = new Color(0.953f, 0.906f, 0.784f, 1f); // #f3e7c8

    private bool _leaving;
    private float _t0;

    private void Start()
    {
        _t0 = Time.time;
        if (versionText != null) versionText.text = $"v {Application.version}";
    }

    private void Update()
    {
        if (tapText == null || _leaving) return;
        // ease-in-out 으로 0.55 → 1 → 0.55
        float p = ((Time.time - _t0) % pulsePeriod) / pulsePeriod;
        float k = 0.5f - 0.5f * Mathf.Cos(p * Mathf.PI * 2f);
        tapText.alpha = Mathf.Lerp(pulseMin, 1f, k);
    }

    /// <summary>화면 전체를 덮은 투명 버튼(TapArea)이 부른다.</summary>
    public void OnTap()
    {
        if (_leaving) return;
        _leaving = true;
        StartCoroutine(LeaveRoutine());
    }

    private IEnumerator LeaveRoutine()
    {
        // 누른 순간: 크림색 + 금빛 번짐 → 0.5초 동안 원래 금색으로
        if (tapText != null)
        {
            var mat = tapText.fontMaterial; // 이 글자만의 복사본
            bool hasGlow = mat.HasProperty(ShaderUtilities.ID_GlowColor);
            if (hasGlow) mat.EnableKeyword(ShaderUtilities.Keyword_Glow);
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.5f;
                tapText.color = Color.Lerp(Cream, Gold, k);
                tapText.alpha = 1f;
                if (hasGlow) mat.SetColor(ShaderUtilities.ID_GlowColor, new Color(Gold.r, Gold.g, Gold.b, 0.8f * (1f - k)));
                yield return null;
            }
            tapText.color = Gold;
        }
        EncounterTransition.LoadSceneWithFade(nextScene);
    }
}
