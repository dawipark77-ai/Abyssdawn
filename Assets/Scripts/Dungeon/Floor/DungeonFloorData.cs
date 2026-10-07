using System.Collections.Generic;
using System.Text;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// 층 데이터 — 던전 한 층의 유일한 기준(Model).
// 이동 판정·지도 공개·자동 지도 그리기 모두 이 데이터를 읽는다. 화면(타일맵)은 데이터를 모른다.
//
// 좌표: x = 오른쪽(동)으로 증가, y = 아래쪽(남)으로 증가. 북쪽 = y - 1.
//   (기존 DungeonGridPlayer.GetDirVector 규칙과 동일)
//
// ※ 이 파일과 FloorTableEntry / RogueFloorGenerator / FloorVisibility 는
//   Unity 밖(.NET 4 컴파일러)에서 검증할 수 있도록 C# 5 문법만 사용한다.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>칸의 지형. Rock = 막힌 암반(걸을 수 없음).</summary>
public enum FloorTerrain { Rock = 0, Room = 1, Corridor = 2 }

/// <summary>칸 위의 특수 요소. 한 칸에 하나.</summary>
public enum FloorFeature { None = 0, StairsDown = 1, TownGate = 2, StairsUp = 3, Chest = 4, Trap = 5, Spring = 6 }

/// <summary>
/// 숨겨진 함정 종류 (이상한 던전 / 위저드리).
///  Spike = 최대 HP 일부 피해 / Teleport = 층 안 랜덤 방으로 전송 / Alarm = 즉시 전투 / Pitfall = 아래층으로 추락
///  [2026-10-07] PoisonDart = 피해 + 독 / Blade = 피해 + 출혈 / FlameVent = 피해 + 화상 / BlindingGas = 실명(시야↓)
///               Rockfall = 큰 피해 + 충격(다음 전투 첫 턴 기절) / Net = 그물에 걸려 위험도 크게 상승
///  ※ 저장 파일·층 기억에 숫자로 남으므로 새 종류는 맨 뒤에만 추가.
/// </summary>
public enum FloorTrapType { None = 0, Spike = 1, Teleport = 2, Alarm = 3, Pitfall = 4, PoisonDart = 5, Blade = 6, FlameVent = 7, BlindingGas = 8, Rockfall = 9, Net = 10, ManaDrain = 11, Crossbow = 12 }

/// <summary>층 종류. Town = 마을 층(마을 구현 전까지는 일반 층 + 마을 입구 타일).</summary>
public enum FloorType { Dungeon = 0, Town = 1 }

public struct FloorCell
{
    public FloorTerrain terrain;
    public int roomId;            // 방 칸이면 방 번호, 아니면 -1
    public FloorFeature feature;
    public FloorTrapType trap;    // feature == Trap 일 때 종류

    public bool IsWalkable { get { return terrain != FloorTerrain.Rock; } }
}

/// <summary>방과 통로가 만나는 경계 = 문. roomCell 은 방 안쪽 칸, corridorCell 은 바로 바깥 통로 칸.</summary>
public struct FloorDoor
{
    public Vector2Int roomCell;
    public Vector2Int corridorCell;

    public FloorDoor(Vector2Int roomCell, Vector2Int corridorCell)
    {
        this.roomCell = roomCell;
        this.corridorCell = corridorCell;
    }
}

/// <summary>방 하나. isGone 이면 방 없이 통로 교차점 1칸만 있는 구획(로그의 "gone room").</summary>
public class FloorRoom
{
    public int id;
    public Vector2Int section;     // 구획 좌표 (열, 행)
    public RectInt bounds;         // 방 영역. isGone 이면 교차점 1x1
    public bool isGone;
    public readonly List<FloorDoor> doors = new List<FloorDoor>();
    /// <summary>벽에 불(화로)이 있는 방 — 안에서는 시야가 넓어진다 (MapManager). brazierCell = 불이 걸린 벽 옆 칸 (지도 아이콘 위치).</summary>
    public bool lit;
    public Vector2Int brazierCell;
}

public class DungeonFloorData
{
    public readonly int floorNumber;
    public readonly int seed;
    public readonly FloorType floorType;
    public readonly int width;
    public readonly int height;
    public readonly FloorCell[,] cells;    // [x, y]
    public readonly List<FloorRoom> rooms = new List<FloorRoom>();
    public readonly List<FloorDoor> doors = new List<FloorDoor>();

    public Vector2Int startPos;
    public Vector2Int stairsPos;
    public bool hasTownGate;
    public Vector2Int townGatePos;
    /// <summary>올라가는 계단 (2층부터, 시작 위치 = 위층에서 내려와 도착하는 자리).</summary>
    public bool hasStairsUp;
    public Vector2Int stairsUpPos;

    // 탐험 요소 위치 (열림·발견·사용 여부는 층 데이터가 아닌 DungeonFloorState 가 기억한다)
    public readonly List<Vector2Int> chests = new List<Vector2Int>();
    public readonly List<Vector2Int> traps = new List<Vector2Int>();
    public readonly List<Vector2Int> springs = new List<Vector2Int>();

    /// <summary>검증 실패로 재시도했을 때 실제로 쓰인 시드 (디버그용).</summary>
    public int generatedSeed;

    public DungeonFloorData(int floorNumber, int seed, FloorType floorType, int width, int height)
    {
        this.floorNumber = floorNumber;
        this.seed = seed;
        this.floorType = floorType;
        this.width = width;
        this.height = height;
        cells = new FloorCell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y].terrain = FloorTerrain.Rock;
                cells[x, y].roomId = -1;
                cells[x, y].feature = FloorFeature.None;
            }
        }
    }

    public bool InBounds(Vector2Int p)
    {
        return p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
    }

    public bool IsWalkable(Vector2Int p)
    {
        return InBounds(p) && cells[p.x, p.y].IsWalkable;
    }

    /// <summary>범위 밖은 암반으로 취급.</summary>
    public FloorCell GetCell(Vector2Int p)
    {
        if (InBounds(p)) return cells[p.x, p.y];
        FloorCell rock = new FloorCell();
        rock.terrain = FloorTerrain.Rock;
        rock.roomId = -1;
        return rock;
    }

    /// <summary>그 칸이 속한 방. 통로·암반이면 null.</summary>
    public FloorRoom GetRoomAt(Vector2Int p)
    {
        if (!InBounds(p)) return null;
        int id = cells[p.x, p.y].roomId;
        if (id < 0 || id >= rooms.Count) return null;
        return rooms[id];
    }

    /// <summary>걸을 수 있는 칸 수 (탐험률 계산용).</summary>
    public int CountWalkable()
    {
        int n = 0;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (cells[x, y].IsWalkable) n++;
        return n;
    }

    /// <summary>
    /// 디버그용 글자 지도. revealed 를 주면 드러난 칸만 그린다(null = 전부).
    /// # 암반, . 방, , 통로, @ 시작, &gt; 내려가는 계단, &lt; 올라가는 계단, T 마을 입구, $ 보물상자, ^ 함정, ~ 샘
    /// </summary>
    public string ToAscii(ICollection<Vector2Int> revealed)
    {
        StringBuilder sb = new StringBuilder();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int p = new Vector2Int(x, y);
                if (revealed != null && !revealed.Contains(p)) { sb.Append(' '); continue; }

                FloorCell c = cells[x, y];
                char ch;
                if (c.feature == FloorFeature.StairsUp) ch = '<';
                else if (p == startPos) ch = '@';
                else if (c.feature == FloorFeature.StairsDown) ch = '>';
                else if (c.feature == FloorFeature.TownGate) ch = 'T';
                else if (c.feature == FloorFeature.Chest) ch = '$';
                else if (c.feature == FloorFeature.Trap) ch = '^';
                else if (c.feature == FloorFeature.Spring) ch = '~';
                else if (c.terrain == FloorTerrain.Room) ch = '.';
                else if (c.terrain == FloorTerrain.Corridor) ch = ',';
                else ch = '#';
                sb.Append(ch);
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
