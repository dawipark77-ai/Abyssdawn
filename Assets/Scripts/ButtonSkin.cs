using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// [2026-10-17] 버튼 배경 통일 — 스킬 창 "닫기" 버튼과 같은 금테 패널(DungeonHud.PopupSprite, 9분할) + 금색 굵은 글자.
///  - Apply(button): 버튼 하나. 예전 꾸밈(자식 Border·Inner 사각형)은 끈다
///  - ApplyPlain(root): root 아래에서 "기본 흰 사각형 배경" 버튼만 골라 입힌다 (아이콘·스킬 카드·아이템 카드처럼 고유 그림이 있는 버튼은 그대로)
///  - 씬이 열릴 때 그 씬 전체에 ApplyPlain (SceneLabelsKo 다음). 코드로 만드는 창(마을·상점·확인창)은 만든 곳에서 부른다
/// </summary>
public static class ButtonSkin
{
    public static readonly Color Gold = new Color(0.86f, 0.72f, 0.42f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects()) ApplyPlain(root.transform);
    }

    /// <summary>버튼 하나에 금테 패널 배경을 입힌다.</summary>
    public static void Apply(Button b)
    {
        if (b == null) return;
        Sprite frame = DungeonHud.PopupSprite;
        if (frame == null) return;
        var img = b.targetGraphic as Image;
        if (img == null) img = b.GetComponent<Image>();
        if (img == null) return;
        img.sprite = frame;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 3f;
        img.color = Color.white;
        // 예전 꾸밈(테두리·안쪽 사각형)은 금테와 겹치므로 끈다
        foreach (Transform c in b.transform)
        {
            if (c.name != "Border" && c.name != "Inner") continue;
            var ci = c.GetComponent<Image>();
            if (ci != null) ci.enabled = false;
        }
        // 글자는 금색 굵게 (이미 색을 정한 강조 글자는 그대로 — 흰색·회색 계열만 바꿈)
        foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
        {
            Color c = t.color;
            bool plain = Mathf.Abs(c.r - c.g) < 0.12f && Mathf.Abs(c.g - c.b) < 0.12f; // 흰색·회색·검정
            if (plain) t.color = Gold;
            t.fontStyle |= FontStyles.Bold;
        }
    }

    /// <summary>root 아래에서 기본 사각형 배경(그림 없음·유니티 기본 UISprite)인 글자 버튼만 입힌다.</summary>
    public static void ApplyPlain(Transform root)
    {
        if (root == null) return;
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            var img = b.targetGraphic as Image;
            if (img == null) img = b.GetComponent<Image>();
            if (img == null) continue;
            if (!IsPlain(img.sprite)) continue;
            if (b.GetComponentInChildren<TMP_Text>(true) == null) continue; // 글자 없는 버튼(배경 막기 등)은 건드리지 않음
            if (img.color.a < 0.05f) continue;                               // 투명한 클릭 영역
            Apply(b);
        }
    }

    private static bool IsPlain(Sprite s)
    {
        if (s == null) return true;
        string n = s.name;
        return n == "UISprite" || n == "Background" || n == "InputFieldBackground" || n == "UIMask";
    }
}
