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
    /// 현재 어디서도 호출하지 않음 — 호출 연결은 영입 연출 작업에서 수행.
    /// </summary>
    public static class CompanionRecruitPhrases
    {
        // 문구 8종. 일부는 몬스터 이름 자리({0}) 포함, 일부는 미포함.
        private static readonly string[] Phrases =
        {
            "Something from the abyss follows you.",
            "{0} wishes to remain by your side.",
            "{0} is drawn to you.",
            "Something forgotten gazes upon you.",
            "{0} approaches as if it were once part of you.",
            "A familiar shadow asks to stay beside you.",
            "{0} no longer wishes to be alone.",
            "A lost soul awaits your hand.",
        };

        /// <summary>
        /// 8종 중 하나를 랜덤으로 골라 최종 문구를 반환한다.
        /// 고른 문구에 {0}가 있으면 monsterName을 끼우고, 없으면 그대로 반환한다.
        /// </summary>
        /// <param name="monsterName">{0} 자리에 들어갈 몬스터(동료) 이름</param>
        public static string GetRandomPhrase(string monsterName)
        {
            string template = Phrases[Random.Range(0, Phrases.Length)];

            // {0} 포함 여부 판단 — 포함 시에만 string.Format으로 이름 삽입.
            if (template.Contains("{0}"))
            {
                return string.Format(template, monsterName);
            }
            return template;
        }
    }
}
