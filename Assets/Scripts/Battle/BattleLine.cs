using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AbyssdawnBattle;

/// <summary>
/// 전투 라인 관리 클래스 (포지션 연산 중앙화)
/// 아군/적 각각의 4슬롯 라인을 관리하고, 위치 변경 연산을 담당합니다.
/// </summary>
public class BattleLine<T> where T : MonoBehaviour
{
    private T[] slots = new T[4]; // 인덱스 0-3 (슬롯 1-4에 대응)

    /// <summary>
    /// 슬롯 개수 (항상 4)
    /// </summary>
    public int SlotCount => 4;

    /// <summary>
    /// 전열 경계 인덱스 (슬롯 1,2 = 전열, 슬롯 3,4 = 후열)
    /// </summary>
    public int FrontRowBoundary => 2;

    /// <summary>
    /// 특정 슬롯에 캐릭터 할당
    /// </summary>
    public bool AssignToSlot(T character, int slotIndex)
    {
        if (slotIndex < 1 || slotIndex > 4)
        {
            Debug.LogWarning($"[BattleLine] Invalid slot index: {slotIndex}. Must be 1-4.");
            return false;
        }

        int arrayIndex = slotIndex - 1;
        slots[arrayIndex] = character;
        return true;
    }

    /// <summary>
    /// 특정 슬롯의 캐릭터 반환
    /// </summary>
    public T GetAt(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex > 4) return null;
        return slots[slotIndex - 1];
    }

    /// <summary>
    /// 특정 슬롯이 비어있는지 확인
    /// </summary>
    public bool IsEmpty(int slotIndex)
    {
        return GetAt(slotIndex) == null;
    }

    /// <summary>
    /// 빈 슬롯 찾기 (앞에서부터)
    /// </summary>
    public int FindEmptySlot()
    {
        for (int i = 1; i <= 4; i++)
        {
            if (IsEmpty(i)) return i;
        }
        return -1; // 빈 슬롯 없음
    }

    /// <summary>
    /// 전열의 모든 캐릭터 반환 (슬롯 1, 2)
    /// </summary>
    public IEnumerable<T> FrontRow()
    {
        return slots.Take(FrontRowBoundary).Where(c => c != null);
    }

    /// <summary>
    /// 후열의 모든 캐릭터 반환 (슬롯 3, 4)
    /// </summary>
    public IEnumerable<T> BackRow()
    {
        return slots.Skip(FrontRowBoundary).Where(c => c != null);
    }

    /// <summary>
    /// 모든 캐릭터 반환 (null 제외)
    /// </summary>
    public IEnumerable<T> AllCharacters()
    {
        return slots.Where(c => c != null);
    }

    /// <summary>
    /// 특정 SlotMask에 해당하는 캐릭터들 반환
    /// </summary>
    public List<T> GetCharactersInMask(SlotMask mask)
    {
        List<T> result = new List<T>();

        if ((mask & SlotMask.Slot1) != 0 && slots[0] != null)
            result.Add(slots[0]);
        if ((mask & SlotMask.Slot2) != 0 && slots[1] != null)
            result.Add(slots[1]);
        if ((mask & SlotMask.Slot3) != 0 && slots[2] != null)
            result.Add(slots[2]);
        if ((mask & SlotMask.Slot4) != 0 && slots[3] != null)
            result.Add(slots[3]);

        return result;
    }

    /// <summary>
    /// 두 슬롯의 캐릭터 교환
    /// </summary>
    public bool Swap(int slotIndex1, int slotIndex2)
    {
        if (slotIndex1 < 1 || slotIndex1 > 4 || slotIndex2 < 1 || slotIndex2 > 4)
            return false;

        T temp = slots[slotIndex1 - 1];
        slots[slotIndex1 - 1] = slots[slotIndex2 - 1];
        slots[slotIndex2 - 1] = temp;

        return true;
    }

    /// <summary>
    /// 한 슬롯에서 다른 슬롯으로 이동
    /// </summary>
    public bool Move(int fromSlot, int toSlot)
    {
        if (fromSlot < 1 || fromSlot > 4 || toSlot < 1 || toSlot > 4)
            return false;

        if (IsEmpty(fromSlot)) return false;

        T character = slots[fromSlot - 1];
        slots[fromSlot - 1] = null;
        slots[toSlot - 1] = character;

        return true;
    }

    /// <summary>
    /// 모든 캐릭터를 무작위로 섞기 (셔플)
    /// </summary>
    public void Shuffle()
    {
        List<T> characters = AllCharacters().ToList();
        List<int> emptyIndices = new List<int>();

        // 모든 슬롯 비우기
        for (int i = 0; i < 4; i++)
        {
            if (slots[i] != null)
            {
                emptyIndices.Add(i);
                slots[i] = null;
            }
        }

        // 무작위로 재배치
        for (int i = 0; i < characters.Count && i < emptyIndices.Count; i++)
        {
            int randomIndex = Random.Range(0, emptyIndices.Count);
            int targetSlot = emptyIndices[randomIndex];
            emptyIndices.RemoveAt(randomIndex);
            slots[targetSlot] = characters[i];
        }
    }

    /// <summary>
    /// 특정 캐릭터를 앞으로 이동 (가장 앞의 빈 슬롯으로)
    /// </summary>
    public bool PushForward(T character)
    {
        int emptySlot = FindEmptySlot();
        if (emptySlot == -1) return false;

        // 현재 위치 찾기
        int currentSlot = -1;
        for (int i = 0; i < 4; i++)
        {
            if (slots[i] == character)
            {
                currentSlot = i + 1;
                break;
            }
        }

        if (currentSlot == -1) return false;

        return Move(currentSlot, emptySlot);
    }

    /// <summary>
    /// 특정 캐릭터를 뒤로 이동 (가장 뒤의 빈 슬롯으로)
    /// </summary>
    public bool PushBackward(T character)
    {
        // 뒤에서부터 빈 슬롯 찾기
        int emptySlot = -1;
        for (int i = 4; i >= 1; i--)
        {
            if (IsEmpty(i))
            {
                emptySlot = i;
                break;
            }
        }

        if (emptySlot == -1) return false;

        // 현재 위치 찾기
        int currentSlot = -1;
        for (int i = 0; i < 4; i++)
        {
            if (slots[i] == character)
            {
                currentSlot = i + 1;
                break;
            }
        }

        if (currentSlot == -1) return false;

        return Move(currentSlot, emptySlot);
    }

    /// <summary>
    /// 특정 캐릭터의 현재 슬롯 인덱스 반환 (1-4)
    /// </summary>
    public int GetSlotIndex(T character)
    {
        for (int i = 0; i < 4; i++)
        {
            if (slots[i] == character)
                return i + 1;
        }
        return -1; // 찾을 수 없음
    }

    /// <summary>
    /// 특정 캐릭터를 슬롯에서 제거
    /// </summary>
    public bool Remove(T character)
    {
        for (int i = 0; i < 4; i++)
        {
            if (slots[i] == character)
            {
                slots[i] = null;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 모든 슬롯 초기화
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < 4; i++)
        {
            slots[i] = null;
        }
    }

    /// <summary>
    /// 현재 라인의 상태를 문자열로 반환 (디버그용)
    /// </summary>
    public override string ToString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("[");
        for (int i = 0; i < 4; i++)
        {
            if (slots[i] != null)
                sb.Append($"{i + 1}:{slots[i].name}");
            else
                sb.Append($"{i + 1}:Empty");
            
            if (i < 3) sb.Append(", ");
        }
        sb.Append("]");
        return sb.ToString();
    }
}





