using UnityEngine;
using TMPro;

namespace AbyssdawnBattle
{
    /// <summary>
    /// EnemyPrefab_World에 붙는 UI 표시 컴포넌트.
    /// UIHolder를 부모 스케일과 무관하게 월드 좌표 기준으로 배치합니다.
    /// </summary>
    public class EnemyUIDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text mpText;
        [Tooltip("슬롯 순번(1,2,3,...) 표시용 텍스트. UIHolder/Number 에 연결")]
        [SerializeField] private TMP_Text numberText;

        [Header("UI Position Mode")]
        [Tooltip("TRUE: 모든 몬스터 UI를 동일한 월드 Y로 일렬 정렬. FALSE: 각 몬스터 스프라이트 상단을 따라감")]
        [SerializeField] private bool useFixedWorldY = true;

        [Tooltip("useFixedWorldY=TRUE일 때 모든 몬스터 UI가 정렬될 월드 Y 값")]
        [SerializeField] private float fixedWorldY = 2.5f;

        [Header("UI Position & Scale")]
        [Tooltip("useFixedWorldY=FALSE일 때 스프라이트 상단 위 추가 여백 (월드 단위). 0.3~0.5 권장")]
        [SerializeField] private float worldOffsetY = 0.3f;

        [Tooltip("스프라이트를 찾지 못했을 때 몬스터 중심으로부터 사용할 폴백 오프셋")]
        [SerializeField] private float fallbackOffsetY = 1.2f;

        [Tooltip("UI 표시 크기. 캔버스 Scale이 0.01이면 1.0이 기준. 너무 크면 줄이고 작으면 키우세요.")]
        [SerializeField] private float uiScale = 1f;

        [Tooltip("UIHolder Transform — Inspector에서 연결 (없으면 첫 번째 자식 자동 사용)")]
        [SerializeField] private Transform uiHolder;

        private EnemyStats enemyStats;
        private SpriteRenderer spriteRenderer;

        // BattleManager가 SetUIAnchor로 주입하는 외부 앵커 Transform.
        // 설정되면 UIHolder가 매 프레임 이 앵커의 월드 좌표를 그대로 따라간다.
        private Transform externalAnchor;

        // BattleManager가 SetUIAnchorPosition로 주입하는 고정 월드 좌표.
        // 씬에 앵커 오브젝트를 만들지 않고도 순번별 자동 배치에 사용한다.
        private Vector3? fixedAnchorPos;

        /// <summary>
        /// 씬에 배치된 고정 UI 앵커 Transform을 지정. null을 주면 앵커 사용 해제.
        /// </summary>
        public void SetUIAnchor(Transform anchor)
        {
            externalAnchor = anchor;
            if (anchor != null) fixedAnchorPos = null; // Transform 앵커가 우선
        }

        /// <summary>
        /// 씬 오브젝트 없이 월드 좌표만으로 UI 위치를 고정. BattleManager 자동 배치용.
        /// </summary>
        public void SetUIAnchorPosition(Vector3 worldPos)
        {
            fixedAnchorPos = worldPos;
            externalAnchor = null;
        }

        private void Start()
        {
            enemyStats = GetComponent<EnemyStats>();
            spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();

            if (enemyStats == null)
            {
                Debug.LogWarning($"[EnemyUIDisplay] EnemyStats 컴포넌트를 찾을 수 없습니다: {gameObject.name}");
                return;
            }

            // UIHolder 자동 탐색
            if (uiHolder == null)
            {
                uiHolder = transform.Find("UIHolder");
                if (uiHolder == null && transform.childCount > 0)
                    uiHolder = transform.GetChild(0);
            }

            Canvas canvas = GetComponentInChildren<Canvas>();
            if (canvas != null)
                canvas.worldCamera = Camera.main;

            RefreshUI();
        }

        private void LateUpdate()
        {
            if (uiHolder == null) return;

            // UI 위치 결정 우선순위:
            //  1) externalAnchor(씬 오브젝트) → 그 Transform 좌표
            //  2) fixedAnchorPos(월드 좌표값) → 해당 좌표
            //  3) useFixedWorldY=TRUE → 몬스터 X + fixedWorldY
            //  4) 아니면 스프라이트 상단 + worldOffsetY
            Vector3 targetPos;
            if (externalAnchor != null)
            {
                targetPos = externalAnchor.position;
            }
            else if (fixedAnchorPos.HasValue)
            {
                targetPos = fixedAnchorPos.Value;
            }
            else if (useFixedWorldY)
            {
                targetPos = new Vector3(transform.position.x, fixedWorldY, transform.position.z);
            }
            else if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                targetPos = new Vector3(transform.position.x, spriteRenderer.bounds.max.y + worldOffsetY, transform.position.z);
            }
            else
            {
                targetPos = new Vector3(transform.position.x, transform.position.y + fallbackOffsetY, transform.position.z);
            }
            uiHolder.position = targetPos;

            // 부모 스케일을 상쇄하고 uiScale을 적용
            // uiScale = 1 → UIHolder 월드 스케일 = 1 (캔버스 0.01 기준 적당한 크기)
            // 너무 크면 uiScale을 줄이세요 (예: 0.3)
            Vector3 ps = transform.lossyScale;
            if (ps.x != 0f && ps.y != 0f && ps.z != 0f)
            {
                uiHolder.localScale = new Vector3(
                    uiScale / ps.x,
                    uiScale / ps.y,
                    uiScale / ps.z
                );
            }
        }

        /// <summary>
        /// NameText / HPText / MPText를 EnemyStats 현재 값으로 갱신합니다.
        /// 외부(BattleManager 등)에서 데미지·회복 후 호출하세요.
        /// </summary>
        public void RefreshUI()
        {
            if (enemyStats == null) return;

            if (nameText != null)
                nameText.text = enemyStats.enemyName;

            if (hpText != null)
                hpText.text = $"HP {enemyStats.currentHP}/{enemyStats.maxHP}";

            if (mpText != null)
                mpText.text = $"MP {enemyStats.currentMP}/{enemyStats.maxMP}";
        }

        /// <summary>
        /// 슬롯 순번(1,2,3,...) 표시. BattleManager가 스폰 직후 행(Row)+좌→우 순으로 계산해 호출.
        /// 0 이하 값을 주면 숫자를 비운다.
        /// </summary>
        public void SetNumber(int n)
        {
            if (numberText == null) return;
            numberText.text = n > 0 ? n.ToString() : string.Empty;
        }
    }
}
