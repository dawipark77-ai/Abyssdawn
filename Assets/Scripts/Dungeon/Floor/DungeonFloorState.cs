using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 층 하나의 "탐험 기록" (세계수의 미궁 / 위저드리식 층 기억).
/// 층 모양은 시드로 언제든 똑같이 재생성되므로, 여기엔 시드와 플레이어가 바꾼 것만 저장한다:
///  드러난 지도, 연 보물상자, 발견(발동)한 함정, 사용한 샘.
/// 위층으로 돌아가도 그 층은 떠났을 때 그대로다. DungeonPersistentData.floors 에 층 번호별로 보관 (씬 전환에도 유지).
/// </summary>
public class DungeonFloorState
{
    public readonly int floor;
    public readonly int seed;
    public readonly HashSet<Vector2Int> revealed = new HashSet<Vector2Int>();
    public readonly HashSet<Vector2Int> openedChests = new HashSet<Vector2Int>();
    public readonly HashSet<Vector2Int> knownTraps = new HashSet<Vector2Int>();
    public readonly HashSet<Vector2Int> usedSprings = new HashSet<Vector2Int>();
    /// <summary>계단으로 처음 내려가며 탐험 EXP 를 받았는지 (한 층에 한 번).</summary>
    public bool clearRewarded;
    /// <summary>한 번이라도 들어가 본 방 (처음 들어설 때만 연속 이동을 멈추기 위해).</summary>
    public readonly HashSet<int> enteredRooms = new HashSet<int>();

    public DungeonFloorState(int floor, int seed)
    {
        this.floor = floor;
        this.seed = seed;
    }
}
