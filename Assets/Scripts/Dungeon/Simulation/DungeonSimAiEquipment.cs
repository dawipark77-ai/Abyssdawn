using System.Collections.Generic;
using System.Text;
using AbyssdawnBattle;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 던전 시뮬 — Resources/Crude 장비 풀 로드, 규칙에 맞는 조합 생성, 단순 점수로 AI 선택·적용.
    /// </summary>
    public static class DungeonSimAiEquipment
    {
        public const string CrudeResourcesPath = "Item_Equipments/Equipments/Crude";

        /// <summary>RunDungeonSimulation 시작 시 초기화 — 요약의 ‘첫/마지막 선택’ 샘플용.</summary>
        public static void ResetExplainSamples()
        {
            _explainSampleFirst = null;
            _explainSampleLast = null;
        }

        private static string _explainSampleFirst;
        private static string _explainSampleLast;

        private static void RegisterExplainSample(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (string.IsNullOrEmpty(_explainSampleFirst))
                _explainSampleFirst = line;
            _explainSampleLast = line;
        }

        /// <summary>이번 시뮬 실행 동안 기록된 첫/마지막 마을 장비 점수 설명(없으면 false).</summary>
        public static bool TryGetExplainSamples(out string first, out string last)
        {
            first = _explainSampleFirst;
            last = _explainSampleLast;
            return !string.IsNullOrEmpty(first) || !string.IsNullOrEmpty(last);
        }

        private static List<EquipmentData> _crudeAll;
        private static List<EquipmentData> _oneHandWeapons;
        private static List<EquipmentData> _twoHandWeapons;
        private static List<EquipmentData> _shields;
        private static List<EquipmentData> _armours;
        private static List<EquipmentData> _accessories;

        public static void EnsureCrudePoolLoaded()
        {
            if (_crudeAll != null) return;

            var arr = Resources.LoadAll<EquipmentData>(CrudeResourcesPath);
            _crudeAll = new List<EquipmentData>(arr ?? System.Array.Empty<EquipmentData>());
            _oneHandWeapons = new List<EquipmentData>();
            _twoHandWeapons = new List<EquipmentData>();
            _shields = new List<EquipmentData>();
            _armours = new List<EquipmentData>();
            _accessories = new List<EquipmentData>();

            foreach (var e in _crudeAll)
            {
                if (e == null) continue;
                switch (e.equipmentType)
                {
                    case EquipmentType.Hand:
                        if (e.blockData != null)
                            _shields.Add(e);
                        else if (e.isTwoHanded)
                            _twoHandWeapons.Add(e);
                        else
                            _oneHandWeapons.Add(e);
                        break;
                    case EquipmentType.TwoHanded:
                        _twoHandWeapons.Add(e);
                        break;
                    case EquipmentType.Armour:
                        _armours.Add(e);
                        break;
                    case EquipmentType.Accessory:
                        _accessories.Add(e);
                        break;
                }
            }
        }

        public static int GetMaxStrikeThreatForFloor(DungeonSimMonsterPool pool, int floor)
        {
            if (pool == null || floor < 1) return 0;
            var entry = pool.GetEntryForFloor(floor);
            if (entry?.monsters == null) return 0;
            int max = 0;
            for (int i = 0; i < entry.monsters.Count; i++)
            {
                var m = entry.monsters[i]?.monster;
                if (m == null) continue;
                max = Mathf.Max(max, Mathf.Max(m.ATK, m.MAG));
            }
            return max;
        }

        private readonly struct Loadout
        {
            public readonly EquipmentData Rh;
            public readonly EquipmentData Lh;
            public readonly EquipmentData Body;
            public readonly EquipmentData Acc1;
            public readonly EquipmentData Acc2;

            public Loadout(EquipmentData rh, EquipmentData lh, EquipmentData body, EquipmentData acc1, EquipmentData acc2)
            {
                Rh = rh;
                Lh = lh;
                Body = body;
                Acc1 = acc1;
                Acc2 = acc2;
            }

            public string ToSignature()
            {
                var sb = new StringBuilder(128);
                sb.Append("RH=").Append(SlotName(Rh)).Append("|LH=").Append(SlotName(Lh))
                    .Append("|BD=").Append(SlotName(Body)).Append("|A1=").Append(SlotName(Acc1))
                    .Append("|A2=").Append(SlotName(Acc2));
                return sb.ToString();
            }

            private static string SlotName(EquipmentData e) =>
                e == null ? "-" : (string.IsNullOrEmpty(e.equipmentName) ? e.name : e.equipmentName);
        }

        private static IEnumerable<Loadout> EnumerateHandBodiesAccessories()
        {
            EnsureCrudePoolLoaded();

            var hands = new List<(EquipmentData rh, EquipmentData lh)>();
            hands.Add((null, null));
            foreach (var t in _twoHandWeapons)
                hands.Add((t, null));

            for (int i = 0; i < _oneHandWeapons.Count; i++)
            {
                for (int j = i; j < _oneHandWeapons.Count; j++)
                {
                    var a = _oneHandWeapons[i];
                    var b = _oneHandWeapons[j];
                    hands.Add((a, b));
                    if (i != j)
                        hands.Add((b, a));
                }
            }

            foreach (var w in _oneHandWeapons)
            {
                foreach (var s in _shields)
                {
                    hands.Add((w, s));
                }
            }

            var bodies = new List<EquipmentData> { null };
            bodies.AddRange(_armours);

            var accPairs = new List<(EquipmentData, EquipmentData)>();
            accPairs.Add((null, null));
            foreach (var a in _accessories)
                accPairs.Add((a, null));
            for (int i = 0; i < _accessories.Count; i++)
            {
                for (int j = i; j < _accessories.Count; j++)
                    accPairs.Add((_accessories[i], _accessories[j]));
            }

            foreach (var h in hands)
            {
                foreach (var b in bodies)
                {
                    foreach (var ac in accPairs)
                        yield return new Loadout(h.rh, h.lh, b, ac.Item1, ac.Item2);
                }
            }
        }

        private readonly struct EquipScoreBreakdown
        {
            public readonly int EnemyThreat;
            public readonly int Atk;
            public readonly int Def;
            public readonly int Mag;
            public readonly int Agi;
            public readonly int Luk;
            public readonly int MaxHp;
            public readonly int MaxMp;
            public readonly float TermAtkMag;
            public readonly float TermDef;
            public readonly float TermHp;
            public readonly float TermMp;
            public readonly float TermAgi;
            public readonly float TermLuk;
            public readonly float PenaltyThreat;
            public readonly float Total;

            public EquipScoreBreakdown(
                int enemyThreat,
                int atk, int def, int mag, int agi, int luk, int maxHp, int maxMp,
                float termAtkMag, float termDef, float termHp, float termMp, float termAgi, float termLuk,
                float penaltyThreat, float total)
            {
                EnemyThreat = enemyThreat;
                Atk = atk;
                Def = def;
                Mag = mag;
                Agi = agi;
                Luk = luk;
                MaxHp = maxHp;
                MaxMp = maxMp;
                TermAtkMag = termAtkMag;
                TermDef = termDef;
                TermHp = termHp;
                TermMp = termMp;
                TermAgi = termAgi;
                TermLuk = termLuk;
                PenaltyThreat = penaltyThreat;
                Total = total;
            }
        }

        private static EquipScoreBreakdown EvaluateLoadoutScore(BattleSimUnit u, Loadout L, int enemyThreat)
        {
            int atk = u.IntrinsicAttack;
            int def = u.IntrinsicDefense;
            int mag = u.IntrinsicMagic;
            int agi = u.IntrinsicAgility;
            int luk = u.IntrinsicLuck;
            int hp = u.IntrinsicMaxHP;
            int mp = u.IntrinsicMaxMP;
            float mpPct = 0f;

            void Add(EquipmentData e)
            {
                if (e == null) return;
                atk += e.attackBonus;
                def += e.defenseBonus;
                mag += e.magicBonus;
                agi += e.agiBonus;
                luk += e.luckBonus;
                hp += e.hpBonus;
                mp += e.mpBonus;
                mpPct += e.mpBonusPercent;
            }

            Add(L.Rh);
            Add(L.Lh);
            Add(L.Body);
            Add(L.Acc1);
            Add(L.Acc2);

            int maxHp = Mathf.Max(1, hp);
            int maxMp = Mathf.Max(0, mp + Mathf.RoundToInt(u.IntrinsicMaxMP * mpPct));

            float termAtkMag = (atk + mag) * 1.15f;
            float termDef = def * 1.25f;
            float termHp = maxHp * 0.035f;
            float termMp = maxMp * 0.02f;
            float termAgi = agi * 0.12f;
            float termLuk = luk * 0.18f;
            float penalty = enemyThreat * 1.35f;
            float total = termAtkMag + termDef + termHp + termMp + termAgi + termLuk - penalty;

            return new EquipScoreBreakdown(
                enemyThreat, atk, def, mag, agi, luk, maxHp, maxMp,
                termAtkMag, termDef, termHp, termMp, termAgi, termLuk,
                penalty, total);
        }

        private static float ScoreLoadout(BattleSimUnit u, Loadout L, int enemyThreat) =>
            EvaluateLoadoutScore(u, L, enemyThreat).Total;

        /// <summary>요약 텍스트에 붙이는 점수 모델 설명(계수는 <see cref="EvaluateLoadoutScore"/>와 동일해야 함).</summary>
        public static string DescribeScoringModelForSummary()
        {
            return "enemyThreat = max(현재층·다음층 몬스터 풀의 max(ATK, MAG)).\n" +
                   "장착 후 스탯으로:\n" +
                   "  score = (ATK+MAG)×1.15 + DEF×1.25 + MaxHP×0.035 + MaxMP×0.02 + AGI×0.12 + LUK×0.18 - enemyThreat×1.35\n" +
                   "MaxMP = 장비 평탄 MP합 + IntrinsicMaxMP×(장비 mpBonusPercent 합). 동점 시 RNG로 타이브레이크.\n" +
                   "※ Block·ArmorBreak·명중률(accuracyBonus) 등은 이 점수에 넣지 않음 — 방패의 생존 기여가 전투와 달리 반영되지 않을 수 있음.";
        }

        /// <summary>한 번의 선택에 대한 1줄 설명(동일 클래스 내부 전용).</summary>
        private static string FormatPickScoreLine(
            BattleSimUnit template,
            Loadout best,
            int currentFloor,
            int maxFloors,
            DungeonSimMonsterPool pool)
        {
            if (template == null) return "";
            int fThreat = Mathf.Max(
                GetMaxStrikeThreatForFloor(pool, Mathf.Clamp(currentFloor, 1, maxFloors)),
                GetMaxStrikeThreatForFloor(pool, Mathf.Clamp(currentFloor + 1, 1, maxFloors)));
            var d = EvaluateLoadoutScore(template, best, fThreat);
            var sig = new Loadout(best.Rh, best.Lh, best.Body, best.Acc1, best.Acc2).ToSignature();
            return $"층{currentFloor} threat={d.EnemyThreat} | 장착스탯 ATK{d.Atk} DEF{d.Def} MAG{d.Mag} AGI{d.Agi} LUK{d.Luk} MaxHP{d.MaxHp} MaxMP{d.MaxMp} | " +
                   $"(A+M)×1.15={d.TermAtkMag:F2} DEF×1.25={d.TermDef:F2} HP×0.035={d.TermHp:F2} MP×0.02={d.TermMp:F2} AGI×0.12={d.TermAgi:F2} LUK×0.18={d.TermLuk:F2} " +
                   $"- threat×1.35=-{d.PenaltyThreat:F2} => total={d.Total:F2} | {sig}";
        }

        private static BattleSimUnit PickTemplateUnit(DungeonSimPlayer player)
        {
            if (player?.Units == null || player.Units.Count == 0) return null;
            foreach (var u in player.Units)
            {
                if (u != null && u.SimStatLayerEquipmentEnabled)
                    return u;
            }
            return player.Units[0];
        }

        /// <summary>첫 템플릿 유닛 기준 장비 시그니처(보고용).</summary>
        public static string GetPartyEquipSignature(DungeonSimPlayer player)
        {
            var u = PickTemplateUnit(player);
            if (u == null) return "";
            return BuildSignatureFromUnit(u);
        }

        private static string BuildSignatureFromUnit(BattleSimUnit u) =>
            new Loadout(u.SimEquipRightHand, u.SimEquipLeftHand, u.SimEquipBody, u.SimEquipAccessory1, u.SimEquipAccessory2)
                .ToSignature();

        public static void TryPickAndApply(
            DungeonSimPlayer player,
            DungeonSimMonsterPool pool,
            int currentFloor,
            int maxFloors,
            System.Random rng,
            Dictionary<string, int> aiPickHistogram)
        {
            if (player?.Units == null || rng == null) return;

            var template = PickTemplateUnit(player);
            if (template == null || !template.SimStatLayerEquipmentEnabled) return;

            EnsureCrudePoolLoaded();

            int fThreat = Mathf.Max(
                GetMaxStrikeThreatForFloor(pool, Mathf.Clamp(currentFloor, 1, maxFloors)),
                GetMaxStrikeThreatForFloor(pool, Mathf.Clamp(currentFloor + 1, 1, maxFloors)));

            Loadout best = new Loadout(null, null, null, null, null);
            float bestScore = float.MinValue;

            foreach (var L in EnumerateHandBodiesAccessories())
            {
                float s = ScoreLoadout(template, L, fThreat);
                if (s > bestScore + 1e-4f)
                {
                    bestScore = s;
                    best = L;
                }
                else if (Mathf.Abs(s - bestScore) < 1e-3f && rng.Next(2) == 0)
                    best = L;
            }

            foreach (var u in player.Units)
            {
                if (u == null || !u.SimStatLayerEquipmentEnabled) continue;
                u.SimEquipRightHand = best.Rh;
                u.SimEquipLeftHand = best.Lh;
                u.SimEquipBody = best.Body;
                u.SimEquipAccessory1 = best.Acc1;
                u.SimEquipAccessory2 = best.Acc2;
                u.RecomputeSimDerivedStatsFromIntrinsicsAndEquipment();
            }

            string sig = new Loadout(best.Rh, best.Lh, best.Body, best.Acc1, best.Acc2).ToSignature();
            player.LastAiEquipSignature = sig;
            string explain = FormatPickScoreLine(template, best, currentFloor, maxFloors, pool);
            player.LastAiEquipScoreExplain = explain;
            RegisterExplainSample(explain);
            player.AiEquipPickCount++;

            if (aiPickHistogram != null)
            {
                if (!aiPickHistogram.TryGetValue(sig, out int c))
                    c = 0;
                aiPickHistogram[sig] = c + 1;
            }
        }
    }
}
