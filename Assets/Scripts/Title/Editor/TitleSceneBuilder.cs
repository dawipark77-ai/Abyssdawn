#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// [2026-10-14] 타이틀 씬 자동 생성 — Claude Design "Title Screen (Mobile)" (ui_kits/title/index.html, 430×932) 을 옮긴다.
/// 메뉴: Abyssdawn/Build Title Scene
///   - 글꼴(Cinzel · IM Fell English · JetBrains Mono, Google Fonts OFL) → TMP 글꼴 에셋
///   - 그림(Assets/Images/Title, bake_title.py 로 구움) → 스프라이트 설정
///   - 씬 Assets/Abyssdawn_Title.unity 를 새로 만든다 (있으면 덮어씀 — 손으로 고친 것은 사라짐)
/// 좌표는 모두 디자인 px (캔버스 기준 해상도 430×932, 높이 맞춤). 브라우저에서 잰 실제 위치를 그대로 씀.
/// </summary>
public static class TitleSceneBuilder
{
    private const string ScenePath = "Assets/Abyssdawn_Title.unity";
    private const string FontDir = "Assets/Fonts/Title";
    private const string MatDir = "Assets/Fonts/Title/Materials";
    private const string ImgDir = "Assets/Images/Title";
    private const string KoreanFallback = "Assets/Fonts/NotoSansKR SDF.asset";

    // 디자인 색
    private static readonly Color Gold = Hex("#d4a761");
    private static readonly Color Cream = Hex("#f3e7c8");
    private static readonly Color Bronze500 = Hex("#8a6a35");
    private static readonly Color Bronze400 = Hex("#b38a4c");
    private static readonly Color Parch300 = Hex("#c9b485");
    private static readonly Color Parch400 = Hex("#8f7b54");
    private static readonly Color QuoteCol = Hex("#a88f5f");
    private static readonly Color VersionCol = Hex("#6a5a3c");

    [MenuItem("Abyssdawn/Build Title Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SetupSprites();
        var cinzel = MakeFont("Cinzel-Regular");
        var cinzelSB = MakeFont("Cinzel-SemiBold");
        var cinzelXB = MakeFont("Cinzel-ExtraBold");
        var fellIt = MakeFont("IMFellEnglish-Italic");
        var fellSC = MakeFont("IMFellEnglishSC-Regular");
        var mono = MakeFont("JetBrainsMono-Regular");
        AddFallback(cinzel); AddFallback(cinzelSB);

        var matLogo = MakeMaterial("Title_Logo", cinzelXB, underlay: new Vector4(0f, -0.55f, 0.2f, 0f), glow: new Color(Gold.r, Gold.g, Gold.b, 0.35f));
        var matTap = MakeMaterial("Title_Tap", cinzelSB, underlay: new Vector4(0f, -0.6f, 0.1f, 0.6f), glow: new Color(Gold.r, Gold.g, Gold.b, 0f));
        var matSC = MakeMaterial("Title_SoftSC", fellSC, underlay: new Vector4(0f, -0.6f, 0.1f, 0.6f), glow: null);
        var matIt = MakeMaterial("Title_SoftItalic", fellIt, underlay: new Vector4(0f, -0.6f, 0.1f, 0.6f), glow: null);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라 (오버레이 캔버스만 쓰지만 '카메라 없음' 경고를 막고 바깥을 디자인 body 색으로)
        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#050403");
        cam.orthographic = true;

        var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(430f, 932f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // 세로 높이에 맞춤 — 9:16 이면 옆이 조금 더 보인다

        // 배경 (구운 그림 524×932: 가운데 430 이 디자인 영역) — 화면을 꽉 채움
        var bg = NewImage("Background", canvasGo.transform, Sprite("Title_BG"));
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(524f, 932f);
        var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 524f / 932f;
        bg.raycastTarget = false;

        // 디자인 영역 430×932 (가운데)
        var root = new GameObject("Design", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvasGo.transform, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(430f, 932f);

        // 불씨 (배경 위, 글자 아래)
        var embersRt = new GameObject("Embers", typeof(RectTransform)).GetComponent<RectTransform>();
        embersRt.SetParent(root, false);
        Stretch(embersRt);
        var embers = embersRt.gameObject.AddComponent<TitleEmbers>();
        embers.emberSprite = Sprite("Title_Ember");

        // ── 로고 묶음 ──
        // "d'Gaia Studio presents" + 양옆 선
        var lineL = NewImage("StudioLineL", root, Sprite("Title_LineFade")); Place(lineL.rectTransform, 50.6f, 196.5f, 26f, 1f); lineL.color = Bronze500;
        var lineR = NewImage("StudioLineR", root, Sprite("Title_LineFade")); Place(lineR.rectTransform, 313.5f, 196.5f, 26f, 1f); lineR.color = Bronze500;
        lineR.rectTransform.pivot = new Vector2(0.5f, 1f); // 가운데를 축으로 좌우 뒤집기 (색 → 투명)
        lineR.rectTransform.anchoredPosition = new Vector2(313.5f + 13f, -196.5f);
        lineR.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        var studio = NewText("Studio", root, 0f, 190f, 430f, 14f, "d'Gaia Studio presents", fellSC, matSC, 11f, WithA(Gold, 0.85f), 56f);

        var crown = NewImage("Crown", root, Sprite("Title_Crown"));
        Place(crown.rectTransform, 215f - 84f, 204f - 40f, 168f, 80f);

        var abyss = NewText("Abyss", root, -2.16f, 226f, 430f, 66.2f, "ABYSS", cinzelXB, matLogo, 72f, Cream, 6f);

        var orn = new GameObject("Ornament", typeof(RectTransform)).GetComponent<RectTransform>();
        orn.SetParent(root, false); Stretch(orn);
        var oL = NewImage("LineL", orn, Sprite("Title_LineCenter")); Place(oL.rectTransform, 90f, 302.7f, 86f, 1f); oL.color = Bronze400;
        var oR = NewImage("LineR", orn, Sprite("Title_LineCenter")); Place(oR.rectTransform, 254f, 302.7f, 86f, 1f); oR.color = Bronze400;
        Diamond(orn, "DiaSmL", 193f, 303.2f, 6f, 0.6f);
        Diamond(orn, "Dia", 215f, 303.2f, 10f, 1f);
        Diamond(orn, "DiaSmR", 237f, 303.2f, 6f, 0.6f);

        var dawn = NewText("Dawn", root, -2.16f, 318.2f, 430f, 66.2f, "DAWN", cinzelXB, matLogo, 72f, Color.white, 6f);
        dawn.enableVertexGradient = true;
        // CSS: #f3e7c8 → #d4a761(55%) → #8a6a35 (글줄 높이 기준). TMP 는 글자 칸 기준이라 아래를 조금 더 어둡게
        var dawnTop = Hex("#efdcb0"); var dawnBottom = Hex("#6e5226");
        dawn.colorGradient = new VertexGradient(dawnTop, dawnTop, dawnBottom, dawnBottom);

        NewText("Tagline", root, 0f, 392.5f, 430f, 15f, "A TURN—BASED DESCENT", fellSC, matSC, 12f, WithA(Parch300, 0.9f), 36f);

        // 인용구
        NewText("Quote", root, 40f, 620f, 350f, 24.8f, "\"The lantern gutters. Still, the stair goes down.\"", fellIt, matIt, 16f, QuoteCol, 0f);

        // TAP TO BEGIN
        var tap = NewText("TapToBegin", root, -2.86f, 734f, 430f, 18f, "TAP TO BEGIN", cinzelSB, matTap, 13f, Gold, 44f);
        NewText("Chapter", root, -0.33f, 758f, 430f, 14f, "— Chapter I · The Chalice's Oath —", fellIt, matIt, 11f, Parch400, 6f);

        // 아래: 언어 표시 + 버전
        var pill = NewImage("LangPill", root, Sprite("Title_LangPill"));
        Place(pill.rectTransform, 28f - 8f, 852f - 8f, 110f, 48f);
        pill.raycastTarget = false;
        var lang = NewText("LangText", root, 40f, 852f, 70.2f, 32f, "KR · <color=#D4A761><b>한국어</b></color>", cinzel, null, 10f, Parch300, 24f);
        lang.alignment = TextAlignmentOptions.Left;
        lang.enableAutoSizing = true; lang.fontSizeMin = 7f; lang.fontSizeMax = 10f; // "EN · ENGLISH" 도 테 안에
        var version = NewText("Version", root, 272.8f, 861.5f, 128.4f, 13f, "v 0.7.2 · build 314", mono, null, 10f, VersionCol, 8f);
        version.alignment = TextAlignmentOptions.Right;

        // [2026-10-14] 누르는 곳은 두 군데만 — "TAP TO BEGIN" 글자와 언어 버튼. 화면 다른 곳은 눌러도 아무 일 없음.
        var tapBtn = HitButton("TapButton", root, 215f - 110f, 743f - 22f, 220f, 44f);
        var langBtn = HitButton("LangButton", root, 28f, 852f, 94.2f, 32f);

        var ctl = canvasGo.AddComponent<TitleScreen>();
        ctl.tapText = tap;
        ctl.versionText = version;
        ctl.langText = lang;
        UnityEventTools.AddPersistentListener(tapBtn.onClick, ctl.OnTap);
        UnityEventTools.AddPersistentListener(langBtn.onClick, ctl.OnLanguage);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath, false); // 맨 앞(0번)에 두면 BattleManager 의 LoadScene(0) 예비 경로가 타이틀로 가므로 일단 뒤에
        AddToBuildSettings("Assets/Abyssdawn_Dungeon_2D 08.unity", false);
        Debug.Log("[TitleSceneBuilder] 타이틀 씬 생성 완료: " + ScenePath);
    }

    // ───────────────────────── 모험의 서 · 새 게임 씬 ─────────────────────────
    private const string ProloguePath = "Assets/Abyssdawn_Prologue.unity";

    /// <summary>[2026-10-14] 타이틀 다음 화면 (드퀘식 모험의 서 + 캐릭터 만들기). 화면은 PrologueScreen 이 실행 때 만든다.</summary>
    [MenuItem("Abyssdawn/Build Prologue Scene")]
    public static void BuildPrologue()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#050403");
        cam.orthographic = true;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var go = new GameObject("Prologue");
        var p = go.AddComponent<PrologueScreen>();
        p.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFallback);
        p.headingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/Cinzel-SemiBold SDF.asset");
        p.background = Sprite("Title_BG");
        p.frame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/PopupFrameDark.png"); // [2026-10-14] 드퀘처럼 안쪽은 거의 검정 (금테·모서리 장식만 남김)

        // 종의 기억: 인간 · 엘프 · 오크 각 5가지 ([2026-10-14] 엘프·오크 추가 — 화면에서 종족 탭)
        p.memoryChoices.Clear();
        foreach (var sp in new[] { "Human", "Elf", "Orc" })
            foreach (var g in AssetDatabase.FindAssets("t:MemoryOfSpeciesData", new[] { "Assets/Scripts/Battle/Data/MemoryofSpecies/" + sp }))
            {
                var m = AssetDatabase.LoadAssetAtPath<AbyssdawnBattle.MemoryOfSpeciesData>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null) p.memoryChoices.Add(m);
            }
        // 직업: 지금은 전사만
        p.jobChoices.Clear();
        // [2026-10-14] 전사 · 도적 · 몽크
        foreach (var jn in new[] { "Class_Warrior", "Class_Thief", "Class_Monk" })
        {
            var job = AssetDatabase.LoadAssetAtPath<CharacterClass>($"Assets/Resources/Classes/{jn}.asset");
            if (job != null) p.jobChoices.Add(job);
        }
        p.heroTemplate = AssetDatabase.LoadAssetAtPath<PlayerStatData>("Assets/HeroData.asset"); // 최종 상태창 계산용 (복사본만 씀)
        // 성향: Resources/Dispositions 에 에셋이 생기면 자동으로 (지금은 없음 → "준비 중")
        p.dispositionChoices.Clear();
        foreach (var d in Resources.LoadAll<DispositionData>("Dispositions")) p.dispositionChoices.Add(d);
        // [2026-10-14] 기획 순서대로 (탐험가 · 호기심 · 구두쇠 · 탐욕 · 완벽주의자 · 겁쟁이 · 무모함 · 신중함 · 냉혈 · 고독한 늑대)
        string[] order = { "Explorer", "Curious", "Miser", "Greedy", "Perfectionist", "Coward", "Reckless", "Cautious", "ColdBlooded", "LoneWolf" };
        p.dispositionChoices.Sort((x, y) => System.Array.IndexOf(order, x.name.Replace("Disposition_", "")).CompareTo(System.Array.IndexOf(order, y.name.Replace("Disposition_", ""))));

        EditorSceneManager.SaveScene(scene, ProloguePath);
        AddToBuildSettings(ProloguePath, false);
        Debug.Log($"[TitleSceneBuilder] 모험의 서 씬 생성 완료: {ProloguePath} (기억 {p.memoryChoices.Count}, 직업 {p.jobChoices.Count}, 성향 {p.dispositionChoices.Count})");
    }

    // ───────────────────────── 도우미 ─────────────────────────

    private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    private static Color WithA(Color c, float a) { c.a = a; return c; }

    private static void SetupSprites()
    {
        foreach (var name in new[] { "Title_BG", "Title_Crown", "Title_Diamond", "Title_Ember", "Title_LangPill", "Title_LineCenter", "Title_LineFade" })
        {
            var path = $"{ImgDir}/{name}.png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning("[TitleSceneBuilder] 그림 없음: " + path); continue; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }

    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ImgDir}/{name}.png");

    private static TMP_FontAsset MakeFont(string file)
    {
        string path = $"{FontDir}/{file} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;
        var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{file}.ttf");
        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 12, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = file + " SDF";
        AssetDatabase.CreateAsset(fa, path);
        fa.atlasTextures[0].name = file + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = file + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    private static void AddFallback(TMP_FontAsset fa)
    {
        var kr = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFallback);
        if (fa == null || kr == null) return;
        if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (!fa.fallbackFontAssetTable.Contains(kr)) fa.fallbackFontAssetTable.Add(kr);
        EditorUtility.SetDirty(fa);
    }

    /// <param name="underlay">x,y = 그림자 위치, z = 두께, w = 번짐</param>
    private static Material MakeMaterial(string name, TMP_FontAsset fa, Vector4? underlay, Color? glow)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(FontDir, "Materials");
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(fa.material); AssetDatabase.CreateAsset(mat, path); }
        else mat.CopyPropertiesFromMaterial(fa.material);
        mat.name = name;
        mat.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
        if (underlay.HasValue)
        {
            var u = underlay.Value;
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.92f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, u.x);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, u.y);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, u.z);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, u.w);
        }
        if (glow.HasValue)
        {
            mat.EnableKeyword(ShaderUtilities.Keyword_Glow);
            mat.SetColor(ShaderUtilities.ID_GlowColor, glow.Value);
            mat.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
            mat.SetFloat(ShaderUtilities.ID_GlowInner, 0.05f);
            mat.SetFloat(ShaderUtilities.ID_GlowOuter, 0.6f);
            mat.SetFloat(ShaderUtilities.ID_GlowPower, 0.6f);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>디자인 좌표(왼쪽 위 기준 x, y, 너비, 높이)로 놓기.</summary>
    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, float x, float y, float w, float h, string text,
        TMP_FontAsset font, Material mat, float size, Color color, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        Place(t.rectTransform, x, y, w, h);
        t.font = font;
        if (mat != null) t.fontSharedMaterial = mat;
        t.fontSize = size;
        t.color = color;
        t.characterSpacing = spacing;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.richText = true;
        t.fontFeatures.Clear(); // 커닝 끔 — Cinzel 의 커닝 값이 TMP 에서 너무 세게 먹어 글자 간격이 들쭉날쭉해짐
        t.text = text;
        return t;
    }

    /// <summary>보이지 않는 누름 칸 (투명 그림 + 버튼). 글자나 그림 위에 겹쳐 둔다.</summary>
    private static Button HitButton(string name, Transform parent, float x, float y, float w, float h)
    {
        var img = NewImage(name, parent, null);
        Place(img.rectTransform, x, y, w, h);
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true; // 투명해도 누름은 받는다
        var btn = img.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        return btn;
    }

    private static void Diamond(Transform parent, string name, float cx, float cy, float size, float alpha)
    {
        // 그림 34px 안에 10px 마름모 (둘레는 번짐)
        float s = 34f * size / 10f;
        var img = NewImage(name, parent, Sprite("Title_Diamond"));
        Place(img.rectTransform, cx - s / 2f, cy - s / 2f, s, s);
        img.color = new Color(1f, 1f, 1f, alpha);
    }

    private static void AddToBuildSettings(string path, bool first)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == path)) return;
        var entry = new EditorBuildSettingsScene(path, true);
        if (first) list.Insert(0, entry); else list.Add(entry);
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
