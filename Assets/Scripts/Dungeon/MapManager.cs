using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    // ... (rest of fields maintained)
    public Tilemap fogTilemap;

    public TileBase floorTile;
    public TileBase wallTile;
    public TileBase fogTile;
    public TileBase stairsTile; // ?ㅼ쓬 痢듭쑝濡??대젮媛??怨꾨떒
    public TileBase exitHighlightTile; // 異쒓뎄 ?됱긽 ?꾩슜(諛앹? ???沅뚯옣)
    [Header("Exit Highlight")]
    public bool highlightExit = true;
    public Color exitColor = Color.red;
    public bool alwaysRevealExit = false; // 湲곕낯? ?덇컻??媛由щ릺, 耳쒕㈃ ??긽 蹂댁씠寃?
    public Vector2Int ExitPos => exitPos; // ?몃??먯꽌 異쒓뎄 醫뚰몴 ?뺤씤??
    [Header("Map Size Settings")]
    public int minWidth = 30;
    public int maxWidth = 30;
    public int minHeight = 30;
    public int maxHeight = 30;
    
    [HideInInspector]
    public int width;
    [HideInInspector]
    public int height;

    [Header("Special Room Settings")]
    public int minSpecialRoomCount = 1;
    public int maxSpecialRoomCount = 3;

    [Header("Room Settings")]
    public int minRoomCount = 3;
    public int maxRoomCount = 5;
    public int minRoomSize = 3;
    public int maxRoomSize = 5;
    public int roomSpacing = 2; // 諛??ъ씠 理쒖냼 媛꾧꺽
    
    [Range(0f, 1f)]
    public float sealedRoomChance = 0.3f; // ?쇰컲 諛⑹씠 ?ロ엺 諛⑹씠 ???뺣쪧

    int[,] mapData;
    bool[,] isRoom; // 諛⑹쓽 諛붾떏怨?踰쎌쓣 蹂댄샇?섍린 ?꾪븳 諛곗뿴
    List<RoomInfo> rooms = new List<RoomInfo>(); // ?앹꽦??諛⑸뱾 ???
    Vector2Int startPos; // Store start position
    Vector2Int exitPos;  // ?ㅼ쓬 痢듭쑝濡??대젮媛???꾩튂

    void Start()
    {
        try
        {
            Debug.Log("[MapManager] Start() - Initializing dungeon generation.");
            if (DungeonPersistentData.currentFloor < 1) DungeonPersistentData.currentFloor = 1;
            Debug.Log($"[MapManager] Current Floor: B{DungeonPersistentData.currentFloor}");
            if (!DungeonPersistentData.hasSavedState)
            {
                DungeonPersistentData.currentSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                DungeonPersistentData.revealedTiles.Clear();
                Debug.Log($"[MapManager] New Seed generated: {DungeonPersistentData.currentSeed}. Fog cleared.");
            }
            else
            {
                Debug.Log($"[MapManager] Using existing Seed: {DungeonPersistentData.currentSeed}");
            }

            Debug.Log("[MapManager] Step 1: Generating maze...");
            GenerateMaze();
            
            Debug.Log("[MapManager] Step 2: Drawing map...");
            DrawMap();
            
            Debug.Log("[MapManager] Step 3: Placing player...");
            PlacePlayer(); // Move player placement AFTER drawing map/fog
            
            Debug.Log("[MapManager] Dungeon generation sequence completed.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MapManager] CRITICAL ERROR in Start(): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            // ?덉쇅媛 諛쒖깮?대룄 寃뚯엫??怨꾩냽 ?ㅽ뻾?섎룄濡???            // 理쒖냼?쒖쓽 留듭씠?쇰룄 ?앹꽦 ?쒕룄
            try
            {
                if (floorTilemap != null && wallTilemap != null)
                {
                    Debug.LogWarning("[MapManager] Attempting emergency map generation...");
                    // 理쒖냼?쒖쓽 ?덉쟾??留??앹꽦
                    if (width <= 0 || height <= 0)
                    {
                        width = 10;
                        height = 10;
                        mapData = new int[height, width];
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                mapData[y, x] = (x == 0 || x == width - 1 || y == 0 || y == height - 1) ? 1 : 0;
                            }
                        }
                        startPos = new Vector2Int(1, 1);
                        exitPos = new Vector2Int(width - 2, height - 2);
                        DrawMap();
                        PlacePlayer();
                    }
                }
            }
            catch (System.Exception ex2)
            {
                Debug.LogError($"[MapManager] Emergency recovery also failed: {ex2.Message}");
            }
        }
    }

    /// <summary>
    /// 怨꾨떒 ?꾨떖 ???ㅼ쓬 痢?B1, B2 ...)???앹꽦
    /// </summary>
    public void GenerateNextFloor()
    {
        DungeonPersistentData.currentFloor++;
        DungeonPersistentData.hasSavedState = false;
        DungeonPersistentData.revealedTiles.Clear();
        DungeonPersistentData.lastPlayerGridPos = Vector2Int.zero;
        DungeonPersistentData.lastPlayerFacing = DungeonDirection.North;
        DungeonPersistentData.currentSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

        // 踰쎌꽑 ?쇱씤??珥덇린??
        Genesis01.Dungeon.DungeonWallLineDrawer drawer = FindFirstObjectByType<Genesis01.Dungeon.DungeonWallLineDrawer>();
        if (drawer != null)
        {
            drawer.ResetLines();
        }

        // ??쇰㏊/?뚮젅?댁뼱 ?곹깭瑜??꾩쟾??珥덇린?뷀븯???댁쟾 痢??뺣낫媛 ?⑥? ?딄쾶 ??
        if (fogTilemap != null) fogTilemap.ClearAllTiles();
        if (floorTilemap != null) floorTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();

        DungeonGridPlayer player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null)
        {
            player.gridPos = Vector2Int.zero;
            player.facing = DungeonDirection.North;
        }

        Debug.Log($"[MapManager] Descend to floor B{DungeonPersistentData.currentFloor} with Seed {DungeonPersistentData.currentSeed}");

        GenerateMaze();
        DrawMap();
        PlacePlayer();

        // ??留?湲곕컲?쇰줈 踰쎌꽑 ?ъ깮??
        if (drawer != null)
        {
            drawer.DrawWalls();
        }
    }


    void GenerateMaze()
    {
        // 던전 생성은 시드로 결정론적이어야 하지만,
        // 전역 UnityEngine.Random 시퀀스(인카운터/전투 등)를 오염시키면 안 됨.
        // 따라서 Random.state를 저장/복원하여 던전 생성에서만 시드를 고정한다.
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(DungeonPersistentData.currentSeed);
        
        width = UnityEngine.Random.Range(minWidth, maxWidth + 1);
        height = UnityEngine.Random.Range(minHeight, maxHeight + 1);
        mapData = new int[height, width];
        isRoom = new bool[height, width];

        // 1. Initialize walls
        Debug.Log($"[MapManager] Initializing {width}x{height} grid with walls.");
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mapData[y, x] = 1;
                isRoom[y, x] = false;
            }
        }

        // 2. 諛??앹꽦
        Debug.Log("[MapManager] Step 2: Generating rooms...");
        GenerateRooms();
        
        // 3. 諛⑸뱾 ?곌껐
        Debug.Log("[MapManager] Step 3: Connecting rooms...");
        ConnectRooms();

        // 4. ?섎㉧吏 怨듦컙??誘몃줈 ?앹꽦 (諛⑷낵 ?듬줈? ?곌껐?섎룄濡?
        Debug.Log("[MapManager] Step 4: Generating maze connections...");
        GenerateMazeConnections();

        // 5. ?쒖옉 ?꾩튂 ?ㅼ젙 (泥?踰덉㎏ 諛⑹쓽 以묒떖)
        if (rooms.Count > 0)
        {
            var firstRoom = rooms[0];
            startPos = new Vector2Int(
                firstRoom.rect.x + firstRoom.rect.width / 2,
                firstRoom.rect.y + firstRoom.rect.height / 2
            );
        }
        else
        {
            // 諛⑹씠 ?놁쑝硫?湲곗〈 濡쒖쭅 ?ъ슜
            startPos = new Vector2Int(width / 2, height / 2);
            if (startPos.x % 2 == 0) startPos.x++;
            if (startPos.y % 2 == 0) startPos.y++;

            if (startPos.x >= width) startPos.x -= 2;
            if (startPos.y >= height) startPos.y -= 2;

            Carve(startPos.x, startPos.y);
        }

        // 6. 異쒓뎄(?ㅼ쓬 痢? ?꾩튂 ?좎젙: ?쒖옉?먯뿉??媛??癒?湲?0) ?
        exitPos = FindFarthestFloorCell(startPos);
        Debug.Log($"[MapManager] Start at {startPos}, Exit at {exitPos}");

        UnityEngine.Random.state = prevState;
    }

    void GenerateRooms()
    {
        rooms.Clear();
        int roomCount = UnityEngine.Random.Range(minRoomCount, maxRoomCount + 1);
        int attempts = 0;
        int targetSpecialRoomCount = Mathf.Max(1, UnityEngine.Random.Range(minSpecialRoomCount, maxSpecialRoomCount + 1));
        int specialRoomCreatedCount = 0;
        int maxAttempts = 500; // ?쒕룄 ?잛닔瑜?????섎┝

        for (int i = 0; i < roomCount && attempts < maxAttempts; attempts++)
        {
            // ?쒕뜡 諛??ш린
            int roomWidth = UnityEngine.Random.Range(minRoomSize, maxRoomSize + 1);
            int roomHeight = UnityEngine.Random.Range(minRoomSize, maxRoomSize + 1);

            // ?쒕뜡 ?꾩튂 (寃쎄퀎 怨좊젮)
            // ???醫뚰몴瑜??ъ슜?섏뿬 誘몃줈 洹몃━?쒖? ?쇱튂?쒗궡 (以묒슂!)
            // 踰붿쐞 泥댄겕 異붽?: 理쒖냼媛믪씠 理쒕?媛믩낫???묒븘????
            // x + roomWidth <= width, y + roomHeight <= height 蹂댁옣 ?꾩슂
            int maxX = width - roomWidth - 1; // x + roomWidth媛 width瑜??섏? ?딅룄濡?
            int maxY = height - roomHeight - 1; // y + roomHeight媛 height瑜??섏? ?딅룄濡?
            if (maxX < 1 || maxY < 1)
            {
                Debug.LogWarning($"[MapManager] 留??ш린媛 ?덈Т ?묎굅??諛⑹씠 ?덈Т ?? width={width}, height={height}, roomWidth={roomWidth}, roomHeight={roomHeight}");
                break; // ???댁긽 諛⑹쓣 ?앹꽦?????놁쓬
            }
            
            // ???醫뚰몴濡??쒗븳?섎㈃??踰붿쐞 ?댁뿉 ?덈룄濡?
            int maxXRange = maxX / 2;
            int maxYRange = maxY / 2;
            
            if (maxXRange < 1 || maxYRange < 1)
            {
                Debug.LogWarning($"[MapManager] ???醫뚰몴 ?쒖빟?쇰줈 ?명빐 諛⑹쓣 ?앹꽦?????놁쓬. maxX={maxX}, maxY={maxY}");
                break;
            }
            
            int x = UnityEngine.Random.Range(1, maxXRange + 1) * 2 + 1;
            int y = UnityEngine.Random.Range(1, maxYRange + 1) * 2 + 1;
            
            // 理쒖쥌 踰붿쐞 泥댄겕 (?덉쟾?μ튂)
            if (x + roomWidth > width || y + roomHeight > height)
            {
                Debug.LogWarning($"[MapManager] 怨꾩궛??諛??꾩튂媛 踰붿쐞瑜?踰쀬뼱?? x={x}, y={y}, roomWidth={roomWidth}, roomHeight={roomHeight}, width={width}, height={height}");
                continue; // ?대쾲 ?쒕룄??嫄대꼫?곌퀬 ?ㅼ떆 ?쒕룄
            }

            RectInt newRoomRect = new RectInt(x, y, roomWidth, roomHeight);

            // 湲곗〈 諛⑷낵 寃뱀튂?붿? 泥댄겕
            bool overlaps = false;
            foreach (var room in rooms)
            {
                // 媛꾧꺽??怨좊젮???뺤옣???곸뿭 泥댄겕
                RectInt expanded = new RectInt(
                    room.rect.x - roomSpacing,
                    room.rect.y - roomSpacing,
                    room.rect.width + roomSpacing * 2,
                    room.rect.height + roomSpacing * 2
                );
                
                if (expanded.Overlaps(newRoomRect))
                {
                    overlaps = true;
                    break;
                }
            }

            // 寃뱀튂吏 ?딆쑝硫?異붽?
            if (!overlaps)
            {
                // ?뱀닔 諛?遊됱씤??諛????곗꽑?곸쑝濡??앹꽦, ?섎㉧吏???뺣쪧???곕씪 寃곗젙
                bool isSealed = false;
                if (specialRoomCreatedCount < targetSpecialRoomCount)
                {
                    isSealed = true;
                    specialRoomCreatedCount++;
                }
                else
                {
                    isSealed = UnityEngine.Random.value < sealedRoomChance;
                }

                RoomInfo newRoom = new RoomInfo(newRoomRect, isSealed);

                // 諛??대?瑜?湲?0)濡?留뚮뱾怨?蹂댄샇 援ъ뿭?쇰줈 ?ㅼ젙
                // 寃쎄퀎 泥댄겕 異붽?: 諛곗뿴 踰붿쐞瑜?踰쀬뼱?섏? ?딅룄濡?
                for (int ry = newRoom.rect.y; ry < newRoom.rect.y + newRoom.rect.height; ry++)
                {
                    if (ry < 0 || ry >= height) continue; // y 踰붿쐞 泥댄겕
                    for (int rx = newRoom.rect.x; rx < newRoom.rect.x + newRoom.rect.width; rx++)
                    {
                        if (rx < 0 || rx >= width) continue; // x 踰붿쐞 泥댄겕
                        mapData[ry, rx] = 0;
                        isRoom[ry, rx] = true;
                    }
                }

                // ?꾩쟾???ロ엺 諛⑹씤 寃쎌슦: 諛?二쇰???踰쎌쑝濡??섎윭?멸퀬 臾??섎굹留?留뚮뱾湲?
                if (isSealed)
                {
                    CreateSealedRoom(newRoom);
                }

                rooms.Add(newRoom);
                i++; // ?깃났??諛?媛쒖닔 利앷?
            }
        }

        int sealedCount = 0;
        foreach (var room in rooms)
        {
            if (room.isSealed) sealedCount++;
        }
        Debug.Log($"?앹꽦??諛?媛쒖닔: {rooms.Count} (?쇰컲: {rooms.Count - sealedCount}媛? ?ロ엺 諛? {sealedCount}媛?)");
    }

    void CreateSealedRoom(RoomInfo room)
    {
        // 諛?二쇰???踰쎌쑝濡??섎윭?멸퀬 蹂댄샇 援ъ뿭?쇰줈 ?ㅼ젙
        // 遺곸そ 踰?
        for (int x = room.rect.x - 1; x <= room.rect.x + room.rect.width; x++)
        {
            if (x >= 0 && x < width && room.rect.y - 1 >= 0 && room.rect.y - 1 < height)
            {
                mapData[room.rect.y - 1, x] = 1;
                isRoom[room.rect.y - 1, x] = true;
            }
        }
        // ?⑥そ 踰?
        for (int x = room.rect.x - 1; x <= room.rect.x + room.rect.width; x++)
        {
            if (x >= 0 && x < width && room.rect.y + room.rect.height >= 0 && room.rect.y + room.rect.height < height)
            {
                mapData[room.rect.y + room.rect.height, x] = 1;
                isRoom[room.rect.y + room.rect.height, x] = true;
            }
        }
        // ?쒖そ 踰?
        for (int y = room.rect.y - 1; y <= room.rect.y + room.rect.height; y++)
        {
            if (room.rect.x - 1 >= 0 && room.rect.x - 1 < width && y >= 0 && y < height)
            {
                mapData[y, room.rect.x - 1] = 1;
                isRoom[y, room.rect.x - 1] = true;
            }
        }
        // ?숈そ 踰?
        for (int y = room.rect.y - 1; y <= room.rect.y + room.rect.height; y++)
        {
            if (room.rect.x + room.rect.width >= 0 && room.rect.x + room.rect.width < width && y >= 0 && y < height)
            {
                mapData[y, room.rect.x + room.rect.width] = 1;
                isRoom[y, room.rect.x + room.rect.width] = true;
            }
        }

        // 臾??섎굹 留뚮뱾湲?(?쒕뜡????硫댁뿉)
        // 0: 遺? 1: ?? 2: ?? 3: ??        // 踰붿쐞 ?댁뿉 ?덈뒗 硫대쭔 ?좏깮?섎룄濡??쒕룄
        List<int> validSides = new List<int>();
        if (room.rect.y > 0) validSides.Add(0); // 遺곸そ 媛??
        if (room.rect.x + room.rect.width < width) validSides.Add(1); // ?숈そ 媛??
        if (room.rect.y + room.rect.height < height) validSides.Add(2); // ?⑥そ 媛??
        if (room.rect.x > 0) validSides.Add(3); // ?쒖そ 媛??
        if (validSides.Count == 0)
        {
            Debug.LogWarning($"[MapManager] CreateSealedRoom: 諛⑹씠 留?寃쎄퀎???덈Т 媛源뚯썙 臾몄쓣 留뚮뱾 ???놁쓬. room={room.rect}");
            return; // 臾몄쓣 留뚮뱾 ???놁쑝硫?洹몃깷 諛섑솚
        }
        
        int doorSide = validSides[UnityEngine.Random.Range(0, validSides.Count)];
        Vector2Int doorPos = Vector2Int.zero;

        switch (doorSide)
        {
            case 0: // 遺곸そ
                doorPos = new Vector2Int(
                    room.rect.x + UnityEngine.Random.Range(0, room.rect.width),
                    room.rect.y - 1
                );
                break;
            case 1: // ?숈そ
                doorPos = new Vector2Int(
                    room.rect.x + room.rect.width,
                    room.rect.y + UnityEngine.Random.Range(0, room.rect.height)
                );
                break;
            case 2: // ?⑥そ
                doorPos = new Vector2Int(
                    room.rect.x + UnityEngine.Random.Range(0, room.rect.width),
                    room.rect.y + room.rect.height
                );
                break;
            case 3: // ?쒖そ
                doorPos = new Vector2Int(
                    room.rect.x - 1,
                    room.rect.y + UnityEngine.Random.Range(0, room.rect.height)
                );
                break;
        }

        // 臾??꾩튂瑜?湲?0)濡?留뚮뱾怨?蹂댄샇 ?댁젣 (?듬줈? ?곌껐?????덈룄濡?
        // 異붽? ?덉쟾?μ튂: 踰붿쐞 泥댄겕
        if (doorPos.x >= 0 && doorPos.x < width && doorPos.y >= 0 && doorPos.y < height)
        {
            mapData[doorPos.y, doorPos.x] = 0;
            isRoom[doorPos.y, doorPos.x] = false; // 臾몄? 蹂댄샇?섏? ?딆쓬
            room.doorPos = doorPos;
        }
        else
        {
            Debug.LogWarning($"[MapManager] CreateSealedRoom: 怨꾩궛??臾??꾩튂媛 踰붿쐞瑜?踰쀬뼱?? doorPos={doorPos}, width={width}, height={height}");
        }
    }

    void ConnectRooms()
    {
        Debug.Log($"ConnectRooms ?쒖옉 - 諛?媛쒖닔: {rooms.Count}");
        
        if (rooms.Count < 2)
        {
            Debug.Log("諛⑹씠 2媛?誘몃쭔?대?濡??곌껐 ?ㅽ궢");
            // 諛⑹씠 1媛쒕쭔 ?덉뼱???곌껐 泥댄겕???댁빞 ??
            EnsureAllConnected();
            return;
        }

        // 媛?諛⑹쓽 以묒떖??怨꾩궛 (?ロ엺 諛⑹? 臾??꾩튂 ?ъ슜)
        List<Vector2Int> roomCenters = new List<Vector2Int>();
        foreach (var room in rooms)
        {
            if (room.isSealed && room.doorPos != Vector2Int.zero)
            {
                // ?ロ엺 諛⑹? 臾??꾩튂瑜??곌껐?먯쑝濡??ъ슜
                roomCenters.Add(room.doorPos);
            }
            else
            {
                // ?쇰컲 諛⑹? 以묒떖???ъ슜
                roomCenters.Add(new Vector2Int(
                    room.rect.x + room.rect.width / 2,
                    room.rect.y + room.rect.height / 2
                ));
            }
        }
        
        Debug.Log($"諛?以묒떖??怨꾩궛 ?꾨즺: {roomCenters.Count}媛?");

        // 紐⑤뱺 諛⑹쓣 ?곌껐?섎뒗 理쒖냼 ?좎옣 ?몃━ 諛⑹떇
        // ?대? ?곌껐??諛⑸뱾??異붿쟻
        List<int> connected = new List<int> { 0 }; // 泥?踰덉㎏ 諛⑹? ?대? ?곌껐??
        List<int> unconnected = new List<int>();
        for (int i = 1; i < roomCenters.Count; i++)
        {
            unconnected.Add(i);
        }

        // 紐⑤뱺 諛⑹씠 ?곌껐???뚭퉴吏 諛섎났
        int safety = 0;
        int maxSafety = roomCenters.Count * 2;
        
        Debug.Log($"[MapManager] Starting room connection loop. Unconnected rooms: {unconnected.Count}");
        
        while (unconnected.Count > 0 && safety < maxSafety)
        {
            safety++;
            float minDistance = float.MaxValue;
            int closestConnected = -1;
            int closestUnconnected = -1;

            // ?곌껐??諛⑷낵 ?곌껐?섏? ?딆? 諛?以?媛??媛源뚯슫 ??李얘린
            foreach (int connectedIdx in connected)
            {
                foreach (int unconnectedIdx in unconnected)
                {
                    float dist = Vector2Int.Distance(roomCenters[connectedIdx], roomCenters[unconnectedIdx]);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestConnected = connectedIdx;
                        closestUnconnected = unconnectedIdx;
                    }
                }
            }

            // 媛??媛源뚯슫 諛⑸뱾???곌껐
            if (closestConnected >= 0 && closestUnconnected >= 0)
            {
                Debug.Log($"[MapManager] Connecting room {closestConnected} to room {closestUnconnected} (Distance: {minDistance})");
                CreateCorridor(roomCenters[closestConnected], roomCenters[closestUnconnected]);
                connected.Add(closestUnconnected);
                unconnected.Remove(closestUnconnected);
            }
            else
            {
                Debug.LogWarning("[MapManager] 諛??곌껐 ?ㅽ뙣 - 媛??媛源뚯슫 諛⑹쓣 李얠쓣 ???놁쓬");
                break; // ?곌껐 ?ㅽ뙣 ??以묐떒
            }
        }
        
        if (safety >= maxSafety)
        {
            Debug.LogError($"[MapManager] ConnectRooms safety break triggered! (safety: {safety}, maxSafety: {maxSafety})");
        }
        Debug.Log($"諛??곌껐 ?꾨즺 - ?곌껐??諛? {connected.Count}媛?");
        
        // 紐⑤뱺 湲?0) ????곌껐?섏뼱 ?덈뒗吏 ?뺤씤?섍퀬, 怨좊┰???곸뿭???덉쑝硫??곌껐
        
        EnsureAllConnected();
    }

    void EnsureAllConnected()
    {
        Debug.Log("=== EnsureAllConnected ?쒖옉 ===");
        
        // 紐⑤뱺 湲?0) ? 李얘린
        List<Vector2Int> allFloorCells = new List<Vector2Int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (mapData[y, x] == 0)
                {
                    allFloorCells.Add(new Vector2Int(x, y));
                }
            }
        }

        Debug.Log($"珥?湲?0) ? 媛쒖닔: {allFloorCells.Count}");
        
        if (allFloorCells.Count == 0)
        {
            Debug.LogWarning("湲?0) ????섎굹???놁뒿?덈떎!");
            return;
        }

        // BFS濡??곌껐???곸뿭 李얘린
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        List<List<Vector2Int>> regions = new List<List<Vector2Int>>();

        foreach (var cell in allFloorCells)
        {
            if (visited.Contains(cell)) continue;

            // ?덈줈???곸뿭 諛쒓껄
            List<Vector2Int> region = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(cell);
            visited.Add(cell);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                region.Add(current);

                // 4諛⑺뼢 泥댄겕 (寃쎄퀎 泥댄겕 ?꾪솕)
                Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach (var dir in dirs)
                {
                    Vector2Int next = current + dir;
                    if (next.x >= 0 && next.x < width && next.y >= 0 && next.y < height &&
                        mapData[next.y, next.x] == 0 && 
                        !visited.Contains(next))
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }

            regions.Add(region);
        }

        // ?щ윭 ?곸뿭???덉쑝硫??곌껐 (理쒕? 10踰?諛섎났?쇰줈 臾댄븳 猷⑦봽 諛⑹?)
        int maxIterations = 10;
        int iteration = 0;
        
        while (regions.Count > 1 && iteration < maxIterations)
        {
            iteration++;
            Debug.Log($"怨좊┰???곸뿭 諛쒓껄: {regions.Count}媛? ?곌껐 以?.. (諛섎났 {iteration})");
            
            // 泥?踰덉㎏ ?곸뿭??湲곗??쇰줈 ?섎㉧吏 ?곸뿭?ㅼ쓣 ?곌껐
            for (int i = 1; i < regions.Count; i++)
            {
                // 媛??곸뿭?먯꽌 媛??媛源뚯슫 ? 李얘린
                Vector2Int closest1 = regions[0][0];
                Vector2Int closest2 = regions[i][0];
                float minDist = float.MaxValue;

                foreach (var cell1 in regions[0])
                {
                    foreach (var cell2 in regions[i])
                    {
                        // ?ㅼ젣 嫄곕━媛 ?꾨땲???앹떆 嫄곕━瑜??ъ슜?섏뿬 蹂듬룄? ?????댁슱由ш쾶 ??
                        float dist = Mathf.Abs(cell1.x - cell2.x) + Mathf.Abs(cell1.y - cell2.y);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            closest1 = cell1;
                            closest2 = cell2;
                        }
                    }
                }

                // ???곸뿭???듬줈濡??곌껐
                CreateCorridor(closest1, closest2);
            }

            // CreateCorridor ?몄텧 ??留듭씠 蹂寃쎈릺?덉쑝誘濡?allFloorCells瑜??ㅼ떆 怨꾩궛?댁빞 ??            
            allFloorCells.Clear();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mapData[y, x] == 0)
                    {
                        allFloorCells.Add(new Vector2Int(x, y));
                    }
                }
            }

            // ?ㅼ떆 ?곸뿭 泥댄겕
            visited.Clear();
            regions.Clear();
            
            foreach (var cell in allFloorCells)
            {
                if (visited.Contains(cell)) continue;

                List<Vector2Int> region = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(cell);
                visited.Add(cell);

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    region.Add(current);

                    Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                    foreach (var dir in dirs)
                    {
                        Vector2Int next = current + dir;
                        if (next.x >= 0 && next.x < width && next.y >= 0 && next.y < height &&
                            mapData[next.y, next.x] == 0 && 
                            !visited.Contains(next))
                        {
                            visited.Add(next);
                            queue.Enqueue(next);
                        }
                    }
                }

                regions.Add(region);
            }
        }

        Debug.Log($"理쒖쥌 ?곸뿭 媛쒖닔: {regions.Count}");
        
        if (regions.Count > 1)
        {
            Debug.LogWarning($"寃쎄퀬: {regions.Count}媛쒖쓽 怨좊┰???곸뿭???⑥븘?덉뒿?덈떎.");
        }
        else if (regions.Count == 1)
        {
            Debug.Log("??紐⑤뱺 ?곸뿭???곌껐?섏뿀?듬땲??");
        }
        else
        {
            Debug.LogWarning("寃쎄퀬: ?곸뿭???놁뒿?덈떎!");
        }
        
        Debug.Log("=== EnsureAllConnected ?꾨즺 ===");
    }

    void GenerateMazeConnections()
    {
        Debug.Log("=== GenerateMazeConnections ?쒖옉 ===");
        
        // 留??꾩껜瑜??쒗쉶?섎ŉ ?꾩쭅 諛⑸Ц?섏? ?딆?(踰쎌씤) ???醫뚰몴?먯꽌 誘몃줈 ?쒖옉
        for (int y = 1; y < height - 1; y += 2)
        {
            for (int x = 1; x < width - 1; x += 2)
            {
                if (mapData[y, x] == 1 && !isRoom[y, x])
                {
                    Carve(x, y);
                }
            }
        }
        
        Debug.Log("=== GenerateMazeConnections ?꾨즺 ===");
    }

    void CreateCorridor(Vector2Int start, Vector2Int end)
    {
        // L??寃쎈줈濡??곌껐 (?섑룊 癒쇱?, 洹??ㅼ쓬 ?섏쭅)
        // ?섑룊 寃쎈줈
        int xStart = Mathf.Min(start.x, end.x);
        int xEnd = Mathf.Max(start.x, end.x);
        for (int x = xStart; x <= xEnd; x++)
        {
            if (x >= 0 && x < width && start.y >= 0 && start.y < height)
            {
                // 蹂듬룄??蹂댄샇 援ъ뿭??臾댁떆??(紐⑤뱺 援ъ뿭???곌껐???곗꽑)
                mapData[start.y, x] = 0;
            }
        }

        // ?섏쭅 寃쎈줈
        int yStart = Mathf.Min(start.y, end.y);
        int yEnd = Mathf.Max(start.y, end.y);
        for (int y = yStart; y <= yEnd; y++)
        {
            if (end.x >= 0 && end.x < width && y >= 0 && y < height)
            {
                // 蹂듬룄??蹂댄샇 援ъ뿭??臾댁떆??(紐⑤뱺 援ъ뿭???곌껐???곗꽑)
                mapData[y, end.x] = 0;
            }
        }
    }

    void PlacePlayer()
    {
        try
        {
            // 3. Teleport Player (Safe to do here because DrawMap has already filled the fog)
            DungeonGridPlayer player = Object.FindFirstObjectByType<DungeonGridPlayer>();
            if (player != null)
            {
                if (fogTilemap != null) player.fogTilemap = fogTilemap;
                
                if (DungeonPersistentData.hasSavedState)
                {
                    // ??λ맂 ?꾩튂媛 ?좏슚?쒖? ?뺤씤
                    if (DungeonPersistentData.lastPlayerGridPos.x >= 0 && 
                        DungeonPersistentData.lastPlayerGridPos.y >= 0 &&
                        DungeonPersistentData.lastPlayerGridPos.x < width &&
                        DungeonPersistentData.lastPlayerGridPos.y < height)
                    {
                        player.Teleport(DungeonPersistentData.lastPlayerGridPos);
                        player.facing = DungeonPersistentData.lastPlayerFacing;
                        Debug.Log($"[MapManager] Restored player to ({DungeonPersistentData.lastPlayerGridPos.x}, {DungeonPersistentData.lastPlayerGridPos.y}) facing {player.facing}");
                    }
                    else
                    {
                        Debug.LogWarning($"[MapManager] Saved position invalid, using startPos instead. Saved: {DungeonPersistentData.lastPlayerGridPos}, Start: {startPos}");
                        player.Teleport(startPos);
                    }
                }
                else
                {
                    // startPos媛 ?좏슚?쒖? ?뺤씤
                    if (startPos.x >= 0 && startPos.y >= 0 && startPos.x < width && startPos.y < height)
                    {
                        player.Teleport(startPos);
                        Debug.Log($"[MapManager] Placing player at start: {startPos}");
                    }
                    else
                    {
                        Debug.LogWarning($"[MapManager] startPos invalid ({startPos}), using default position (1, 1)");
                        player.Teleport(new Vector2Int(1, 1));
                    }
                }
            }
            else
            {
                Debug.LogWarning("No DungeonGridPlayer found to place at start.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MapManager] Error in PlacePlayer(): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    void Carve(int x, int y)
    {
        // 踰붿쐞 泥댄겕
        if (!InBounds(x, y))
        {
            Debug.LogWarning($"[MapManager] Carve: 踰붿쐞瑜?踰쀬뼱??醫뚰몴. x={x}, y={y}");
            return;
        }
        
        mapData[y, x] = 0; 

        List<Vector2Int> dirs = new List<Vector2Int>
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        for (int i = 0; i < dirs.Count; i++)
        {
            Vector2Int temp = dirs[i];
            int rand = UnityEngine.Random.Range(i, dirs.Count);
            dirs[i] = dirs[rand];
            dirs[rand] = temp;
        }

        foreach (var dir in dirs)
        {
            int nx = x + dir.x * 2;
            int ny = y + dir.y * 2;
            int midX = x + dir.x;
            int midY = y + dir.y;

            // 蹂댄샇?????isRoom)? ?뚭퀬?ㅼ? ?딆쓬
            if (InBounds(nx, ny) && mapData[ny, nx] == 1 && !isRoom[ny, nx])
            {
                // 以묎컙 踰쎈룄 踰붿쐞 泥댄겕 ???쒓굅
                if (InBounds(midX, midY))
                {
                    mapData[midY, midX] = 0;
                }
                Carve(nx, ny);
            }
        }
    }

    bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    void DrawMap()
    {
        // null 泥댄겕
        if (floorTilemap == null)
        {
            Debug.LogError("[MapManager] DrawMap: floorTilemap is null!");
            return;
        }
        if (wallTilemap == null)
        {
            Debug.LogError("[MapManager] DrawMap: wallTilemap is null!");
            return;
        }
        if (mapData == null)
        {
            Debug.LogError("[MapManager] DrawMap: mapData is null! GenerateMaze() must be called first.");
            return;
        }
        
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        if (fogTilemap != null) fogTilemap.ClearAllTiles();

        int floorCount = 0;
        int wallCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3Int pos = new Vector3Int(x, -y, 0);

                bool isExit = (x == exitPos.x && y == exitPos.y);

                if (fogTilemap != null && fogTile != null)
                {
                    // ?먰뿕???곸뿭?몄? ?뺤씤?섏뿬 ?덇컻 ?ㅼ젙
                    if (DungeonPersistentData.revealedTiles.Contains(new Vector2Int(x, y)))
                    {
                        fogTilemap.SetTile(pos, null);
                    }
                    else
                    {
                        // ?덇컻 ?좎? (異쒓뎄??媛由щ뒗 寃껋씠 湲곕낯)
                        fogTilemap.SetTile(pos, fogTile);
                    }
                }

                if (mapData[y, x] == 1)
                {
                    wallTilemap.SetTile(pos, wallTile);
                    wallCount++;
                }
                else
                {
                    // 湲곕낯 諛붾떏??源먮떎 (?댄썑 異쒓뎄硫??ㅻⅨ ??쇰줈 ??쓣 ???덉쓬)
                    floorTilemap.SetTile(pos, floorTile);
                    // ?됱긽 ?곸슜???꾪빐 LockColor ?댁젣 (湲곕낯 諛붾떏)
                    floorTilemap.SetTileFlags(pos, TileFlags.None);

                    // 異쒓뎄 ?쒖떆 (怨꾨떒 ????먮뒗 ?됱긽 ?섏씠?쇱씠??
                    if (isExit)
                    {
                        // 諛앹? ?꾩슜 ??쇱씠 ?덉쑝硫??곗꽑 ?ъ슜, ?놁쑝硫?怨꾨떒/諛붾떏 ?쒖쑝濡??좏깮
                        if (exitHighlightTile != null)
                        {
                            floorTilemap.SetTile(pos, exitHighlightTile);
                        }
                        else if (stairsTile != null)
                        {
                            floorTilemap.SetTile(pos, stairsTile);
                        }

                        // 怨꾨떒 ??쇱쓣 ??뿀?쇰?濡??ㅼ떆 LockColor ?댁젣
                        floorTilemap.SetTileFlags(pos, TileFlags.None);

                        if (highlightExit)
                        {
                            // 怨꾨떒 ??쇱씠 ?덈뱺 ?녿뱺 ?됱긽 ?곸슜 (怨꾨떒 ?ㅽ봽?쇱씠?멸? ?⑥깋?대㈃ ??뼱?)
                            floorTilemap.SetColor(pos, exitColor);
                            // RefreshTile? 猷⑦봽 ?앹뿉????踰덈쭔 ?몄텧?섎룄濡?理쒖쟻??
                            Debug.Log($"[MapManager] Exit highlighted at {exitPos} with color {exitColor}");
                        }
                        else
                        {
                            floorTilemap.SetColor(pos, Color.white);
                            // RefreshTile? 猷⑦봽 ?앹뿉????踰덈쭔 ?몄텧?섎룄濡?理쒖쟻??
                        }
                    }
                    else
                    {
                        // ?쇰컲 諛붾떏? 湲곕낯?됱쑝濡?
                        floorTilemap.SetColor(pos, Color.white);
                        // RefreshTile? 猷⑦봽 ?앹뿉????踰덈쭔 ?몄텧?섎룄濡?理쒖쟻??
                    }
                    floorCount++;
                }
            }
        }

        // 理쒖쥌?곸쑝濡?異쒓뎄 ?됱긽???ㅼ떆 ??踰?媛뺤젣 ?곸슜 (?뱀떆 以묎컙???????寃쎌슦 ?鍮?
        ForceExitHighlight();
        
        // 紐⑤뱺 ????낅뜲?댄듃瑜???踰덉뿉 泥섎━ (?깅뒫 理쒖쟻??
        floorTilemap.RefreshAllTiles();
        wallTilemap.RefreshAllTiles();
        if (fogTilemap != null) fogTilemap.RefreshAllTiles();

        Debug.Log($"留??앹꽦 ?꾨즺: ?ш린 {width}x{height} = {width * height}媛?? (湲? {floorCount}媛? 踰? {wallCount}媛?)");
    }

    void ForceExitHighlight()
    {
        if (!highlightExit || floorTilemap == null) return;
        if (!InBounds(exitPos.x, exitPos.y)) return;

        Vector3Int pos = new Vector3Int(exitPos.x, -exitPos.y, 0);

        // 諛붾떏 ?놁쑝硫?諛붾떏遺??
        if (!floorTilemap.HasTile(pos))
        {
            TileBase baseTile = exitHighlightTile != null ? exitHighlightTile : (stairsTile != null ? stairsTile : floorTile);
            floorTilemap.SetTile(pos, baseTile);
        }
        else if (exitHighlightTile != null)
        {
            floorTilemap.SetTile(pos, exitHighlightTile);
        }
        else if (stairsTile != null)
        {
            // 怨꾨떒???ъ슜?섎룄濡???린
            floorTilemap.SetTile(pos, stairsTile);
        }

        // ???곸슜???꾪빐 LockColor ?댁젣 ???됱긽 媛뺤젣
        floorTilemap.SetTileFlags(pos, TileFlags.None);

        // ?뚰뙆媛 0??寃쎌슦 ?鍮? 理쒖냼 0.9濡?蹂댁젙
        Color c = exitColor;
        if (c.a < 0.05f) c.a = 1f;
        floorTilemap.SetColor(pos, c);

        // RefreshAllTiles??DrawMap()?먯꽌 ?대? ?몄텧?섎?濡??ш린?쒕뒗 ?앸왂
        Debug.Log($"[MapManager] ForceExitHighlight at {exitPos} color {c}");
    }

    // ?쒖옉 吏?먯뿉??媛??癒?湲?0) ? 李얘린 (留⑦빐??嫄곕━ 湲곗? BFS)
    Vector2Int FindFarthestFloorCell(Vector2Int start)
    {
        if (!InBounds(start.x, start.y) || mapData[start.y, start.x] != 0)
        {
            Debug.LogWarning($"[MapManager] FindFarthestFloorCell: ?쒖옉 ?꾩튂媛 ?좏슚?섏? ?딆쓬. startPos 諛섑솚: {startPos}");
            return startPos;
        }

        Queue<Vector2Int> q = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> dist = new Dictionary<Vector2Int, int>();
        q.Enqueue(start);
        dist[start] = 0;

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        Vector2Int far = start;
        int maxD = 0;
        
        // ?덉쟾?μ튂: 理쒕? ?먯깋 ?잛닔 ?쒗븳 (留??ш린??2諛?
        int maxSearchCount = width * height * 2;
        int searchCount = 0;

        while (q.Count > 0 && searchCount < maxSearchCount)
        {
            searchCount++;
            var cur = q.Dequeue();
            int d = dist[cur];
            if (d > maxD)
            {
                maxD = d;
                far = cur;
            }

            foreach (var dir in dirs)
            {
                Vector2Int next = cur + dir;
                if (!InBounds(next.x, next.y)) continue;
                if (mapData[next.y, next.x] != 0) continue;
                if (dist.ContainsKey(next)) continue;
                dist[next] = d + 1;
                q.Enqueue(next);
            }
        }
        
        if (searchCount >= maxSearchCount)
        {
            Debug.LogWarning($"[MapManager] FindFarthestFloorCell: 理쒕? ?먯깋 ?잛닔 ?꾨떖. ?꾩옱源뚯? 李얠? 媛??癒??꾩튂 諛섑솚: {far}");
        }

        return far;
    }
}


