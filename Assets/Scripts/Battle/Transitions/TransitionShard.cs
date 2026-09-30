using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전환 연출용 조각 하나 — 캡처한 화면 텍스처의 다각형(볼록) 부분만 그린다.
/// 유리 깨짐(삼각형 조각), 발톱 베기(화면 두 쪽) 연출이 사용. 꼭짓점은 이 오브젝트 기준 로컬 좌표.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class TransitionShard : MaskableGraphic
{
    private Texture _texture;
    private Vector2[] _verts;
    private Vector2[] _uvs;

    public override Texture mainTexture => _texture != null ? _texture : s_WhiteTexture;

    /// <summary>verts = 로컬 좌표(볼록 다각형, 순서대로), uvs = 같은 순서의 텍스처 좌표(0~1).</summary>
    public void Setup(Texture texture, Vector2[] verts, Vector2[] uvs)
    {
        _texture = texture;
        _verts = verts;
        _uvs = uvs;
        raycastTarget = false;
        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_verts == null || _verts.Length < 3) return;
        for (int i = 0; i < _verts.Length; i++)
            vh.AddVert(_verts[i], color, _uvs[i]);
        for (int i = 1; i < _verts.Length - 1; i++)
            vh.AddTriangle(0, i, i + 1); // 부채꼴 분할 (볼록 다각형)
    }
}
