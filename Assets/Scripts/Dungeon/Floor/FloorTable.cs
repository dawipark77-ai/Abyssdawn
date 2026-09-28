using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 층 설정표 에셋. "몇 층부터 몇 층까지 어떤 크기·구조로 생성할지"를 표로 관리한다.
/// 100층·200층으로 늘릴 때 코드는 그대로 두고 이 표에 줄만 추가하면 된다.
///
/// 만들기: Project 창 우클릭 → Create → Abyssdawn → Dungeon → Floor Table (베타 10층 기본값으로 채워짐)
/// 사용:   던전 씬의 MapManager → Floor Table 칸에 연결. 비워두면 코드에 내장된 베타 기본값을 쓴다.
/// </summary>
[CreateAssetMenu(menuName = "Abyssdawn/Dungeon/Floor Table", fileName = "FloorTable")]
public class FloorTable : ScriptableObject
{
    [Tooltip("위에서부터 층 범위가 맞는 첫 줄을 쓴다. 어느 줄에도 없는 깊은 층은 가장 깊은 줄을 계속 쓴다.")]
    public List<FloorTableEntry> entries = new List<FloorTableEntry>();

    public FloorTableEntry GetEntry(int floor) => FloorTableDefaults.Find(entries, floor);

    // 에셋을 처음 만들 때 자동 호출
    private void Reset() => entries = FloorTableDefaults.CreateBeta();

    [ContextMenu("베타 10층 기본값으로 초기화")]
    private void ResetToBeta() => entries = FloorTableDefaults.CreateBeta();
}
