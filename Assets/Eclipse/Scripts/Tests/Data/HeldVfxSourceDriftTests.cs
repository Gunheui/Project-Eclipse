using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using NUnit.Framework;
using UnityEditor;

namespace Eclipse.Tests
{
    /// <summary>
    /// 유지 이펙트를 물린 스킬에 정작 지속 효과가 없는 배선 실수를 잡는다. 오라의 수명은 그 스킬이 건
    /// 지속 효과에서 나오므로, 지속 효과가 없으면 오라가 뜨자마자 걷힌다.
    /// </summary>
    public class HeldVfxSourceDriftTests
    {
        // 걸리면 유닛에 남아 오라의 수명이 되는 효과들. 즉시 피해·회복은 걸리지 않고 지나간다.
        private static readonly EffectType[] LastingTypes =
        {
            EffectType.Buff, EffectType.Debuff, EffectType.Dot,
            EffectType.Regen, EffectType.Shield, EffectType.Taunt,
        };

        [Test]
        public void 유지_이펙트를_쓰는_스킬은_지속_효과를_가진다()
        {
            var failures = new List<string>();

            foreach (var skill in AllSkills())
            {
                if (!HasHeldLayer(skill.castVfx) && !HasHeldLayer(skill.impactVfx)) continue;
                if (skill.effects != null && skill.effects.Any(IsLasting)) continue;
                failures.Add($"{skill.id}: 유지 이펙트를 참조하는데 남는 효과가 없어 오라가 즉시 걷힌다.");
            }

            Assert.That(failures, Is.Empty,
                "유지 이펙트 배선 드리프트 감지:\n" + string.Join("\n", failures));
        }

        private static IEnumerable<SkillSO> AllSkills()
            => AssetDatabase.FindAssets("t:SkillSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SkillSO>)
                .OrderBy(s => s.id);

        /// <summary>지속턴이 0이면 걸리자마자 만료되므로 수명을 받쳐 주지 못한다.</summary>
        private static bool IsLasting(SkillEffect effect)
            => effect.duration != 0 && LastingTypes.Contains(effect.type);

        private static bool HasHeldLayer(VfxSpec spec)
            => spec != null && spec.layers != null && spec.layers.Any(l => l.holdTurns > 0);
    }
}
