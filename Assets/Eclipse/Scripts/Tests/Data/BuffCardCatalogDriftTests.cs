using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eclipse.Tests
{
    /// <summary>
    /// 실제 카드 카탈로그 에셋이 기획표에서 벗어나는 것을 잡는다. 저주 행은 부호 하나만 뒤집혀도
    /// 적을 강화하는 쪽으로 조용히 돌아선다.
    /// </summary>
    public class BuffCardCatalogDriftTests
    {
        // 값 배열은 커먼 · 레어 · 에픽 순이며, 에픽이 각 축의 등급 천장이다.
        private static readonly (StatType axis, string name, float[] byGrade)[] ExpectedBuffs =
        {
            (StatType.Hp, "심부의 활력", new[] { 0.12f, 0.16f, 0.20f }),
            (StatType.Atk, "벼려진 칼날", new[] { 0.09f, 0.12f, 0.15f }),
            (StatType.Def, "굳은 대열", new[] { 0.11f, 0.14f, 0.18f }),
            (StatType.Spd, "빨라진 맥박", new[] { 0.09f, 0.12f, 0.15f }),
            (StatType.CritRate, "급소 간파", new[] { 0.07f, 0.10f, 0.12f }),
            (StatType.CritDamage, "파열의 일격", new[] { 0.18f, 0.24f, 0.30f }),
        };

        private static readonly (StatType axis, string name, float[] byGrade)[] ExpectedCurses =
        {
            (StatType.Hp, "쇠약한 육신", new[] { -0.07f, -0.10f, -0.12f }),
            (StatType.Atk, "무딘 발톱", new[] { -0.07f, -0.10f, -0.12f }),
            (StatType.Def, "갈라진 각질", new[] { -0.09f, -0.12f, -0.15f }),
            (StatType.Spd, "굼뜬 촉수", new[] { -0.06f, -0.08f, -0.10f }),
        };

        // 유니크 5장이 붙는 자리와 세기. 대상 스킬 슬롯을 잘못 적으면 라이더가 조용히 다른 스킬에 붙는다.
        private static readonly (string id, string name, string characterId, SkillSlot slot,
            EffectType type, TargetSelector target, float value, int duration)[] ExpectedUniques =
        {
            ("unique_kael", "무너지지 않는 벽", "kael", SkillSlot.Normal,
                EffectType.Shield, TargetSelector.Self, 0.10f, 2),
            ("unique_ria", "연격 본능", "ria", SkillSlot.Basic,
                EffectType.Damage, TargetSelector.SingleEnemy, 0.70f, 0),
            ("unique_eliana", "폭주의 불씨", "eliana", SkillSlot.Basic,
                EffectType.Dot, TargetSelector.SingleEnemy, 0.12f, 2),
            ("unique_arin", "끊이지 않는 견제", "arin", SkillSlot.Basic,
                EffectType.Debuff, TargetSelector.SingleEnemy, 0.12f, 2),
            ("unique_selene", "생명의 샘", "selene", SkillSlot.Basic,
                EffectType.Regen, TargetSelector.LowestHpAlly, 0.12f, 2),
        };

        private static readonly CardGrade[] RampGrades = { CardGrade.Common, CardGrade.Rare, CardGrade.Epic };

        private static BuffCardCatalogSO Load()
            => AssetDatabase.FindAssets("t:BuffCardCatalogSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuffCardCatalogSO>)
                .Single();

        [Test]
        public void 카탈로그가_범용_십팔행과_저주_십이행과_유니크_오행이다()
        {
            var cards = Load().cards;

            Assert.AreEqual(35, cards.Length);
            Assert.AreEqual(18, cards.Count(c => !c.targetsEnemies && c.grade != CardGrade.Unique),
                "범용 6효과 × 3등급");
            Assert.AreEqual(12, cards.Count(c => c.targetsEnemies), "저주 4효과 × 3등급");
            foreach (var grade in RampGrades)
                Assert.AreEqual(10, cards.Count(c => c.grade == grade), $"{grade} 등급은 범용 6 + 저주 4장이다");
            Assert.AreEqual(5, cards.Count(c => c.grade == CardGrade.Unique), "유니크는 캐릭터당 1장");
        }

        [Test]
        public void 유니크가_기획표의_스킬에_기획표의_효과를_붙인다()
        {
            var actual = Load().cards.Where(c => c.grade == CardGrade.Unique).ToDictionary(c => c.id);

            var failures = ExpectedUniques
                .Where(row => !actual.TryGetValue(row.id, out var card)
                    || card.displayName != row.name
                    || card.requiredCharacterId != row.characterId
                    || card.targetSkill != row.slot
                    || card.addedEffect.type != row.type
                    || card.addedEffect.target != row.target
                    || card.addedEffect.duration != row.duration
                    || !Mathf.Approximately(card.addedEffect.value, row.value))
                .Select(row => $"{row.name}: {row.characterId} {row.slot} {row.type} {row.value} {row.duration}턴")
                .ToList();

            Assert.That(failures, Is.Empty, "유니크 드리프트 감지:\n" + string.Join("\n", failures));
            Assert.IsTrue(actual.Values.All(c => c.deltas == null || c.deltas.Length == 0),
                "유니크는 스탯을 건드리지 않는다");
            Assert.IsTrue(actual.Values.All(c => !string.IsNullOrEmpty(c.description)),
                "유니크는 화면 문구를 증감값이 아니라 설명문에서 낸다");
        }

        [Test]
        public void 카드_수치가_기획표와_일치한다()
        {
            var actual = Load().cards
                .Where(c => c.grade != CardGrade.Unique)
                .ToDictionary(c => (c.targetsEnemies, c.deltas.Single().axis, c.grade));

            var failures = ExpectedBuffs.Select(row => (targetsEnemies: false, row))
                .Concat(ExpectedCurses.Select(row => (targetsEnemies: true, row)))
                .SelectMany(entry => RampGrades.Select((grade, i) => (entry, grade, expected: entry.row.byGrade[i])))
                .Where(t => !actual.TryGetValue((t.entry.targetsEnemies, t.entry.row.axis, t.grade), out var card)
                    || card.displayName != t.entry.row.name
                    || !Mathf.Approximately(card.deltas.Single().value, t.expected))
                .Select(t => $"{t.entry.row.name}({t.grade}): 기대 {t.expected}")
                .ToList();

            Assert.That(failures, Is.Empty, "카드 수치 드리프트 감지:\n" + string.Join("\n", failures));
        }

        [Test]
        public void 저주는_전부_음수고_범용은_전부_양수다()
        {
            var offenders = Load().cards
                .Where(c => c.deltas.Any(d => c.targetsEnemies ? d.value >= 0f : d.value <= 0f))
                .Select(c => $"{c.id}: {c.deltas.Single().value}")
                .ToList();

            Assert.That(offenders, Is.Empty,
                "적 스탯은 버프와 같은 덧셈 경로를 타므로 저주는 음수여야 한다:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void 등급_가중_노브가_기획값이다()
        {
            var catalog = Load();

            Assert.AreEqual(60, catalog.WeightOf(CardGrade.Common));
            Assert.AreEqual(30, catalog.WeightOf(CardGrade.Rare));
            Assert.AreEqual(10, catalog.WeightOf(CardGrade.Epic));
            Assert.AreEqual(60, catalog.WeightOf(CardGrade.Unique));
        }
    }
}
