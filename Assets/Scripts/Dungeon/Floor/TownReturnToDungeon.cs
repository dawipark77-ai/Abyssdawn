using UnityEngine;

/// <summary>
/// 마을 씬의 "던전으로" 버튼에 붙여 쓰는 컴포넌트. Button.onClick → ReturnToDungeon().
/// 떠났던 던전 층의 같은 자리(마을 입구 위)에서 이어진다.
/// </summary>
public class TownReturnToDungeon : MonoBehaviour
{
    [Tooltip("던전에서 오지 않고 마을 씬을 바로 실행했을 때 갈 던전 씬")]
    public string fallbackDungeonScene = "Abyssdawn_Dungeon_2D 07";

    public void ReturnToDungeon()
    {
        DungeonTownGate.ReturnToDungeon(fallbackDungeonScene);
    }
}
