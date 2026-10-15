using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-15] 상태 카드 연출 — 던전 파티 카드와 전투 상태 카드 공용.
///  - Flash: 카드 위에 잠깐 빛이 번졌다 사라짐 (아이템을 썼다는 표시)
///  - Number: 카드 위로 숫자가 떠올랐다 사라짐 (회복 +HP 초록, +MP 파랑)
///  - ItemUsed: 소비 아이템 결과에 맞춰 위 둘을 한 번에
/// 연출 코루틴은 숨은 러너 오브젝트가 돌린다 (카드가 꺼져도 안전).
/// </summary>
public static class CardFx
{
    public static readonly Color HealColor = new Color(0.35f, 1f, 0.45f);
    public static readonly Color ManaColor = new Color(0.45f, 0.75f, 1f);
    public static readonly Color UseFlashColor = new Color(1f, 0.92f, 0.65f, 0.55f);

    private class Runner : MonoBehaviour { }
    private static Runner _runner;
    private static Runner Get()
    {
        if (_runner == null)
        {
            var go = new GameObject("[CardFx]");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }
        return _runner;
    }

    /// <summary>주인공(또는 전투 중 아군) 카드. 전투면 전투 상태 카드, 아니면 던전 파티 카드.</summary>
    public static RectTransform CardOf(PlayerStats p)
    {
        if (p == null) return null;
        var bm = Object.FindFirstObjectByType<BattleManager>();
        if (bm != null) return bm.GetPlayerCardRect(p);
        if (!p.IsRecruitedCompanion && DungeonPartyBar.Instance != null) return DungeonPartyBar.Instance.HeroCardRect();
        return null;
    }

    public static void Flash(RectTransform card, Color color, float seconds = 0.55f)
    {
        if (card == null || !card.gameObject.activeInHierarchy) return;
        var go = new GameObject("CardFlash", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(card, false);
        go.transform.SetAsLastSibling();
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(-6f, -6f); rt.offsetMax = new Vector2(6f, 6f);
        var img = go.GetComponent<Image>(); img.raycastTarget = false; img.color = color;
        Get().StartCoroutine(FlashRoutine(img, color, seconds));
    }

    private static IEnumerator FlashRoutine(Image img, Color c, float seconds)
    {
        for (float t = 0f; t < seconds && img != null; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            float a = k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f; // 빠르게 밝아졌다 천천히 사라짐
            img.color = new Color(c.r, c.g, c.b, c.a * a);
            yield return null;
        }
        if (img != null) Object.Destroy(img.gameObject);
    }

    public static void Number(RectTransform card, string text, Color color, float yOffset = 0f)
    {
        if (card == null || !card.gameObject.activeInHierarchy) return;
        var go = new GameObject("CardNumber", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(card, false);
        go.transform.SetAsLastSibling();
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 90f); rt.anchoredPosition = new Vector2(0f, yOffset);
        var t = go.GetComponent<TextMeshProUGUI>();
        var any = card.GetComponentInChildren<TextMeshProUGUI>(true);
        if (any != null && any != t) t.font = any.font;
        t.text = text; t.color = color; t.fontSize = 54f; t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.outlineWidth = 0.25f; t.outlineColor = new Color32(0, 0, 0, 255);
        Get().StartCoroutine(NumberRoutine(t, rt));
    }

    private static IEnumerator NumberRoutine(TextMeshProUGUI t, RectTransform rt)
    {
        Vector2 start = rt.anchoredPosition;
        const float seconds = 1.1f;
        for (float e = 0f; e < seconds && t != null; e += Time.unscaledDeltaTime)
        {
            float k = e / seconds;
            rt.anchoredPosition = start + new Vector2(0f, 70f * (1f - (1f - k) * (1f - k)));
            t.alpha = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            yield return null;
        }
        if (t != null) Object.Destroy(t.gameObject);
    }

    // ── 자리 이동 연출: 카드가 예전 자리에서 새 자리로 미끄러져 들어온다 (밀치기·당기기·위치 이동) ──
    public static void SlideFrom(RectTransform card, Vector3 fromWorld, float seconds = 0.35f)
    {
        if (card == null || !card.gameObject.activeInHierarchy) return;
        Get().StartCoroutine(SlideRoutine(card, fromWorld, seconds));
    }

    private static IEnumerator SlideRoutine(RectTransform card, Vector3 fromWorld, float seconds)
    {
        Vector3 home = card.localPosition;
        Vector3 fromLocal = card.parent != null ? card.parent.InverseTransformPoint(fromWorld) : fromWorld;
        fromLocal.z = home.z;
        for (float t = 0f; t < seconds && card != null; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            float e = 1f - (1f - k) * (1f - k) * (1f - k); // 빠르게 출발해서 부드럽게 멈춤
            card.localPosition = Vector3.Lerp(fromLocal, home, e);
            yield return null;
        }
        if (card != null) card.localPosition = home;
    }

    // ── 차례 표시: 지금 행동하는(명령을 고르는) 카드 테두리가 금색으로 숨 쉬듯 반짝 ──
    private static GameObject _turnGlow;
    private static Coroutine _turnGlowRoutine;
    public static readonly Color TurnGlowColor = new Color(1f, 0.85f, 0.4f, 1f);

    /// <summary>card 에 차례 테두리 (null 이면 끔). 한 번에 하나만.</summary>
    public static void SetTurnGlow(RectTransform card)
    {
        if (_turnGlow != null && card != null && _turnGlow.transform.parent == card) return;
        if (_turnGlow != null) Object.Destroy(_turnGlow);
        _turnGlow = null;
        if (card == null || !card.gameObject.activeInHierarchy) return;
        _turnGlow = new GameObject("TurnGlow", typeof(RectTransform));
        _turnGlow.transform.SetParent(card, false);
        _turnGlow.transform.SetAsLastSibling();
        var rt = (RectTransform)_turnGlow.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(-5f, -5f); rt.offsetMax = new Vector2(5f, 5f);
        var edges = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var e = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            e.transform.SetParent(rt, false);
            var ert = (RectTransform)e.transform;
            const float w = 5f;
            switch (i)
            {
                case 0: ert.anchorMin = new Vector2(0f, 1f); ert.anchorMax = Vector2.one; ert.pivot = new Vector2(0.5f, 1f); ert.sizeDelta = new Vector2(0f, w); break;
                case 1: ert.anchorMin = Vector2.zero; ert.anchorMax = new Vector2(1f, 0f); ert.pivot = new Vector2(0.5f, 0f); ert.sizeDelta = new Vector2(0f, w); break;
                case 2: ert.anchorMin = Vector2.zero; ert.anchorMax = new Vector2(0f, 1f); ert.pivot = new Vector2(0f, 0.5f); ert.sizeDelta = new Vector2(w, 0f); break;
                default: ert.anchorMin = new Vector2(1f, 0f); ert.anchorMax = Vector2.one; ert.pivot = new Vector2(1f, 0.5f); ert.sizeDelta = new Vector2(w, 0f); break;
            }
            ert.anchoredPosition = Vector2.zero;
            edges[i] = e.GetComponent<Image>(); edges[i].raycastTarget = false; edges[i].color = TurnGlowColor;
        }
        if (_turnGlowRoutine != null) Get().StopCoroutine(_turnGlowRoutine);
        _turnGlowRoutine = Get().StartCoroutine(TurnGlowRoutine(_turnGlow, edges));
    }

    private static IEnumerator TurnGlowRoutine(GameObject glow, Image[] edges)
    {
        while (glow != null)
        {
            float a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
            foreach (var e in edges) if (e != null) e.color = new Color(TurnGlowColor.r, TurnGlowColor.g, TurnGlowColor.b, a);
            yield return null;
        }
    }

    /// <summary>소비 아이템 사용 결과 연출. 전투 중 HP 회복 숫자는 BattleFx 가 이미 띄우므로 여기선 MP·반짝임만.</summary>
    public static void ItemUsed(PlayerStats user, ConsumableEffectApplier.EffectResult r)
    {
        RectTransform card = CardOf(user);
        if (card == null) return;
        Flash(card, UseFlashColor);
        bool inBattle = Object.FindFirstObjectByType<BattleManager>() != null;
        if (r.hpHealed > 0 && !inBattle) Number(card, $"+{r.hpHealed}", HealColor, 10f);
        if (r.mpHealed > 0) Number(card, $"+{r.mpHealed} MP", ManaColor, r.hpHealed > 0 && !inBattle ? -40f : 0f);
    }
}
