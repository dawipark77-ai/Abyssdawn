using UnityEngine;

public class RoomInfo
{
    public RectInt rect;
    public bool isSealed;
    public Vector2Int doorPos;

    public RoomInfo(RectInt r, bool isSealed_)
    {
        rect = r;
        isSealed = isSealed_;
        doorPos = Vector2Int.zero;
    }
}































































