using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AbyssdawnBattle;

namespace Abyssdawn
{
    /// <summary>
    /// 전투 시뮬 스킬 시전 횟수 누적 — 키는 <see cref="SkillData.skillID"/>(비어 있으면 <see cref="SkillData.skillName"/>).
    /// CSV/요약용 문자열: <c>key=count|key2=count2</c> (키 오름차순).
    /// </summary>
    public static class SimSkillActivationLog
    {
        public static string KeyFor(SkillData skill)
        {
            if (skill == null) return "";
            if (!string.IsNullOrEmpty(skill.skillID)) return skill.skillID;
            if (!string.IsNullOrEmpty(skill.skillName)) return skill.skillName;
            return "?";
        }

        public static void Bump(Dictionary<string, long> counts, SkillData skill, long delta = 1)
        {
            if (counts == null || skill == null || delta == 0) return;
            string k = KeyFor(skill);
            if (string.IsNullOrEmpty(k)) return;
            if (counts.TryGetValue(k, out long v))
                counts[k] = v + delta;
            else
                counts[k] = delta;
        }

        public static void MergeFrom(Dictionary<string, long> into, Dictionary<string, long> from)
        {
            if (into == null || from == null) return;
            foreach (var kv in from)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                if (into.TryGetValue(kv.Key, out long v))
                    into[kv.Key] = v + kv.Value;
                else
                    into[kv.Key] = kv.Value;
            }
        }

        public static void MergeEncoded(Dictionary<string, long> into, string encoded)
        {
            if (into == null || string.IsNullOrEmpty(encoded)) return;
            var segments = encoded.Split('|');
            for (int i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];
                if (string.IsNullOrEmpty(seg)) continue;
                int eq = seg.IndexOf('=');
                if (eq <= 0 || eq >= seg.Length - 1) continue;
                string k = seg.Substring(0, eq);
                if (string.IsNullOrEmpty(k)) continue;
                if (!long.TryParse(seg.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) ||
                    n == 0)
                    continue;
                if (into.TryGetValue(k, out long v))
                    into[k] = v + n;
                else
                    into[k] = n;
            }
        }

        public static string ToEncoded(Dictionary<string, long> counts)
        {
            if (counts == null || counts.Count == 0) return "";
            var parts = counts
                .Where(kv => !string.IsNullOrEmpty(kv.Key) && kv.Value != 0)
                .OrderBy(kv => kv.Key, System.StringComparer.Ordinal)
                .Select(kv => kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
            return string.Join("|", parts);
        }

        /// <summary>요약 텍스트용 — 횟수 내림차순, 동률 시 키 순.</summary>
        public static void AppendSortedHumanReport(StringBuilder sb, Dictionary<string, long> counts, string title, int maxLines = 40)
        {
            sb.AppendLine(title);
            if (counts == null || counts.Count == 0)
            {
                sb.AppendLine("  (없음)");
                sb.AppendLine();
                return;
            }

            int n = 0;
            foreach (var kv in counts
                         .Where(x => !string.IsNullOrEmpty(x.Key) && x.Value > 0)
                         .OrderByDescending(x => x.Value)
                         .ThenBy(x => x.Key, System.StringComparer.Ordinal))
            {
                sb.AppendLine($"  {kv.Key}: {kv.Value}");
                n++;
                if (n < maxLines) continue;
                sb.AppendLine($"  … 상위 {maxLines}행만 표시");
                sb.AppendLine();
                return;
            }

            sb.AppendLine();
        }
    }
}
