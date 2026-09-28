using UnityEngine;
using System.Collections;
using Genesis01.Dungeon;

namespace Genesis01.Dungeon
{
    public class DungeonPlayerMovement : MonoBehaviour
    {
        [Header("Dependencies")]
        public DungeonGenerator dungeonGenerator; // Optional now
        public Camera mainCamera;
        public MiniMapController miniMapController;

        [Header("Movement Settings")]
        public float moveDuration = 0.4f;
        public float rotateDuration = 0.3f;

        [Header("Head Bob Settings")]
        public float bobAmount = 0.05f; // How high the camera goes up
        public float bobFrequency = 10.0f; // Speed of the bob
        public float stepZoomAmount = 2.0f; // FOV reduction during step

        private Vector2Int currentGridPos = new Vector2Int(1, 1);
        private int currentDirIndex = 0; // 0=North, 1=East, 2=South, 3=West
        
        // Directions: N, E, S, W
        // Aligned with DungeonMap: North is (0, -1) [y decrease]
        private readonly Vector2Int[] directions = {
            new Vector2Int(0, -1), // North
            new Vector2Int(1, 0),  // East
            new Vector2Int(0, 1),  // South
            new Vector2Int(-1, 0)  // West
        };

        private bool isBusy = false;
        private Vector3 initialCameraPos;
        private float initialFOV;

        void Start()
        {
            // [2026-09-29] 탑다운 격자 이동(DungeonGridPlayer)이 있는 씬에서는 끈다.
            // 같은 W/A/S/D 입력을 함께 읽으면서 벽 검사 없는 가상 위치로 인카운터를 한 번 더 굴리고,
            // 카메라 위치를 되돌려 카메라 추적(DungeonCameraFollow)과도 충돌했다.
            if (FindFirstObjectByType<DungeonGridPlayer>() != null)
            {
                Debug.Log("[DungeonPlayerMovement] 격자 이동(DungeonGridPlayer)이 있어 1인칭 이동을 비활성화합니다.");
                enabled = false;
                return;
            }

            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                initialCameraPos = mainCamera.transform.localPosition;
                initialFOV = mainCamera.fieldOfView;
            }

            if (miniMapController == null)
                miniMapController = FindFirstObjectByType<MiniMapController>();
                
            // Initial update
            if (miniMapController != null)
            {
                ExploreSurroundings(currentGridPos);
                miniMapController.OnPlayerMove(currentGridPos);
            }
        }

        void Update()
        {
            if (isBusy) return;

            // Keyboard Input (WASD)
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) MoveForward();
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) MoveBackward();
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) TurnLeft();
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) TurnRight();
            
            // Optional: Q/E for Strafing if desired
            if (Input.GetKey(KeyCode.Q)) StrafeLeft();
            if (Input.GetKey(KeyCode.E)) StrafeRight();
        }

        // --- Public Methods for UI Buttons ---

        public void OnClickForward() 
        {
            Debug.Log("Forward Button Clicked");
            MoveForward();
        }
        public void OnClickBackward() => MoveBackward();
        public void OnClickTurnLeft() => TurnLeft();
        public void OnClickTurnRight() => TurnRight();
        public void OnClickStrafeLeft() => StrafeLeft(); // Connect this to "L Side" button
        public void OnClickStrafeRight() => StrafeRight(); // Connect this to "R Side" button

        // --- Movement Logic ---

        private void MoveForward()
        {
            if (isBusy) 
            {
                Debug.Log("Player is busy");
                return;
            }
            Vector2Int dir = directions[currentDirIndex];
            WallFlags wallDir = GetWallFlagFromIndex(currentDirIndex);
            Debug.Log($"MoveForward called. Dir: {dir}, WallFlags: {wallDir}");
            StartCoroutine(SimulateMoveEffect(dir, wallDir));
        }

        private void MoveBackward()
        {
            if (isBusy) return;
            Vector2Int dir = -directions[currentDirIndex];
            StartCoroutine(SimulateMoveEffect(dir, WallFlags.None));
        }

        private void StrafeLeft()
        {
            if (isBusy) return;
            // Left is -90 degrees from current
            int leftIndex = (currentDirIndex + 3) % 4; 
            Vector2Int dir = directions[leftIndex];
            StartCoroutine(SimulateMoveEffect(dir, WallFlags.None));
        }

        private void StrafeRight()
        {
            if (isBusy) return;
            // Right is +90 degrees from current
            int rightIndex = (currentDirIndex + 1) % 4;
            Vector2Int dir = directions[rightIndex];
            StartCoroutine(SimulateMoveEffect(dir, WallFlags.None));
        }

        // 현재 바라보는 방향 인덱스(0=N,1=E,2=S,3=W)를 WallFlags로 변환
        private WallFlags GetWallFlagFromIndex(int dirIndex)
        {
            switch (dirIndex % 4)
            {
                case 0: return WallFlags.North;
                case 1: return WallFlags.East;
                case 2: return WallFlags.South;
                case 3: return WallFlags.West;
                default: return WallFlags.None;
            }
        }


        private void TurnLeft()
        {
            if (isBusy) return;
            StartCoroutine(SimulateTurnEffect(-1));
        }

        private void TurnRight()
        {
            if (isBusy) return;
            StartCoroutine(SimulateTurnEffect(1));
        }

        // --- Coroutines for Visual Effects ---

        IEnumerator SimulateMoveEffect(Vector2Int dir, WallFlags wallDir)
        {
            isBusy = true;
            Debug.Log($"SimulateMoveEffect started. Dir: {dir}, WallDir: {wallDir}");

            // 1. Logical Update (Check collision using DungeonMap)
            Vector2Int targetPos = currentGridPos + dir;
            bool canMove = true;
            
            var map = FindFirstObjectByType<DungeonMap>();
            if (map != null)
            {
                if (!map.IsWalkable(0, targetPos.x, targetPos.y))
                {
                    Debug.Log($"Blocked by Wall at {targetPos}");
                    canMove = false;
                }
            }
            else if (dungeonGenerator != null)
            {
                if (!dungeonGenerator.IsWalkable(targetPos.x, targetPos.y))
                {
                    canMove = false;
                }
            }

            if (canMove)
            {
                Debug.Log($"Movement Successful! New Pos: {targetPos}");
                currentGridPos = targetPos;
                
                // Update Exploration
                ExploreSurroundings(currentGridPos);

                // Update Minimap (선택적 기능 - 없어도 게임 진행 가능)
                if (miniMapController == null)
                {
                    miniMapController = FindFirstObjectByType<MiniMapController>();
                }

                if (miniMapController != null)
                {
                    Debug.Log($"Updating Minimap to {currentGridPos}");
                    miniMapController.OnPlayerMove(currentGridPos);
                }
                // MiniMapController가 없어도 게임은 계속 진행 (에러 대신 조용히 무시)

                // 랜덤 인카운터 체크 추가
                DungeonEncounter.Instance?.CheckEncounter(currentGridPos);
            }

            // 2. Visual Effect (Head Bob + Zoom)
            
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / moveDuration;
                float curve = Mathf.Sin(t * Mathf.PI); // 0 -> 1 -> 0

                if (mainCamera != null)
                {
                    // Head Bob (Y-axis)
                    Vector3 bobOffset = new Vector3(0, curve * bobAmount, 0);
                    mainCamera.transform.localPosition = initialCameraPos + bobOffset;

                    // FOV Zoom (Zoom in slightly at peak)
                    mainCamera.fieldOfView = initialFOV - (curve * stepZoomAmount);
                }

                yield return null;
            }

            // Reset
            if (mainCamera != null)
            {
                mainCamera.transform.localPosition = initialCameraPos;
                mainCamera.fieldOfView = initialFOV;
            }

            isBusy = false;
        }

        IEnumerator SimulateTurnEffect(int direction) // -1 Left, 1 Right
        {
            isBusy = true;

            // Logical Update
            currentDirIndex = (currentDirIndex + direction + 4) % 4;

            // Visual Effect
            
            Quaternion startRot = mainCamera.transform.localRotation;
            Quaternion targetTilt = Quaternion.Euler(0, 0, -direction * 2f); // Slight tilt

            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / rotateDuration;
                float curve = Mathf.Sin(t * Mathf.PI); // 0 -> 1 -> 0
                
                // Simple tilt effect
                mainCamera.transform.localRotation = Quaternion.Slerp(startRot, startRot * targetTilt, curve);
                
                yield return null;
            }
            
            mainCamera.transform.localRotation = startRot; // Reset

            isBusy = false;
        }

        private void ExploreSurroundings(Vector2Int center)
        {
            var map = FindFirstObjectByType<DungeonMap>();
            if (map == null) return;

            // Explore 3x3 area
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    int targetX = center.x + x;
                    int targetY = center.y + y;

                    if (map.IsValidPosition(0, targetX, targetY))
                    {
                        // Explicitly referencing Genesis01.Dungeon.DungeonTile
                        DungeonTile tile = map.GetTile(0, targetX, targetY);
                        if (!tile.isExplored)
                        {
                            tile.isExplored = true;
                            map.SetTile(0, targetX, targetY, tile);
                        }
                    }
                }
            }
        }
    }
}
