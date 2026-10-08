using System;
using System.Collections.Generic;
using UnityEngine;

// ※ C# 5 문법만 사용 (DungeonFloorData.cs 머리말 참고).

/// <summary>
/// 층 설정표의 한 줄 — "minFloor ~ maxFloor 층은 이 설정으로 생성".
/// 층 크기 = (구획 열 × 구획 폭) × (구획 행 × 구획 높이).
/// </summary>
[Serializable]
public class FloorTableEntry
{
    [Tooltip("이 설정을 쓰는 첫 층")]
    public int minFloor = 1;
    [Tooltip("이 설정을 쓰는 마지막 층")]
    public int maxFloor = 1;
    public FloorType floorType = FloorType.Dungeon;

    [Header("구획 (로그식: 구획마다 방 하나)")]
    public int sectionCols = 2;
    public int sectionRows = 2;
    [Tooltip("구획 하나의 가로 칸 수. 방 최대 폭 = 구획 폭 - 3 (좌 1칸, 우 2칸 여백)")]
    public int sectionWidth = 8;
    [Tooltip("구획 하나의 세로 칸 수. 방 최대 높이 = 구획 높이 - 3")]
    public int sectionHeight = 10;

    [Header("방")]
    public int minRoomWidth = 3;
    public int minRoomHeight = 3;

    [Header("빈 구획 (방 없이 통로 교차점만 있는 구획)")]
    public int minGoneSections = 0;
    public int maxGoneSections = 0;

    [Header("여분 연결 (돌아가는 길)")]
    public int minExtraLinks = 0;
    public int maxExtraLinks = 1;

    [Header("모양 다양성")]
    [Tooltip("방이 직사각형이 아닌 모양(L자·십자·가운데 빈 고리·기둥 방)이 될 확률 (0~1)")]
    [Range(0f, 1f)] public float shapedRoomChance = 0.45f;
    [Tooltip("통로 하나가 두 줄(폭 2) 통로가 될 확률 (0~1). 문 바로 앞은 항상 한 줄")]
    [Range(0f, 1f)] public float wideCorridorChance = 0.3f;
    [Tooltip("막다른 길 개수 범위")]
    public int minDeadEnds = 0;
    public int maxDeadEnds = 2;

    [Header("빛 (안개 시야)")]
    [Tooltip("벽에 불(화로)이 걸린 방이 될 확률 (0~1). 불 있는 방 안에서는 시야가 넓어진다")]
    [Range(0f, 1f)] public float litRoomChance = 0.3f;
    [Tooltip("어두운 층 — 기본 시야가 1칸으로 줄어든다 (불·횃불이 있으면 조금 넓어짐)")]
    public bool darkFloor = false;

    [Header("인카운터")]
    [Tooltip("이 층들의 인카운터 확률. -1 이면 씬의 DungeonEncounter 설정값 그대로 사용")]
    public float encounterChance = -1f;

    [Tooltip("한 번 전투에 나오는 몬스터 수 1·2·3·4마리의 가중치 (예: 55,30,12,3). 4마리면 2/2 대열")]
    public float[] groupSizeWeights = new float[] { 1f, 0f, 0f, 0f };

    [Header("탐험 요소")]
    [Tooltip("보물상자 개수 범위 (방 안에 배치)")]
    public int minChests = 1;
    public int maxChests = 2;
    [Tooltip("숨겨진 함정 개수 범위 (방·통로에 배치, 밟기 전엔 보이지 않음). 종류는 층이 깊을수록 늘어난다")]
    public int minTraps = 0;
    public int maxTraps = 2;
    [Tooltip("회복의 샘이 생길 확률 (0~1)")]
    [Range(0f, 1f)] public float springChance = 0.3f;

    public int FloorWidth { get { return sectionCols * sectionWidth; } }
    public int FloorHeight { get { return sectionRows * sectionHeight; } }

    public bool Contains(int floor)
    {
        return floor >= minFloor && floor <= maxFloor;
    }

    public static FloorTableEntry Create(int minFloor, int maxFloor, FloorType type,
                                         int cols, int rows, int sectionWidth, int sectionHeight,
                                         int goneMin, int goneMax, int extraMin, int extraMax)
    {
        FloorTableEntry e = new FloorTableEntry();
        e.minFloor = minFloor;
        e.maxFloor = maxFloor;
        e.floorType = type;
        e.sectionCols = cols;
        e.sectionRows = rows;
        e.sectionWidth = sectionWidth;
        e.sectionHeight = sectionHeight;
        e.minGoneSections = goneMin;
        e.maxGoneSections = goneMax;
        e.minExtraLinks = extraMin;
        e.maxExtraLinks = extraMax;
        return e;
    }

    /// <summary>모양 다양성 설정 (기본값 표 작성용).</summary>
    /// <summary>몬스터 수 1~4마리 가중치.</summary>
    public FloorTableEntry WithGroups(float one, float two, float three, float four)
    {
        groupSizeWeights = new float[] { one, two, three, four };
        return this;
    }

    /// <summary>roll01 (0~1 난수)로 몬스터 수 1~4를 고른다. 가중치가 없으면 1.</summary>
    public int PickGroupSize(float roll01)
    {
        float total = 0f;
        if (groupSizeWeights != null) for (int i = 0; i < groupSizeWeights.Length && i < 4; i++) total += Math.Max(0f, groupSizeWeights[i]);
        if (total <= 0f) return 1;
        float r = roll01 * total;
        for (int i = 0; i < groupSizeWeights.Length && i < 4; i++)
        {
            r -= Math.Max(0f, groupSizeWeights[i]);
            if (r <= 0f) return i + 1;
        }
        return 1;
    }

    public FloorTableEntry WithShapes(float shapedRooms, float wideCorridors, int deadEndMin, int deadEndMax)
    {
        shapedRoomChance = shapedRooms;
        wideCorridorChance = wideCorridors;
        minDeadEnds = deadEndMin;
        maxDeadEnds = deadEndMax;
        return this;
    }

    /// <summary>탐험 요소 개수 설정 (기본값 표 작성용).</summary>
    public FloorTableEntry WithContents(int chestMin, int chestMax, int trapMin, int trapMax, float spring)
    {
        minChests = chestMin;
        maxChests = chestMax;
        minTraps = trapMin;
        maxTraps = trapMax;
        springChance = spring;
        return this;
    }
}

/// <summary>층 설정표 기본값과 조회 규칙.</summary>
public static class FloorTableDefaults
{
    /// <summary>
    /// 베타 10층 기본 설정. 세로 화면(가로 약 16칸 보임)에 맞춰 초반 층은 가로 16칸 이하.
    ///   1층 마을 / 2~4층 2×2 / 5층 마을 / 6~8층 2×3 / 9~10층 3×3 / 11층~ 3×4 (계속 사용)
    /// 보물상자·함정·샘은 깊을수록 많아진다.
    /// </summary>
    public static List<FloorTableEntry> CreateBeta()
    {
        List<FloorTableEntry> list = new List<FloorTableEntry>();
        // 몬스터 수 가중치 (1·2·3·4마리) — [2026-10-04] 같은 몬스터 최대 4마리까지, 4마리면 2/2 대열
        // [2026-10-08] 1층은 튜토리얼 — 거의 1대1 (1마리 70% · 2마리 22% · 3마리 7% · 4마리 1%)
        list.Add(FloorTableEntry.Create(1, 1, FloorType.Town, 2, 2, 8, 10, 0, 0, 0, 1).WithContents(1, 1, 6, 9, 0.5f).WithShapes(0.3f, 0.2f, 0, 1).WithGroups(70, 22, 7, 1));
        // [2026-10-07 2차] 함정 밀도 약 3% → 약 8% (걸을 수 있는 칸 기준. 1·2~4·5층 약 100칸, 6~8층 156, 9~10층 239, 11층~ 318)
        //   1층 0~1 → 6~9 / 2~4층 2~4 → 7~10 / 5층 0~1 → 5~7 / 6~8층 3~6 → 11~15 / 9~10층 4~7 → 17~22 / 11층~ 5~8 → 23~29
        list.Add(FloorTableEntry.Create(2, 4, FloorType.Dungeon, 2, 2, 8, 10, 0, 0, 0, 1).WithContents(1, 2, 7, 10, 0.3f).WithShapes(0.4f, 0.3f, 0, 2).WithGroups(40, 35, 18, 7));
        list.Add(FloorTableEntry.Create(5, 5, FloorType.Town, 2, 2, 8, 10, 0, 0, 0, 1).WithContents(1, 2, 5, 7, 0.5f).WithShapes(0.4f, 0.3f, 0, 1).WithGroups(30, 35, 22, 13));
        list.Add(FloorTableEntry.Create(6, 8, FloorType.Dungeon, 2, 3, 8, 10, 0, 0, 1, 1).WithContents(1, 3, 11, 15, 0.3f).WithShapes(0.5f, 0.35f, 1, 2).WithGroups(30, 35, 22, 13));
        list.Add(FloorTableEntry.Create(9, 10, FloorType.Dungeon, 3, 3, 8, 10, 0, 1, 1, 2).WithContents(2, 3, 17, 22, 0.35f).WithShapes(0.55f, 0.4f, 1, 3).WithGroups(30, 35, 22, 13));
        list.Add(FloorTableEntry.Create(11, 9999, FloorType.Dungeon, 3, 4, 8, 10, 0, 2, 1, 3).WithContents(2, 4, 23, 29, 0.35f).WithShapes(0.6f, 0.4f, 2, 4).WithGroups(30, 35, 22, 13));
        for (int i = 0; i < list.Count; i++) list[i].encounterChance = BetaEncounterChance;
        return list;
    }

    /// <summary>
    /// [2026-09-30] 인카운터 확률 0.09 → 0.05 (위험도 게이지 기준 평균 약 24걸음마다 전투).
    /// 전투는 줄이고 한 번 한 번을 무겁게 — 전투 사이 회복 수단이 적어 0.09(약 15걸음)면 층당 7회 전투로 버틸 수 없었다 (10층 시뮬레이션).
    /// </summary>
    public const float BetaEncounterChance = 0.05f;

    /// <summary>
    /// floor 에 해당하는 설정. 해당 줄이 없으면 가장 깊은 줄(maxFloor 최대)을 계속 사용.
    /// 표가 비어 있으면 베타 기본값에서 찾는다.
    /// </summary>
    public static FloorTableEntry Find(List<FloorTableEntry> entries, int floor)
    {
        if (entries == null || entries.Count == 0) entries = CreateBeta();

        FloorTableEntry deepest = null;
        for (int i = 0; i < entries.Count; i++)
        {
            FloorTableEntry e = entries[i];
            if (e == null) continue;
            if (e.Contains(floor)) return e;
            if (deepest == null || e.maxFloor > deepest.maxFloor) deepest = e;
        }
        return deepest;
    }
}
