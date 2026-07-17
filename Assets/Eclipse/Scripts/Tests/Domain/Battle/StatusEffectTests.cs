using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eclipse.Tests
{
    public class StatusEffectTests
    {
        // --- 빌더 ---

        private static Stats S(int hp, int atk, int def, int spd, float cr = 0f, float cd = 1.5f)
            => new Stats { hp = hp, atk = atk, def = def, spd = spd, critRate = cr, critDamage = cd };

        // 스킬 없이 스탯만 가진 아군 유닛(상태효과 단위 테스트용).
        private static Combatant Unit(Stats stats, int slot = 0)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.displayName = "U";
            so.baseStats = stats;
            so.growthCurve = ScriptableObject.CreateInstance<GrowthCurve>(); // Lv1이라 스케일 없음
            return Combatant.FromCharacter(new OwnedCharacter(so, 1), slot);
        }

        // --- 버프·디버프: 유효 스탯 반영 ---

        [Test]
        public void 버프는_공격력을_비율로_올린다()
        {
            var unit = Unit(S(1000, 100, 50, 100));
            ((IDamageable)unit).ApplyEffect(StatusEffect.StatModifier(EffectType.Buff, StatType.Atk, 0.4f, 2));

            Assert.AreEqual(140, unit.EffectiveStats.atk);
            Assert.AreEqual(50, unit.EffectiveStats.def, "다른 스탯은 그대로");
        }

        [Test]
        public void 디버프는_방어력을_비율로_내린다()
        {
            var unit = Unit(S(1000, 100, 200, 100));
            ((IDamageable)unit).ApplyEffect(StatusEffect.StatModifier(EffectType.Debuff, StatType.Def, 0.3f, 2));

            Assert.AreEqual(140, unit.EffectiveStats.def);
        }

        [Test]
        public void 버프는_자기_턴_수만큼_지속되고_만료된다()
        {
            var unit = Unit(S(1000, 100, 50, 100));
            ((IDamageable)unit).ApplyEffect(StatusEffect.StatModifier(EffectType.Buff, StatType.Atk, 0.4f, 2));

            unit.TickStatusEffects(); // 지속턴 2 → 1, 아직 유효
            Assert.AreEqual(140, unit.EffectiveStats.atk);

            unit.TickStatusEffects(); // 지속턴 1 → 0, 만료
            Assert.AreEqual(100, unit.EffectiveStats.atk);
        }

        [Test]
        public void 같은_스탯_버프_재적용은_갱신되어_하나만_유지된다()
        {
            var unit = Unit(S(1000, 100, 50, 100));
            ((IDamageable)unit).ApplyEffect(StatusEffect.StatModifier(EffectType.Buff, StatType.Atk, 0.4f, 1));
            ((IDamageable)unit).ApplyEffect(StatusEffect.StatModifier(EffectType.Buff, StatType.Atk, 0.5f, 3));

            // 누적(190)이 아니라 새 값(150)만 반영.
            Assert.AreEqual(150, unit.EffectiveStats.atk);
        }

        [Test]
        public void 같은_스탯의_버프와_디버프는_상쇄된다()
        {
            var unit = Unit(S(1000, 100, 50, 100));
            ((IDamageable)unit).ApplyEffect(StatusEffect.StatModifier(EffectType.Buff, StatType.Atk, 0.4f, 3));
            ((IDamageable)unit).ApplyEffect(StatusEffect.StatModifier(EffectType.Debuff, StatType.Atk, 0.3f, 3));

            // 100 × (1 + 0.4 − 0.3) = 110
            Assert.AreEqual(110, unit.EffectiveStats.atk);
        }

        // --- 도트·리젠: 매 턴 HP 변화 ---

        [Test]
        public void 도트는_매_틱_고정_피해를_주고_지속턴_후_멈춘다()
        {
            var unit = Unit(S(100, 100, 0, 100));
            ((IDamageable)unit).ApplyEffect(StatusEffect.Periodic(EffectType.Dot, 10, 3));

            unit.TickStatusEffects(); Assert.AreEqual(90, unit.CurrentHp);
            unit.TickStatusEffects(); Assert.AreEqual(80, unit.CurrentHp);
            unit.TickStatusEffects(); Assert.AreEqual(70, unit.CurrentHp); // 3틱째 후 만료
            unit.TickStatusEffects(); Assert.AreEqual(70, unit.CurrentHp, "만료 후 더는 안 깎임");
        }

        [Test]
        public void 리젠은_매_틱_회복하고_최대_HP를_넘지_않는다()
        {
            var unit = Unit(S(100, 100, 0, 100));
            ((IDamageable)unit).ApplyDamage(50); // HP 50
            ((IDamageable)unit).ApplyEffect(StatusEffect.Periodic(EffectType.Regen, 30, 2));

            unit.TickStatusEffects(); Assert.AreEqual(80, unit.CurrentHp);
            unit.TickStatusEffects(); Assert.AreEqual(100, unit.CurrentHp, "최대치에서 클램프");
        }

        // --- 실드: 전액 흡수 후 초과분만 HP ---

        [Test]
        public void 실드는_피해를_전액_흡수하고_소진되면_초과분만_HP를_깎는다()
        {
            var unit = Unit(S(100, 100, 0, 100));
            ((IDamageable)unit).ApplyEffect(StatusEffect.Shield(30, 5));

            ((IDamageable)unit).ApplyDamage(20);
            Assert.AreEqual(100, unit.CurrentHp, "실드가 전액 흡수(30 → 10 남음)");

            ((IDamageable)unit).ApplyDamage(25);
            Assert.AreEqual(85, unit.CurrentHp, "실드 10 흡수 + 초과분 15만 HP로");
        }

        [Test]
        public void 실드는_지속턴이_끝나면_남은_흡수량과_함께_사라진다()
        {
            var unit = Unit(S(100, 100, 0, 100));
            ((IDamageable)unit).ApplyEffect(StatusEffect.Shield(50, 1));

            unit.TickStatusEffects(); // 지속턴 1 → 0, 만료 제거

            ((IDamageable)unit).ApplyDamage(20);
            Assert.AreEqual(80, unit.CurrentHp, "실드가 사라져 피해가 그대로 HP로");
        }

        // --- SPD 수정자가 턴 순서에 라이브로 반영 ---

        [Test]
        public void SPD_디버프는_턴_순서를_바꾼다()
        {
            var fast = Unit(S(1000, 1, 0, 100), slot: 0);
            var slow = Unit(S(1000, 1, 0, 90), slot: 1);
            var before = new AtbTurnScheduler(new ICombatant[] { fast, slow });
            Assert.AreSame(fast, before.GetNextActor(), "디버프 전엔 빠른 유닛이 먼저");

            var fast2 = Unit(S(1000, 1, 0, 100), slot: 0);
            var slow2 = Unit(S(1000, 1, 0, 90), slot: 1);
            ((IDamageable)fast2).ApplyEffect(StatusEffect.StatModifier(EffectType.Debuff, StatType.Spd, 0.3f, 5));
            var after = new AtbTurnScheduler(new ICombatant[] { fast2, slow2 });
            Assert.AreSame(slow2, after.GetNextActor(), "SPD 디버프로 유효 SPD 70 < 90 → 느렸던 유닛이 먼저");
        }

        // --- 도발: 단일-적 타겟을 도발자에게 끌어옴 ---

        [Test]
        public void 도발_중인_적이_있으면_단일적_공격이_도발자를_친다()
        {
            var resolver = new TargetResolver();
            var attacker = Unit(S(500, 100, 0, 100));
            var taunter = Unit(S(1000, 100, 0, 100), slot: 1); // 슬롯 뒤(폴백 슬롯순이면 안 뽑힐 자리)
            var squishy = Unit(S(100, 100, 0, 100), slot: 0);  // 슬롯 앞
            ((IDamageable)taunter).ApplyEffect(StatusEffect.Taunt(2));

            var enemies = new List<ICombatant> { taunter, squishy };
            var targets = resolver.Resolve(TargetSelector.SingleEnemy, attacker, new List<ICombatant>(), enemies);

            Assert.AreSame(taunter, targets.Single(), "도발 중이면 슬롯 앞 유닛이 있어도 도발자를 우선");
        }

        [Test]
        public void 도발자가_죽으면_단일적_공격이_정상_타겟으로_돌아간다()
        {
            var resolver = new TargetResolver();
            var attacker = Unit(S(500, 100, 0, 100));
            var taunter = Unit(S(1000, 100, 0, 100), slot: 0);
            var squishy = Unit(S(100, 100, 0, 100), slot: 1);
            ((IDamageable)taunter).ApplyEffect(StatusEffect.Taunt(2));
            ((IDamageable)taunter).ApplyDamage(9999); // 도발자 사망

            var enemies = new List<ICombatant> { taunter, squishy };
            var targets = resolver.Resolve(TargetSelector.SingleEnemy, attacker, new List<ICombatant>(), enemies);

            Assert.AreSame(squishy, targets.Single(), "죽은 도발자는 후보에서 빠져 생존 적으로");
        }

        [Test]
        public void 광역_공격은_도발과_무관하게_전체를_친다()
        {
            var resolver = new TargetResolver();
            var attacker = Unit(S(500, 100, 0, 100));
            var taunter = Unit(S(1000, 100, 0, 100), slot: 0);
            var other = Unit(S(500, 100, 0, 100), slot: 1);
            ((IDamageable)taunter).ApplyEffect(StatusEffect.Taunt(2));

            var enemies = new List<ICombatant> { taunter, other };
            var targets = resolver.Resolve(TargetSelector.AllEnemies, attacker, new List<ICombatant>(), enemies);

            Assert.AreEqual(2, targets.Count, "광역기는 도발을 무시하고 전체 타격");
        }

        [Test]
        public void 도발은_지속턴이_끝나면_풀린다()
        {
            var unit = Unit(S(1000, 100, 0, 100));
            ((IDamageable)unit).ApplyEffect(StatusEffect.Taunt(2));
            Assert.IsTrue(unit.IsTaunting);

            unit.TickStatusEffects(); // 2 → 1, 아직 유효
            Assert.IsTrue(unit.IsTaunting);
            unit.TickStatusEffects(); // 1 → 0, 만료
            Assert.IsFalse(unit.IsTaunting, "지속턴 소진 후 도발 풀림");
        }

        [Test]
        public void 도발_재적용은_갱신되어_최신_지속턴만_남는다()
        {
            var unit = Unit(S(1000, 100, 0, 100));
            ((IDamageable)unit).ApplyEffect(StatusEffect.Taunt(3));
            ((IDamageable)unit).ApplyEffect(StatusEffect.Taunt(1)); // 갱신 → 지속턴 1

            unit.TickStatusEffects(); // 1 → 0 만료
            Assert.IsFalse(unit.IsTaunting, "누적이 아니라 갱신돼 최신(1턴)만 남고 소진");
        }

        // --- 상태효과가 낀 전투도 시드 고정이면 결정적 ---

        [UnityTest]
        public IEnumerator 도트_스킬_전투도_같은_시드면_같은_결과() => UniTask.ToCoroutine(async () =>
        {
            SkillSO Skill()
            {
                var s = ScriptableObject.CreateInstance<SkillSO>();
                s.id = "atk_dot"; s.displayName = "독칼"; s.cooldownTurns = 0;
                s.effects = new List<SkillEffect>
                {
                    new SkillEffect { type = EffectType.Damage, target = TargetSelector.SingleEnemy, value = 0.8f },
                    new SkillEffect { type = EffectType.Dot, target = TargetSelector.SingleEnemy, value = 0.3f, duration = 3 },
                };
                return s;
            }

            BattleEngine Build()
            {
                var so = ScriptableObject.CreateInstance<CharacterSO>();
                so.displayName = "A"; so.baseStats = S(500, 100, 20, 110, 0.3f, 2f);
                so.growthCurve = ScriptableObject.CreateInstance<GrowthCurve>();
                so.basicSkill = Skill();
                var ally = Combatant.FromCharacter(new OwnedCharacter(so, 1), 0);

                var eso = ScriptableObject.CreateInstance<EnemySO>();
                eso.displayName = "E"; eso.baseStats = S(400, 80, 10, 100);
                eso.basicSkill = Skill();
                var enemy = Combatant.FromEnemy(eso, 0);

                var allies = new List<Combatant> { ally };
                var enemies = new List<Combatant> { enemy };
                var scheduler = new AtbTurnScheduler(allies.Concat(enemies));
                var pipeline = new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(777));
                var combat = new CombatPipeline(pipeline);
                var targeting = new TargetResolver();
                var executor = new SkillExecutor(combat, targeting);
                // 1v1 결정성 테스트라 타겟 선택은 후보가 하나뿐(난수 미소비). 양편에 같은 프로파일을 넘겨도 결과가 같다.
                var targetRng = new SeededRandom(BattleSeed.For(777, BattleSeed.Stream.AllyTargeting));
                var provider = RuleBasedActionProvider.AllyAuto(targeting, combat, targetRng);
                return new BattleEngine(allies, enemies, scheduler, executor, provider, provider, 200);
            }

            var e1 = Build(); var o1 = await e1.RunAsync(CancellationToken.None);
            var e2 = Build(); var o2 = await e2.RunAsync(CancellationToken.None);

            Assert.AreEqual(o1, o2, "같은 시드·편성은 같은 승패");
            Assert.AreEqual(e1.ActionCount, e2.ActionCount, "같은 시드·편성은 같은 행동 수");
        });
    }
}