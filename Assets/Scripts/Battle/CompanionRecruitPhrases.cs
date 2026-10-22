using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 몬스터 영입 시 출력할 "영입 권유 문구"를 랜덤으로 제공하는 순수 데이터·함수 유틸.
    ///
    /// static 클래스로 분리한 이유:
    ///   - 순수 데이터(문구 배열) + 순수 함수(랜덤 + 포맷)라 MonoBehaviour일 필요 없음.
    ///   - 전투 씬(BattleManager)뿐 아니라 던전 씬 등 어디서든 호출 가능.
    ///   - 이미 동료 전용 static 유틸 CompanionPartyPersistence가 있어 같은 결로 분리.
    ///
    /// </summary>
    public static class CompanionRecruitPhrases
    {
        // [2026-10-22] 한국어로 (게임 전체 한국어). {0} = 이름, {I}/{Eun}/{Eul} = 이름 뒤 조사 (이/가 · 은/는 · 을/를)
        private static readonly string[] Phrases =
        {
            "심연에서 무언가가 당신을 따라온다.",
            "{0}{I} 당신 곁에 남고 싶어 한다.",
            "{0}{I} 당신에게 이끌린다.",
            "잊힌 무언가가 당신을 바라본다.",
            "{0}{I} 원래 당신의 일부였던 것처럼 다가온다.",
            "낯익은 그림자가 곁에 머물게 해 달라고 한다.",
            "{0}{Eun} 더는 혼자이고 싶지 않다.",
            "길 잃은 혼이 당신의 손을 기다린다.",
        };

        /// <summary>
        /// 8종 중 하나를 랜덤으로 골라 최종 문구를 반환한다.
        /// {0} 자리에는 색을 입힌 이름, {I}/{Eun}/{Eul} 에는 받침에 맞는 조사가 들어간다.
        /// </summary>
        /// <param name="monsterName">몬스터(동료) 이름 (색 태그 없이)</param>
        /// <param name="color">이름 색 (예: #FFD700). 비우면 색 없음</param>
        public static string GetRandomPhrase(string monsterName, string color = "#FFD700")
        {
            string template = Phrases[Random.Range(0, Phrases.Length)];
            string name = string.IsNullOrEmpty(color) ? monsterName : $"<color={color}>{monsterName}</color>";
            return template
                .Replace("{I}", Ko.I(monsterName).Substring(monsterName.Length))
                .Replace("{Eun}", Ko.Eun(monsterName).Substring(monsterName.Length))
                .Replace("{Eul}", Ko.Eul(monsterName).Substring(monsterName.Length))
                .Replace("{0}", name);
        }
    }
}
