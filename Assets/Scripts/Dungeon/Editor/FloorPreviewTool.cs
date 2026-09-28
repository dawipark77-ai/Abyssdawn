using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 게임을 실행하지 않고 층 생성 결과를 PNG 로 미리 본다 (층 설정표 조정용).
/// 메뉴: Abyssdawn → Dungeon → 층 미리보기 (1~10층 PNG)
/// 결과: 프로젝트 폴더/FloorPreviews/ (Assets 밖이라 임포트되지 않음)
/// 색: 검정 암반 / 밝은 회색 방 / 어두운 회색 통로 / 주황 문 / 초록 시작 / 하늘 계단 / 노랑 마을 입구
/// </summary>
public static class FloorPreviewTool
{
    private const int CellPx = 8;
    private const int PreviewFloors = 10;

    [MenuItem("Abyssdawn/Dungeon/층 미리보기 (1~10층 PNG)")]
    private static void PreviewFloors1To10()
    {
        FloorTable table = FindFloorTable();
        int baseSeed = Random.Range(int.MinValue, int.MaxValue);
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "FloorPreviews");
        Directory.CreateDirectory(dir);

        for (int floor = 1; floor <= PreviewFloors; floor++)
        {
            FloorTableEntry entry = table != null && table.entries.Count > 0
                ? table.GetEntry(floor)
                : FloorTableDefaults.Find(null, floor);
            int seed = unchecked(baseSeed + floor * 7919);
            DungeonFloorData data = RogueFloorGenerator.Generate(floor, seed, entry);

            string path = Path.Combine(dir, $"B{floor:00}_{data.floorType}_{data.width}x{data.height}.png");
            File.WriteAllBytes(path, Render(data).EncodeToPNG());
            Debug.Log($"[FloorPreview] B{floor} {data.floorType} {data.width}x{data.height} seed {seed} → {path}\n{data.ToAscii(null)}");
        }

        Debug.Log($"[FloorPreview] {PreviewFloors}개 층 미리보기 저장 완료 ({(table != null ? table.name : "내장 베타 설정")}): {dir}");
        EditorUtility.RevealInFinder(dir);
    }

    private static FloorTable FindFloorTable()
    {
        string[] guids = AssetDatabase.FindAssets("t:FloorTable");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<FloorTable>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static Texture2D Render(DungeonFloorData data)
    {
        Color rock = new Color(0.06f, 0.06f, 0.08f);
        Color room = new Color(0.62f, 0.62f, 0.62f);
        Color corridor = new Color(0.36f, 0.36f, 0.4f);
        Color door = new Color(1f, 0.6f, 0.15f);
        Color start = new Color(0.3f, 0.95f, 0.35f);
        Color stairs = new Color(0.35f, 0.85f, 1f);
        Color gate = new Color(1f, 0.85f, 0.2f);

        int w = data.width * CellPx, h = data.height * CellPx;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];

        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                var p = new Vector2Int(x, y);
                FloorCell c = data.cells[x, y];
                Color col = c.terrain == FloorTerrain.Room ? room : c.terrain == FloorTerrain.Corridor ? corridor : rock;
                if (p == data.startPos) col = start;
                else if (c.feature == FloorFeature.StairsDown) col = stairs;
                else if (c.feature == FloorFeature.TownGate) col = gate;
                FillCell(pixels, w, h, x, y, col, 0);
            }
        }

        // 문: 통로 쪽 칸에 테두리 없이 주황으로 칠해 입구를 표시
        foreach (var d in data.doors) FillCell(pixels, w, h, d.corridorCell.x, d.corridorCell.y, door, 2);

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>격자 y는 아래로 증가, 텍스처 y는 위로 증가 → 뒤집어서 칠한다. inset = 칸 안쪽 여백 픽셀.</summary>
    private static void FillCell(Color[] pixels, int w, int h, int cx, int cy, Color col, int inset)
    {
        int px0 = cx * CellPx + inset, px1 = (cx + 1) * CellPx - inset;
        int ty1 = h - cy * CellPx - inset, ty0 = h - (cy + 1) * CellPx + inset;
        for (int py = ty0; py < ty1; py++)
            for (int px = px0; px < px1; px++)
                pixels[py * w + px] = col;
    }
}
