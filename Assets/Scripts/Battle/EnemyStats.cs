using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using AbyssdawnBattle;
using Abyssdawn;

/// <summary>
/// 무기(물리) 상태이상 인스턴스 — StatusEffectSO 기반 추적용
/// </summary>
[System.Serializable]
public class StatusEffectInstance
{
    public StatusEffectSO data;
    public int remainingTurns;
    /// <summary>
    /// 적용된 턴에는 DoT/차감을 건너뜁니다. 다음 턴부터 정상 처리합니다.
    /// </summary>
    public bool appliedThisTurn = true;

    public StatusEffectInstance(StatusEffectSO effectData)
    {
        data = effectData;
        remainingTurns = effectData.physicalDuration;
    }
}

public class EnemyStats : MonoBehaviour
{
    [Header("Base Stats")]
    public string enemyName = "";
    public int maxHP = 0;
    public int currentHP;
    public int maxMP = 0;
    public int currentMP;
    public int attack = 0;
    public int defense = 0;
    public int magic = 0;
    public int Agility = 0;
    public int luck = 0;

    [Header("Battle Position")]
    [Tooltip("현재 슬롯 위치 (BattleLine에서 자동 설정됨). 슬롯 1,2 = 전열, 슬롯 3,4 = 후열.")]
    public BattleSlot currentSlot = BattleSlot.Slot1;

    [Tooltip("이 적이 배치될 수 있는 슬롯 범위. Inspector에서 직접 지정.")]
    public SlotMask allowedSlots = SlotMask.Any;

    public bool IsFrontRow => SlotHelper.IsFrontRow(currentSlot);
    public bool IsBackRow => SlotHelper.IsBackRow(currentSlot);

    [Header("Visual Effects")]
    public float hitShakeDuration = 0.2f;
    public float hitShakeMagnitude = 0.1f;
    
    [Header("Status Effect System (상태이상 시스템)")]
    [Tooltip("현재 걸린 상태이상 리스트 (런타임에서 자동 관리)")]
    public List<StatusEffectInstance> activeStatusEffects = new List<StatusEffectInstance>();

    // Legacy compatibility
    public bool isIgnited => HasStatusEffect(StatusEffectType.Ignite);
    public int igniteTurnsRemaining => GetStatusEffectRemainingTurns(StatusEffectType.Ignite);

    // 런타임 UI 참조
    private Image monsterImage;
    private TextMeshProUGUI nameText;

    private bool isDead = false;
    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;
    private SpriteRenderer spriteRenderer;
    private GameObject statusUI;
    private Canvas statusCanvas;
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI mpText;
    private bool usesEmbeddedStatusUI = false;
    private bool runtimeGeneratedStatusUI = false;
    private Material originalMaterial;
    private Material highlightMaterial;
    private bool isHighlighted = false;

    private static readonly Vector2 DEFAULT_STATUS_UI_SIZE = new Vector2(120f, 50f);
    private const float DEFAULT_STATUS_UI_SCALE = 0.005f;

    // 상태이상 아이콘 패널
    private GameObject statusIconPanel;
    private readonly List<GameObject> statusIconObjects = new List<GameObject>();
    private const float DEFAULT_STATUS_UI_OFFSET_Y = 1.0f;

    [Header("Status UI Settings")]
    [Tooltip("Check to manually override UI size/scale/offset in Inspector")]
    public bool useCustomStatusUISettings = false;

    [Tooltip("끄면 월드 위에 뜨는 HP/MP UI를 생성하지 않습니다.")]
    public bool enableWorldSpaceStatusUI = true;

    [Tooltip("World-space UI width/height (pixels before scaling)")]
    public Vector2 statusUISize = DEFAULT_STATUS_UI_SIZE;
    [Tooltip("World-space scale applied to the status UI canvas")]
    public float statusUIScale = DEFAULT_STATUS_UI_SCALE;
    [Tooltip("Vertical offset from enemy position to place the UI")]
    public float statusUIOffsetY = DEFAULT_STATUS_UI_OFFSET_Y;
    
    // statusUI 접근용 프로퍼티
    public GameObject StatusUI { get { return statusUI; } }

    [Header("Level Info")]
    public int level = 1;
    public int expReward = 10;

    void Awake()
    {
        // 씬 시작 시 최대 HP/MP로 초기화
        currentHP = maxHP;
        currentMP = maxMP;
        originalPosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();

        ApplyDefaultStatusUISettingsIfNeeded();
        TryBindEmbeddedStatusUI();
    }

    /// <summary>
    /// MonsterSO 데이터로 스탯을 초기화합니다.
    /// ConfigureStatsByName()은 호출하지 않으며, SO 데이터가 모든 기본값을 덮어씁니다.
    /// </summary>
    public void Init(MonsterSO so, GameObject uiPanel = null)
    {
        if (so == null)
        {
            Debug.LogWarning("[EnemyStats] Init() called with null MonsterSO.");
            return;
        }

        enemyName    = so.MonsterName;
        maxHP        = so.HP;
        currentHP    = so.HP;
        maxMP        = so.MP;
        currentMP    = so.MP;
        attack       = so.ATK;
        defense      = so.DEF;
        magic        = so.MAG;
        Agility      = so.AGI;
        luck         = so.LUK;
        allowedSlots = so.AllowedSlots;
        expReward    = so.ExpReward;

        // 스프라이트는 BattleManager에서 selectedSprites[i]로 직접 주입 — 여기서 건드리지 않음
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        // 박스 콜라이더 크기를 스프라이트에 맞게 자동 설정 (BattleManager가 sr.sprite 주입 후 적용됨)
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null && spriteRenderer != null && spriteRenderer.sprite != null)
        {
            col.size = spriteRenderer.sprite.bounds.size;
        }

        // 프리팹 내부에 부착된 UI를 우선 사용
        TryBindEmbeddedStatusUI();

        // 내장 UI가 없을 때만 외부 패널 연결(레거시 호환)
        if (!usesEmbeddedStatusUI && uiPanel != null)
        {
            TextMeshProUGUI[] texts = uiPanel.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var t in texts)
            {
                if (t.name == "Nametext" || t.name == "NameText") nameText = t;
                if (t.name == "HPText")   hpText   = t;
                if (t.name == "MPText")   mpText   = t;
            }
            statusUI = uiPanel;
            statusCanvas = uiPanel.GetComponentInChildren<Canvas>(true);
        }

        UpdateStatusUI();
        RefreshStatusIcons();
        Debug.Log($"[ENEMY_STATS] Init 완료: {enemyName}, HP:{maxHP}");
    }

    private void TryBindEmbeddedStatusUI()
    {
        Transform uiRoot = FindNamedDescendantRecursive(transform, "EnemyUIPrefab");
        if (uiRoot == null)
        {
            usesEmbeddedStatusUI = false;
            return;
        }

        statusUI = uiRoot.gameObject;
        statusCanvas = uiRoot.GetComponent<Canvas>();
        if (statusCanvas != null && statusCanvas.renderMode == RenderMode.WorldSpace && Camera.main != null)
        {
            statusCanvas.worldCamera = Camera.main;
        }

        Transform nameTransform = FindNamedDescendantRecursive(uiRoot, "NameText")
            ?? FindNamedDescendantRecursive(uiRoot, "Nametext");
        if (nameTransform != null) nameText = nameTransform.GetComponent<TextMeshProUGUI>();

        Transform hpTransform = FindNamedDescendantRecursive(uiRoot, "HPText");
        if (hpTransform != null) hpText = hpTransform.GetComponent<TextMeshProUGUI>();

        Transform mpTransform = FindNamedDescendantRecursive(uiRoot, "MPText");
        if (mpTransform != null) mpText = mpTransform.GetComponent<TextMeshProUGUI>();

        Transform statusIconTransform = FindNamedDescendantRecursive(uiRoot, "StatusIconRow");
        if (statusIconTransform != null) statusIconPanel = statusIconTransform.gameObject;

        usesEmbeddedStatusUI = statusUI != null;
    }

    private Transform FindNamedDescendantRecursive(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;

        foreach (Transform child in root)
        {
            Transform found = FindNamedDescendantRecursive(child, targetName);
            if (found != null) return found;
        }

        return null;
    }



    private void ConfigureStatsByName()
    {
        // 이름에 "(Clone)" 등이 붙을 수 있으므로 Contains로 확인
        if (enemyName.Contains("Goblin"))
        {
            level = 5;
            expReward = 15;
            // 고블린은 약하니까 HP/공격력도 좀 낮춰줄까요? 하라는 말은 없었으니 레벨만 설정.
        }
        else if (enemyName.Contains("Wizard"))
        {
            level = 7;
            expReward = 20;
        }
        else if (enemyName.Contains("Slime"))
        {
            level = 10;
            expReward = 30;
        }
        else if (enemyName.Contains("Orc"))
        {
            level = 15;
            expReward = 50;
        }
        else
        {
            // 기본값
            level = 1;
            expReward = 10;
        }
    }

    void Start()
    {
        // 최종 스폰 위치를 원본 위치로 저장
        originalPosition = transform.position;
        
        // World Space UI 생성 (약간의 지연을 두고 생성하여 위치가 확정된 후 생성)
        if (enableWorldSpaceStatusUI)
        {
            StartCoroutine(DelayedCreateWorldSpaceUI());
        }
    }

    private void ApplyDefaultStatusUISettingsIfNeeded()
    {
        if (!useCustomStatusUISettings)
        {
            statusUISize = DEFAULT_STATUS_UI_SIZE;
            statusUIScale = DEFAULT_STATUS_UI_SCALE;
            statusUIOffsetY = DEFAULT_STATUS_UI_OFFSET_Y;
        }
    }
    
    // UI 생성을 지연시키는 코루틴
    IEnumerator DelayedCreateWorldSpaceUI()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f); // 위치가 확정될 때까지 대기
        
        // originalPosition이 제대로 설정되었는지 확인
        if (originalPosition == Vector3.zero)
        {
            originalPosition = transform.position;
        }
        
        // CreateWorldSpaceUI();
    }

    // originalPosition 설정 (외부에서 호출 가능)
    public void SetOriginalPosition(Vector3 pos)
    {
        originalPosition = pos;
        // UI 위치도 업데이트
        if (statusCanvas != null && Camera.main != null)
        {
            statusCanvas.transform.position = originalPosition + Vector3.up * statusUIOffsetY;
            statusCanvas.transform.LookAt(Camera.main.transform);
            statusCanvas.transform.Rotate(0, 180, 0);
        }
        
        // UI가 아직 생성되지 않았으면 생성
        // if (enableWorldSpaceStatusUI && statusUI == null && Camera.main != null)
        // {
        //     CreateWorldSpaceUI();
        // }
    }

    // 데미지 처리
    public int TakeDamage(int damage, bool isCritical = false)
    {
        if (isDead) return 0;

        int appliedDamage = Mathf.Min(damage, currentHP);
        currentHP -= damage;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        UpdateStatusUI();

        Debug.Log($"{enemyName} took {damage} damage. HP: {currentHP}/{maxHP}");

        if (currentHP <= 0)
        {
            HandleDeath();
        }
        else
        {
            PlayHitShake(isCritical);
        }

        return appliedDamage;
    }

    // 회복
    public void Heal(int amount)
    {
        if (isDead) return;
        
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        UpdateStatusUI();

        Debug.Log($"{enemyName} healed {amount}. HP: {currentHP}/{maxHP}");
    }

    // 죽음 체크
    public bool IsDead()
    {
        return isDead || currentHP <= 0;
    }

    // 타격 시 흔들림 효과
    private void PlayHitShake(bool isCritical = false)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            transform.position = originalPosition;
        }
        shakeCoroutine = StartCoroutine(HitShakeRoutine(isCritical));
    }

    private IEnumerator HitShakeRoutine(bool isCritical = false)
    {
        // 크리티컬 여부에 따라 흔들림 강도와 지속시간 조정
        float duration = isCritical ? 0.35f : 0.15f;  // 크리티컬: 0.35초, 일반: 0.15초
        float magnitude = isCritical ? 0.25f : 0.05f; // 크리티컬: 0.25, 일반: 0.05 (얕게)
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float offsetX = UnityEngine.Random.Range(-magnitude, magnitude);
            float offsetY = UnityEngine.Random.Range(-magnitude, magnitude);
            transform.position = originalPosition + new Vector3(offsetX, offsetY, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPosition;
        shakeCoroutine = null;
    }

    // ─────────── StatusEffectSO 기반 상태이상 ───────────

    /// <summary>
    /// 무기 물리 공격으로 상태이상을 부여합니다.
    /// physicalApplyChance 확률 체크 후 중복이면 지속 턴 갱신, 신규면 추가합니다.
    /// </summary>
    public bool ApplyStatusEffect(StatusEffectSO effect)
    {
        if (effect == null) return false;

        if (UnityEngine.Random.value > effect.physicalApplyChance) return false;

        return ApplyStatusEffectDirect(effect, effect.physicalDuration);
    }

    /// <summary>
    /// 확률 재롤 없이 상태이상을 직접 적용합니다 (스킬에서 이미 확률을 굴린 경우).
    /// duration: 0이면 damageType에 따라 physicalDuration/magicalDuration 자동 선택.
    /// </summary>
    public bool ApplyStatusEffectDirect(StatusEffectSO effect, int duration = 0)
    {
        if (effect == null) return false;

        int turns = duration > 0 ? duration : effect.physicalDuration;

        StatusEffectInstance existing = activeStatusEffects.Find(e => e.data.effectType == effect.effectType);
        if (existing != null)
        {
            existing.remainingTurns = Mathf.Max(existing.remainingTurns, turns);
            Debug.Log($"[StatusEffect] {enemyName} {effect.effectType} duration refreshed: {existing.remainingTurns} turns");
        }
        else
        {
            var instance = new StatusEffectInstance(effect);
            instance.remainingTurns = turns;
            activeStatusEffects.Add(instance);
            Debug.Log($"[StatusEffect] {enemyName} afflicted with {effect.effectType}! ({turns} turns)");
            RefreshStatusIcons();
        }
        return true;
    }

    /// <summary>
    /// 현재 activeStatusEffects 기반으로 아이콘 패널을 다시 그립니다.
    /// </summary>
    private void RefreshStatusIcons()
    {
        if (statusIconPanel == null) return;

        // 기존 아이콘 즉시 제거
        foreach (var obj in statusIconObjects)
        {
            if (obj == null) continue;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
        statusIconObjects.Clear();

        const float iconSize = 16f;
        const float spacing = 2f;
        int count = 0;

        foreach (var se in activeStatusEffects)
        {
            if (se?.data == null) continue;
            Sprite icon = se.data.flatIcon ?? se.data.itemIcon;
            if (icon == null) continue;

            GameObject iconObj = new GameObject($"Icon_{se.data.effectType}",
                typeof(RectTransform), typeof(UnityEngine.UI.Image));
            iconObj.transform.SetParent(statusIconPanel.transform, false);

            RectTransform rt = iconObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(iconSize, iconSize);
            rt.anchoredPosition = new Vector2((iconSize + spacing) * count + 2f, 2f);

            UnityEngine.UI.Image img = iconObj.GetComponent<UnityEngine.UI.Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            statusIconObjects.Add(iconObj);
            count++;
        }

        UpdateStatusUI();
        Debug.Log($"[StatusIcon] {enemyName} icons refreshed: {count} active effects");
    }

    /// <summary>
    /// 턴 종료 시 상태이상 DoT 처리 및 턴 감소
    /// </summary>
    public void ProcessStatusEffectsEndOfTurn()
    {
        if (activeStatusEffects.Count == 0 || currentHP <= 0 || isDead) return;

        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            StatusEffectInstance se = activeStatusEffects[i];

            // 적용된 턴에는 DoT/차감 없이 플래그만 해제
            if (se.appliedThisTurn)
            {
                se.appliedThisTurn = false;
                Debug.Log($"[StatusEffect] {enemyName} {se.data.effectType} applied this turn — skip DoT/decrement.");
                continue;
            }

            if (se.data.physicalDamagePerTurn > 0f)
            {
                int dotDamage = Mathf.Max(1, Mathf.FloorToInt(maxHP * se.data.physicalDamagePerTurn));
                currentHP = Mathf.Max(0, currentHP - dotDamage);
                Debug.Log($"[StatusEffect] {enemyName} took {dotDamage} DoT damage from {se.data.effectType}. (HP: {currentHP})");
            }

            se.remainingTurns--;
            if (se.remainingTurns <= 0)
            {
                Debug.Log($"[StatusEffect] {enemyName} {se.data.effectType} expired.");
                activeStatusEffects.RemoveAt(i);
            }
        }

        RefreshStatusIcons();
        UpdateStatusUI();
        if (currentHP <= 0) HandleDeath();
    }

    /// <summary>
    /// 특정 타입의 상태이상이 걸려있는지 확인
    /// </summary>
    public bool HasStatusEffect(StatusEffectType type)
    {
        return activeStatusEffects.Exists(e => e.data.effectType == type);
    }

    /// <summary>
    /// 특정 타입의 상태이상 남은 턴 수 반환
    /// </summary>
    public int GetStatusEffectRemainingTurns(StatusEffectType type)
    {
        StatusEffectInstance se = activeStatusEffects.Find(e => e.data.effectType == type);
        return se != null ? se.remainingTurns : 0;
    }

    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 특정 타입의 상태이상이 걸려있는지 확인 (구 HasCurse 대체)
    /// </summary>
    public bool HasCurse(StatusEffectType type) => HasStatusEffect(type);

    /// <summary>
    /// 특정 타입의 상태이상 남은 턴 수 반환 (구 GetCurseRemainingTurns 대체)
    /// </summary>
    public int GetCurseRemainingTurns(StatusEffectType type) => GetStatusEffectRemainingTurns(type);

    /// <summary>
    /// 공격력 감소 디버프 합산
    /// </summary>
    public float GetStatusEffectAttackDebuff()
    {
        float total = 0f;
        foreach (var se in activeStatusEffects)
            total += se.data.attackDebuff;
        return Mathf.Min(total, 100f);
    }

    /// <summary>
    /// 방어력 감소 디버프 합산
    /// </summary>
    public float GetStatusEffectDefenseDebuff()
    {
        float total = 0f;
        foreach (var se in activeStatusEffects)
            total += se.data.defenseDebuff;
        return Mathf.Min(total, 100f);
    }

    /// <summary>행동 불가 상태 확인 (Stun)</summary>
    public bool IsStunned() => activeStatusEffects.Exists(se => se.data.preventAction);

    /// <summary>스킬 사용 불가 상태 확인 (Silence)</summary>
    public bool IsSilenced() => activeStatusEffects.Exists(se => se.data.preventSkillUse);
    
    // 죽음 처리
    private void HandleDeath()
    {
        isDead = true;

        // 사망 시 모든 상태이상 제거
        activeStatusEffects.Clear();
        
        // 흔들림 중지 및 위치 복원
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        transform.position = originalPosition;

        // 스프라이트 숨기기
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        // 콜라이더 비활성화
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // 상태 UI 숨기기
        if (statusUI != null)
        {
            statusUI.SetActive(false);
        }
    }

    // World Space UI 생성 (public으로 변경하여 외부에서 호출 가능)
    public void CreateWorldSpaceUI()
    {
        if (!enableWorldSpaceStatusUI)
        {
            return;
        }

        if (Camera.main == null)
        {
            Debug.LogWarning($"[EnemyStats] Camera.main is null! Cannot create world space UI for {enemyName}");
            return;
        }

        // 프리팹 내부 UI가 있으면 런타임 생성 UI는 만들지 않음
        if (usesEmbeddedStatusUI)
        {
            UpdateStatusUI();
            return;
        }

        // 이미 UI가 있으면 제거
        if (statusUI != null)
        {
            Destroy(statusUI);
        }

        // Canvas 생성
        GameObject canvasObj = new GameObject($"EnemyStatusCanvas_{enemyName}");
        statusCanvas = canvasObj.AddComponent<Canvas>();
        statusCanvas.renderMode = RenderMode.WorldSpace;
        statusCanvas.worldCamera = Camera.main;

        // GraphicRaycaster 제거 (필요 없음)
        // CanvasScaler는 WorldSpace에서는 필요 없지만 에러 방지를 위해 추가
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 캔버스 크기: 상단 50px = HP/MP 텍스트, 하단 20px = 상태이상 아이콘
        const float canvasW = 120f, textH = 50f, iconH = 20f;
        float totalH = textH + iconH;

        RectTransform canvasRT = statusCanvas.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(canvasW, totalH);
        canvasRT.localScale = Vector3.one * statusUIScale;
        canvasRT.position = originalPosition + Vector3.up * statusUIOffsetY;

        // ── 배경 (전체 캔버스) ──
        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRT = bgObj.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        bgObj.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.7f);

        // ── HP/MP 텍스트 (상단 textH px) ──
        GameObject textObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(canvasObj.transform, false);
        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, iconH / totalH);
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        hpText = textObj.GetComponent<TextMeshProUGUI>();
        hpText.text = $"HP: {currentHP}/{maxHP}\nMP: {currentMP}/{maxMP}";
        hpText.fontSize = 20;
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.color = Color.white;
        hpText.raycastTarget = false;
        hpText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        hpText.overflowMode = TMPro.TextOverflowModes.Overflow;
        hpText.autoSizeTextContainer = false;
        mpText = hpText;

        // ── 상태이상 아이콘 패널 (하단 iconH px, 캔버스 내부) ──
        statusIconPanel = new GameObject("StatusIconPanel", typeof(RectTransform));
        statusIconPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform iconPanelRT = statusIconPanel.GetComponent<RectTransform>();
        iconPanelRT.anchorMin = new Vector2(0f, 0f);
        iconPanelRT.anchorMax = new Vector2(1f, iconH / totalH);
        iconPanelRT.offsetMin = Vector2.zero;
        iconPanelRT.offsetMax = Vector2.zero;

        statusUI = canvasObj;
        runtimeGeneratedStatusUI = true;
        statusIconObjects.Clear();

        // UI 위치 및 회전 설정 (RefreshStatusIcons 호출 없이)
        if (Camera.main != null)
        {
            statusCanvas.transform.position = originalPosition + Vector3.up * statusUIOffsetY;
            statusCanvas.transform.LookAt(Camera.main.transform);
            statusCanvas.transform.Rotate(0, 180, 0);
        }

        Debug.Log($"[EnemyStats] World space UI created for {enemyName} at position {originalPosition}");
    }

    // 상태 UI 업데이트 (외부에서 호출)
    public void UpdateStatusUI()
    {
        if (isDead) return;

        if (nameText != null)
        {
            nameText.text = enemyName;
        }

        // originalPosition 기준으로 UI 위치 업데이트 (런타임 생성 UI만 위치·회전 조정)
        if (statusCanvas != null && Camera.main != null && runtimeGeneratedStatusUI)
        {
            statusCanvas.transform.position = originalPosition + Vector3.up * statusUIOffsetY;
            statusCanvas.transform.LookAt(Camera.main.transform);
            statusCanvas.transform.Rotate(0, 180, 0);
        }
        // else if (statusUI == null && Camera.main != null)
        // {
        //     CreateWorldSpaceUI();
        // }

        // HP/MP 텍스트 갱신만 담당 — 아이콘은 효과 변경 시에만 RefreshStatusIcons() 호출
        if (hpText != null && mpText != null && hpText != mpText)
        {
            hpText.text = $"HP {currentHP}/{maxHP}";
            mpText.text = $"MP {currentMP}/{maxMP}";
            hpText.ForceMeshUpdate();
            mpText.ForceMeshUpdate();
        }
        else if (hpText != null)
        {
            hpText.text = $"HP: {currentHP}/{maxHP}" + System.Environment.NewLine + $"MP: {currentMP}/{maxMP}";
            hpText.ForceMeshUpdate();
        }
    }

    // 하이라이트 효과
    public void SetHighlight(bool highlight)
    {
        if (isDead || spriteRenderer == null) return;

        isHighlighted = highlight;
        
        if (highlight)
        {
            // 하얀색 블링크 효과 시작
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
                StartCoroutine(HighlightBlinkRoutine());
            }
        }
        else
        {
            // 원래 색상으로 복원
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
            StopCoroutine(HighlightBlinkRoutine());
        }
    }

    private IEnumerator HighlightBlinkRoutine()
    {
        while (isHighlighted && !isDead)
        {
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            yield return new WaitForSeconds(0.1f);
        }
        if (!isDead && spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
    }

    void Update()
    {
        // UI가 카메라를 향하도록 업데이트 (런타임 생성 UI만)
        if (statusCanvas != null && !isDead && Camera.main != null && runtimeGeneratedStatusUI)
        {
            statusCanvas.transform.position = originalPosition + Vector3.up * statusUIOffsetY;
            statusCanvas.transform.LookAt(Camera.main.transform);
            statusCanvas.transform.Rotate(0, 180, 0);
        }

        // UI가 없으면 생성 시도
        // if (enableWorldSpaceStatusUI && statusUI == null && !isDead && Camera.main != null)
        // {
        //     CreateWorldSpaceUI();
        // }
    }

    public void SetWorldSpaceStatusUIEnabled(bool enabled)
    {
        if (usesEmbeddedStatusUI)
        {
            if (statusUI != null)
            {
                statusUI.SetActive(true);
            }
            return;
        }

        enableWorldSpaceStatusUI = enabled;

        if (!enabled && statusUI != null)
        {
            Destroy(statusUI);
            statusUI = null;
            statusCanvas = null;
            hpText = null;
            mpText = null;
            runtimeGeneratedStatusUI = false;
        }
        else if (enabled && statusUI == null)
        {
            CreateWorldSpaceUI();
        }
    }

    void OnDestroy()
    {
        // 코루틴 정리
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        // UI 정리
        if (runtimeGeneratedStatusUI && statusUI != null)
        {
            Destroy(statusUI);
        }
    }
}

