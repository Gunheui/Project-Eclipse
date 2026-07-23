using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Eclipse.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eclipse.Tests
{
    /// <summary>
    /// 스킬 설명문과 effects 수치의 어긋남(드리프트)을 잡는 휴리스틱 검사.
    /// 숫자가 설명문에 존재하는지만 확인하며, 문장의 의미까지 검증하지는 않는다.
    /// </summary>
    public class SkillDescriptionDriftTests
    {
        private static readonly Regex PercentPattern = new Regex(@"(\d+)%");
        private static readonly Regex TurnPattern = new Regex(@"(\d+)턴");

        private static IEnumerable<SkillSO> AllSkills()
            => AssetDatabase.FindAssets("t:SkillSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SkillSO>)
                .OrderBy(s => s.id);

        [Test]
        public void 설명문_수치가_effects와_어긋나지_않는다()
        {
            var failures = new List<string>();

            foreach (var skill in AllSkills())
            {
                var desc = skill.description ?? string.Empty;

                // 쿨다운: (재사용 N턴) 문구와 cooldownTurns의 정확 일치.
                var cooldownText = $"(재사용 {skill.cooldownTurns}턴)";
                if (skill.cooldownTurns > 0 && !desc.Contains(cooldownText))
                    failures.Add($"{skill.id}: '{cooldownText}' 문구가 설명문에 없다.");
                if (skill.cooldownTurns == 0 && desc.Contains("재사용"))
                    failures.Add($"{skill.id}: cooldownTurns가 0인데 설명문에 재사용 문구가 있다.");

                // 퍼센트: 효과별 value×100의 등장 횟수가 설명문에 그만큼 있어야 한다.
                var descPercents = PercentPattern.Matches(desc)
                    .Select(m => int.Parse(m.Groups[1].Value))
                    .ToList();
                var expectedPercents = skill.effects
                    .Where(e => e.value > 0f)
                    .Select(e => Mathf.RoundToInt(e.value * 100f));
                foreach (var group in expectedPercents.GroupBy(p => p))
                {
                    var found = descPercents.Count(p => p == group.Key);
                    if (found < group.Count())
                        failures.Add($"{skill.id}: {group.Key}%가 설명문에 {group.Count()}회 필요한데 {found}회 있다.");
                }

                // 지속턴: 쿨다운 문구를 뗀 나머지에서 duration 턴 수가 언급돼야 한다.
                // 여러 효과가 같은 지속턴을 공유하면 한 문구로 서술할 수 있어 횟수는 세지 않는다.
                var descWithoutCooldown = desc.Replace(cooldownText, string.Empty);
                var descTurns = TurnPattern.Matches(descWithoutCooldown)
                    .Select(m => int.Parse(m.Groups[1].Value))
                    .ToHashSet();
                foreach (var duration in skill.effects.Where(e => e.duration > 0).Select(e => e.duration).Distinct())
                {
                    if (!descTurns.Contains(duration))
                        failures.Add($"{skill.id}: 지속 {duration}턴이 설명문에 언급되지 않았다.");
                }
            }

            Assert.That(failures, Is.Empty,
                "스킬 설명문 드리프트 감지:\n" + string.Join("\n", failures));
        }
    }
}
