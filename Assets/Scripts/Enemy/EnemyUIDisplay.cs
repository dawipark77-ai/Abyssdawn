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

        [Header("UI Position & Scale")]
        [Tooltip("몬스터 월드 좌표 기준 머리 위 오프셋 (스케일 무관)")]
        [SerializeField] private float worldOffsetY = 1.2f;

        [Tooltip("UI 표시 크기. 캔버스 Scale이 0.01이면 1.0이 기준. 너무 크면 줄이고 작으면 키우세요.")]
        [SerializeField] private float uiScale = 1f;

        [Tooltip("UIHolder Transform — Inspector에서 연결 (없으면 첫 번째 자식 자동 사용)")]
        [SerializeField] private Transform uiHolder;

        private EnemyStats enemyStats;

        private void Start()
        {
            enemyStats = GetComponent<EnemyStats>();

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

            // 부모 스케일 무관하게 월드 좌표로 UIHolder 위치 고정
            uiHolder.position = transform.position + Vector3.up * worldOffsetY;

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
    }
}
