using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using AbyssdawnBattle;

/// <summary>
/// 무기(물리) 상태이상 인스턴스 — StatusEffectSO 기반 추적용
/// </summary>
[System.Serializable]
public class StatusEffectInstance
{
    public StatusEffectSO data;
    public int remainingTurns;

    public StatusEffectInstance(StatusEffectSO effectData)
    {
        data = effectData;
        remainingTurns = effectData.physicalDuration;
    }
}

public class EnemyStats : MonoBehaviour
{
    [Header("Base Stats")]
    public string enemyName = "Slime";
    public int maxHP = 130;
    public int currentHP;
    public int maxMP = 0;
    public int currentMP;
    public int attack = 15;
    public int defense = 5;
    public int magic = 0;
    public int Agility = 5;
    public int luck = 1;

    [Header("Battle Position")]
    [Tooltip("현재 슬롯 위치 (BattleLine에서 자동 설정됨). 슬롯 1,2 = 전열, 슬롯 3,4 = 후열.")]
    public BattleSlot currentSlot = BattleSlot.Slot1;

    public bool IsFrontRow => SlotHelper.IsFrontRow(currentSlot);
    public bool IsBackRow => SlotHelper.IsBackRow(currentSlot);

    [Header("Visual Effects")]
    public float hitShakeDuration = 0.2f;
    public float hitShakeMagnitude = 0.1f;
    
    [Header("Curse System (저주 시스템)")]
    [Tooltip("현재 걸린 저주 리스트 (런타임에서 자동 관리)")]
    public List<CurseInstance> activeCurses = new List<CurseInstance>();

    [Header("Status Effect System (상태이상 시스템)")]
    [Tooltip("무기 공격으로 부여된 상태이상 리스트 (런타임에서 자동 관리)")]
    public List<StatusEffectInstance> activeStatusEffects = new List<StatusEffectInstance>();

    // Legacy compatibility (for old Ignite system)
    public bool isIgnited => HasCurse(CurseType.Ignite);
    public int igniteTurnsRemaining => GetCurseRemainingTurns(CurseType.Ignite);

    private bool isDead = false;
    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;
    private SpriteRenderer spriteRenderer;
    private GameObject statusUI;
    private Canvas statusCanvas;
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI mpText;
    private Material originalMaterial;
    private Material highlightMaterial;
    private bool isHighlighted = false;

    private static readonly Vector2 DEFAULT_STATUS_UI_SIZE = new Vector2(120f, 50f);
    private const float DEFAULT_STATUS_UI_SCALE = 0.005f;
    private const float DEFAULT_STATUS_UI_OFFSET_Y = 1.0f;

    [Header("Status UI Settings")]
    [Tooltip("Check to manually override UI size/scale/offset in Inspector")]
    public bool useCustomStatusUISettings = false;

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

        // [Anti-Gravity] 이름에 따른 레벨/경험치 자동 설정 (프리팹 데이터 대신 코드로 강제)
        ConfigureStatsByName();

        ApplyDefaultStatusUISettingsIfNeeded();
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
        StartCoroutine(DelayedCreateWorldSpaceUI());
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
        
        CreateWorldSpaceUI();
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
        if (statusUI == null && Camera.main != null)
        {
            CreateWorldSpaceUI();
        }
    }

    // 데미지 처리
    public int TakeDamage(int damage, bool isCritical = false)
    {
        if (isDead) return 0;

        int appliedDamage = Mathf.Min(damage, currentHP);
        currentHP -= damage;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        
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

    // --- [Curse System Methods] ---

    /// <summary>
    /// 저주를 적용합니다
    /// </summary>
    public void ApplyCurse(CurseData curseData)
    {
        if (curseData == null)
        {
            Debug.LogWarning($"[EnemyStats] {enemyName}에게 null Curse를 적용하려 했습니다.");
            return;
        }

        // 이미 같은 타입의 저주가 걸려있는지 확인
        CurseInstance existingCurse = activeCurses.Find(c => c.curseData.type == curseData.type);

        if (existingCurse != null)
        {
            // 기존 저주의 duration을 갱신 (더 긴 것으로)
            existingCurse.remainingTurns = Mathf.Max(existingCurse.remainingTurns, curseData.duration);
            Debug.Log($"[Curse] {enemyName}의 {curseData.curseName} 지속시간 갱신: {existingCurse.remainingTurns}턴");
        }
        else
        {
            // 새로운 저주 추가
            CurseInstance newCurse = new CurseInstance(curseData);
            activeCurses.Add(newCurse);
            Debug.Log($"[Curse] {enemyName}에게 {curseData.curseName} 적용! (지속: {curseData.duration}턴)");

            // VFX 생성 (선택사항)
            if (curseData.curseVFX != null)
            {
                newCurse.vfxInstance = Instantiate(curseData.curseVFX, transform);
            }
        }
    }

    /// <summary>
    /// 특정 타입의 저주를 제거합니다
    /// </summary>
    public void RemoveCurse(CurseType type)
    {
        CurseInstance curse = activeCurses.Find(c => c.curseData.type == type);
        if (curse != null)
        {
            // VFX 제거
            if (curse.vfxInstance != null)
            {
                Destroy(curse.vfxInstance);
            }

            activeCurses.Remove(curse);
            Debug.Log($"[Curse] {enemyName}의 {curse.curseData.curseName} 해제됨");
        }
    }

    /// <summary>
    /// 모든 저주를 제거합니다
    /// </summary>
    public void RemoveAllCurses()
    {
        foreach (var curse in activeCurses)
        {
            if (curse.vfxInstance != null)
            {
                Destroy(curse.vfxInstance);
            }
        }
        activeCurses.Clear();
        Debug.Log($"[Curse] {enemyName}의 모든 저주 해제됨");
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

        StatusEffectInstance existing = activeStatusEffects.Find(e => e.data.effectType == effect.effectType);
        if (existing != null)
        {
            existing.remainingTurns = Mathf.Max(existing.remainingTurns, effect.physicalDuration);
            Debug.Log($"[StatusEffect] {enemyName}의 {effect.effectType} 지속 갱신: {existing.remainingTurns}턴");
        }
        else
        {
            activeStatusEffects.Add(new StatusEffectInstance(effect));
            Debug.Log($"[StatusEffect] {enemyName}에게 {effect.effectType} 부여! ({effect.physicalDuration}턴)");
        }
        return true;
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

            if (se.data.physicalDamagePerTurn > 0f)
            {
                int dotDamage = Mathf.Max(1, Mathf.FloorToInt(maxHP * se.data.physicalDamagePerTurn));
                currentHP = Mathf.Max(0, currentHP - dotDamage);
                Debug.Log($"[StatusEffect] {enemyName}이(가) {se.data.effectType}로 {dotDamage} DoT 피해. (남은 HP: {currentHP})");
            }

            se.remainingTurns--;
            if (se.remainingTurns <= 0)
            {
                Debug.Log($"[StatusEffect] {enemyName}의 {se.data.effectType} 효과 종료");
                activeStatusEffects.RemoveAt(i);
            }
        }

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
    /// 특정 타입의 저주가 걸려있는지 확인
    /// </summary>
    public bool HasCurse(CurseType type)
    {
        return activeCurses.Exists(c => c.curseData.type == type);
    }

    /// <summary>
    /// 특정 타입의 저주 남은 턴 수 반환
    /// </summary>
    public int GetCurseRemainingTurns(CurseType type)
    {
        CurseInstance curse = activeCurses.Find(c => c.curseData.type == type);
        return curse != null ? curse.remainingTurns : 0;
    }

    /// <summary>
    /// 턴 종료 시 호출: 모든 저주의 DoT 데미지를 적용하고 duration 감소
    /// </summary>
    public void ProcessCursesEndOfTurn()
    {
        if (activeCurses.Count == 0 || currentHP <= 0 || isDead) return;

        Debug.Log($"[Curse] {enemyName}의 저주 처리 시작 (저주 수: {activeCurses.Count})");

        // 역순으로 순회하여 안전하게 제거
        for (int i = activeCurses.Count - 1; i >= 0; i--)
        {
            CurseInstance curse = activeCurses[i];

            // DoT 데미지 적용
            if (curse.curseData.dotDamagePercent > 0)
            {
                int dotDamage = Mathf.Max(1, Mathf.FloorToInt(maxHP * (curse.curseData.dotDamagePercent / 100f)));
                currentHP = Mathf.Max(0, currentHP - dotDamage);
                Debug.Log($"[Curse] {enemyName}이(가) {curse.curseData.curseName}로 {dotDamage} 데미지를 입었습니다. (남은 HP: {currentHP})");
            }

            // Duration 감소
            curse.remainingTurns--;

            // Duration이 0이 되면 저주 제거
            if (curse.remainingTurns <= 0)
            {
                Debug.Log($"[Curse] {curse.curseData.curseName} 효과 종료");
                if (curse.vfxInstance != null)
                {
                    Destroy(curse.vfxInstance);
                }
                activeCurses.RemoveAt(i);
            }
        }

        // 사망 체크
        if (currentHP <= 0)
        {
            HandleDeath();
        }
    }

    /// <summary>
    /// 저주로 인한 스탯 디버프를 계산 (총합)
    /// </summary>
    public float GetCurseAttackDebuff()
    {
        float total = 0f;
        foreach (var curse in activeCurses)
        {
            total += curse.curseData.attackDebuff;
        }
        return Mathf.Min(total, 100f); // 최대 100%
    }

    public float GetCurseDefenseDebuff()
    {
        float total = 0f;
        foreach (var curse in activeCurses)
        {
            total += curse.curseData.defenseDebuff;
        }
        return Mathf.Min(total, 100f);
    }

    /// <summary>
    /// 행동 불가 상태인지 확인 (Stun 등)
    /// </summary>
    public bool IsStunned()
    {
        return activeCurses.Exists(c => c.curseData.preventAction);
    }

    /// <summary>
    /// 스킬 사용 불가 상태인지 확인 (Silence 등)
    /// </summary>
    public bool IsSilenced()
    {
        return activeCurses.Exists(c => c.curseData.preventSkillUse);
    }

    // Legacy compatibility method
    [System.Obsolete("Use ApplyCurse with CurseData instead")]
    public void SetIgnited(bool ignited, int turns = 5)
    {
        if (ignited)
        {
            // Ignite curse를 찾아서 적용 (없으면 경고)
            CurseData igniteCurse = Resources.Load<CurseData>("Curses/Ignite");
            if (igniteCurse != null)
            {
                ApplyCurse(igniteCurse);
            }
            else
            {
                Debug.LogWarning("[EnemyStats] Ignite CurseData를 찾을 수 없습니다. Resources/Curses/Ignite.asset을 생성하세요.");
            }
        }
        else
        {
            RemoveCurse(CurseType.Ignite);
        }
    }

    // Legacy compatibility method
    [System.Obsolete("Use ProcessCursesEndOfTurn instead")]
    public void ProcessIgniteDamage()
    {
        ProcessCursesEndOfTurn();
    }
    
    // 죽음 처리
    private void HandleDeath()
    {
        isDead = true;

        // 사망 시 모든 저주 및 상태이상 제거
        RemoveAllCurses();
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
        if (Camera.main == null)
        {
            Debug.LogWarning($"[EnemyStats] Camera.main is null! Cannot create world space UI for {enemyName}");
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

        // Canvas 크기 설정 (충분한 가로폭 확보)
        RectTransform canvasRT = statusCanvas.GetComponent<RectTransform>();
        canvasRT.sizeDelta = statusUISize;
        canvasRT.localScale = Vector3.one * statusUIScale;
        canvasRT.position = originalPosition + Vector3.up * statusUIOffsetY;

        // 배경 추가 (가독성 향상)
        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRT = bgObj.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        
        UnityEngine.UI.Image bgImage = bgObj.GetComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.7f); // 반투명 검은색 배경

        // 단일 텍스트로 HP/MP를 두 줄로 표시
        GameObject textObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(canvasObj.transform, false);
        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        
        hpText = textObj.GetComponent<TextMeshProUGUI>();
        hpText.text = $"HP: {currentHP}/{maxHP}\nMP: {currentMP}/{maxMP}";
        hpText.fontSize = 20;
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.color = Color.white;
        hpText.raycastTarget = false;
        hpText.textWrappingMode = TMPro.TextWrappingModes.NoWrap; // 강제 줄바꿈 외에는 개행 금지
        hpText.overflowMode = TMPro.TextOverflowModes.Overflow; // 오버플로우 허용
        hpText.autoSizeTextContainer = false; // 자동 크기 조정 비활성화

        // MP 텍스트는 HP 텍스트와 같은 객체 사용 (두 줄로 표시)
        mpText = hpText;

        statusUI = canvasObj;
        
        // UI 위치 및 회전 설정
        UpdateStatusUI();
        
        Debug.Log($"[EnemyStats] World space UI created for {enemyName} at position {originalPosition}");
    }

    // 상태 UI 업데이트 (외부에서 호출)
    public void UpdateStatusUI()
    {
        if (isDead) return;

        // originalPosition 기준으로 UI 위치 업데이트 (흔들리지 않도록)
        if (statusCanvas != null && Camera.main != null)
        {
            statusCanvas.transform.position = originalPosition + Vector3.up * statusUIOffsetY;
            statusCanvas.transform.LookAt(Camera.main.transform);
            statusCanvas.transform.Rotate(0, 180, 0);
        }
        else if (statusUI == null && Camera.main != null)
        {
            // UI가 없으면 생성
            CreateWorldSpaceUI();
        }

        // HP와 MP를 한 텍스트에 두 줄로 표시
        if (hpText != null)
        {
            // 명시적으로 두 줄로 표시 (줄바꿈 문자 사용)
            hpText.text = $"HP: {currentHP}/{maxHP}" + System.Environment.NewLine + $"MP: {currentMP}/{maxMP}";
            // 텍스트가 제대로 표시되도록 강제 업데이트
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
        // UI가 카메라를 향하도록 업데이트
        if (statusCanvas != null && !isDead && Camera.main != null)
        {
            statusCanvas.transform.position = originalPosition + Vector3.up * statusUIOffsetY;
            statusCanvas.transform.LookAt(Camera.main.transform);
            statusCanvas.transform.Rotate(0, 180, 0);
        }
        
        // UI가 없으면 생성 시도
        if (statusUI == null && !isDead && Camera.main != null)
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
        if (statusUI != null)
        {
            Destroy(statusUI);
        }
    }
}

