using System.Collections.Generic;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    public class TargetPriorityPolicyTests
    {
        // 정책이 읽는 상태만 채우는 테스트용 유닛.
        private sealed class Combatant : ICombatant
        {
            public string DisplayName => "u";
            public Team Team { get; set; }
            public int SlotIndex { get; set; }
            public Stats EffectiveStats { get; set; }
            public int MaxHp { get; set; }
            public int CurrentHp { get; set; }
            public bool IsAlive => CurrentHp > 0;
            public IReadOnlyList<SkillRuntime> Skills { get; set; }
            public bool IsTaunting { get; set; }
            public int ShieldAbsorb { get; set; }
        }

        private static Combatant Enemy(int slot, int hp, int maxHp, int atk = 0, bool taunting = false, int shield = 0)
            => new Combatant
            {
                Team = Team.Enemy, SlotIndex = slot, CurrentHp = hp, MaxHp = maxHp,
                EffectiveStats = new Stats { atk = atk }, IsTaunting = taunting, ShieldAbsorb = shield
            };

        private static Combatant Actor(int atk)
            => new Combatant { Team = Team.Ally, EffectiveStats = new Stats { atk = atk } };

        // 단일-적 데미지 스킬(정책이 다루는 유일한 형태).
        private static SkillRuntime SingleEnemyDamage(float power = 1f)
            => Skill(EffectType.Damage, TargetSelector.SingleEnemy, power);

        private static SkillRuntime Skill(EffectType type, TargetSelector target, float power)
        {
            var s = ScriptableObject.CreateInstance<SkillSO>();
            s.id = "s"; s.displayName = "s"; s.cooldownTurns = 0;
            s.effects = new List<SkillEffect> { new SkillEffect { type = type, target = target, value = power } };
            return new SkillRuntime(s);
        }

        // 방어경감 없이(def 0) 예측이 단순해지도록 defenseK는 무관. varMin 0.95로 하한이 정해진다.
        private static CombatPipeline Combat(int seed = 1)
            => new CombatPipeline(new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(seed)));

        private static TargetPriorityPolicy Policy(TargetPolicyProfile profile, int seed = 1)
            => new TargetPriorityPolicy(new TargetResolver(), Combat(seed),
                new SeededRandom(BattleSeed.ForTargeting(seed)), profile);

        // 적 프로파일. 기본값은 프로덕션 튜닝값(BattleConstantsSO 기본)과 같은 0.6 / 0.5.
        private static TargetPolicyProfile EnemyAi(float lethalChance = 0.6f, float lowHpBias = 0.5f)
            => TargetPolicyProfile.EnemyAi(lethalChance, lowHpBias);

        private static readonly IReadOnlyList<ICombatant> NoAllies = new List<ICombatant>();

        [Test]
        public void 단일적_데미지가_아니면_null을_돌려준다()
        {
            var actor = Actor(atk: 100);
            var enemies = new List<ICombatant> { Enemy(0, 100, 100) };
            var heal = Skill(EffectType.Heal, TargetSelector.SingleAlly, 1f);

            var target = Policy(TargetPolicyProfile.AllyAuto).ChoosePrimaryTarget(actor, heal, NoAllies, enemies);

            Assert.IsNull(target); // 힐·광역·자기 스킬은 주 타겟을 고르지 않는다
        }

        [Test]
        public void 유효_후보가_없으면_null을_돌려준다()
        {
            var actor = Actor(atk: 100);
            var enemies = new List<ICombatant> { Enemy(0, 0, 100) }; // 전부 사망

            var target = Policy(TargetPolicyProfile.AllyAuto)
                .ChoosePrimaryTarget(actor, SingleEnemyDamage(), NoAllies, enemies);

            Assert.IsNull(target);
        }

        [Test]
        public void 도발_중인_적이_있으면_프로파일과_무관하게_도발자를_고른다()
        {
            var actor = Actor(atk: 100);
            var taunter = Enemy(1, 1000, 1000, taunting: true); // 슬롯 뒤·풀피지만 도발
            var squishy = Enemy(0, 10, 1000);                   // 슬롯 앞·저HP
            var enemies = new List<ICombatant> { taunter, squishy };

            var ally = Policy(TargetPolicyProfile.AllyAuto).ChoosePrimaryTarget(actor, SingleEnemyDamage(), NoAllies, enemies);
            var enemy = Policy(EnemyAi()).ChoosePrimaryTarget(actor, SingleEnemyDamage(), NoAllies, enemies);

            Assert.AreSame(taunter, ally);
            Assert.AreSame(taunter, enemy);
        }

        [Test]
        public void 아군_막타층은_HP비율이_더_낮은_적보다_처치가능한_적을_우선한다()
        {
            // atk 100·power 1 → 예상 하한 ≈ round(100 × 0.95) = 95.
            // killable(HP 50/100, 비율 0.5)은 확정 처치. weaker(HP 200/1000, 비율 0.2)는 비율이 더 낮지만 처치 불가.
            // → 막타 층을 끄면 기저(최저 HP비율)가 weaker를 고르므로, killable이 나오는 건 막타 층 때문뿐이다.
            var actor = Actor(atk: 100);
            var killable = Enemy(1, 50, 100);     // 비율 0.5 · 막타 가능
            var weaker = Enemy(0, 200, 1000);     // 비율 0.2(더 낮음) · 막타 불가
            var enemies = new List<ICombatant> { killable, weaker };

            var target = Policy(TargetPolicyProfile.AllyAuto)
                .ChoosePrimaryTarget(actor, SingleEnemyDamage(power: 1f), NoAllies, enemies);

            Assert.AreSame(killable, target); // 막타 > 최저 HP비율

            // 대조군: 같은 픽스처에서 막타 층만 끄면 기저가 weaker를 고른다(위 결과가 막타 층 덕임을 못박는다).
            var noLethal = new TargetPolicyProfile(0f, TargetBaseTier.AllyLowestHpBucket, 0f);
            var baseTarget = Policy(noLethal)
                .ChoosePrimaryTarget(actor, SingleEnemyDamage(power: 1f), NoAllies, enemies);

            Assert.AreSame(weaker, baseTarget);
        }

        [Test]
        public void 아군_막타층은_실드에_막혀_죽지_않는_적을_처치가능으로_보지_않는다()
        {
            // atk 100·power 1 → 예상 하한 ≈ 95.
            // shielded: HP 50이라 HP만 보면 처치 가능해 보이지만 실드 60까지 뚫어야 해 유효 HP 110 → 실제로는 못 죽인다.
            // killable: 실드 없이 HP 90 → 95로 확정 처치.
            // 실드를 무시하면 HP가 더 낮은 shielded를 골라 한 턴을 버린다.
            var actor = Actor(atk: 100);
            var shielded = Enemy(0, 50, 100, shield: 60);
            var killable = Enemy(1, 90, 100);
            var enemies = new List<ICombatant> { shielded, killable };

            var target = Policy(TargetPolicyProfile.AllyAuto)
                .ChoosePrimaryTarget(actor, SingleEnemyDamage(power: 1f), NoAllies, enemies);

            Assert.AreSame(killable, target);
        }

        [Test]
        public void 아군_막타가_없으면_HP비율이_가장_낮은_적을_고른다()
        {
            // power를 아주 작게 줘 아무도 처치 불가 → 기저(최저 HP비율) 층으로 내려간다.
            var actor = Actor(atk: 100);
            var wounded = Enemy(1, 300, 1000);   // 비율 0.3 (슬롯 뒤)
            var healthy = Enemy(0, 900, 1000);   // 비율 0.9 (슬롯 앞)
            var enemies = new List<ICombatant> { wounded, healthy };

            var target = Policy(TargetPolicyProfile.AllyAuto)
                .ChoosePrimaryTarget(actor, SingleEnemyDamage(power: 0.0001f), NoAllies, enemies);

            Assert.AreSame(wounded, target); // 슬롯 앞이 아니라 HP비율 최저
        }

        [Test]
        public void 같은_시드면_적_가중랜덤도_같은_대상을_고른다()
        {
            // HP비율을 서로 다르게 둬야 가중치가 약분되지 않아 재현이 실제로 검증된다.
            List<ICombatant> Enemies() => new List<ICombatant>
            {
                Enemy(0, 1000, 1000), Enemy(1, 500, 1000), Enemy(2, 100, 1000)
            };
            var actor = Actor(atk: 100);

            var a = Policy(EnemyAi(), seed: 55).ChoosePrimaryTarget(actor, SingleEnemyDamage(), NoAllies, Enemies());
            var b = Policy(EnemyAi(), seed: 55).ChoosePrimaryTarget(actor, SingleEnemyDamage(), NoAllies, Enemies());

            Assert.AreEqual(((Combatant)a).SlotIndex, ((Combatant)b).SlotIndex); // 시드 고정 → 재현
        }

        // 가중치 = 1 + bias × (1 − HP비율). 방향이 뒤집히면(멀쩡한 적을 노리면) 이 테스트가 깨진다.
        // 표본을 시드 0..199로 고정해 통계적이지만 완전히 결정적이다.
        [Test]
        public void 적_가중랜덤은_다친_대상을_더_자주_고른다()
        {
            var actor = Actor(atk: 100);
            int woundedPicks = 0, healthyPicks = 0;

            for (int seed = 0; seed < 200; seed++)
            {
                var wounded = Enemy(0, 100, 1000);   // 비율 0.1 → 가중 1 + 0.5×0.9 = 1.45
                var healthy = Enemy(1, 1000, 1000);  // 비율 1.0 → 가중 1 + 0.5×0.0 = 1.00
                var enemies = new List<ICombatant> { wounded, healthy };

                var target = Policy(EnemyAi(), seed).ChoosePrimaryTarget(actor, SingleEnemyDamage(), NoAllies, enemies);
                if (ReferenceEquals(target, wounded)) woundedPicks++; else healthyPicks++;
            }

            // 기대 확률 1.45/2.45 ≈ 59% vs 41%. 방향만 못박고 정확한 비율은 요구하지 않는다.
            Assert.Greater(woundedPicks, healthyPicks,
                $"저HP 편향이 뒤집혔다(다친 {woundedPicks} vs 멀쩡 {healthyPicks})");
        }

        // 적 막타는 확률 층이라 "항상"도 "전혀"도 아니다. 표본을 시드 0..199로 고정해 통계적이지만 결정적이다.
        [Test]
        public void 적_막타층은_처치가능한_대상을_확률적으로_고른다()
        {
            // atk 100·power 1 → 예상 하한 ≈ 95. killable(HP 50)만 확정 처치.
            // 기저 층은 bias 0(균등)으로 꺼 둬, killable 편중이 오직 막타 층에서만 나오게 한다.
            var actor = Actor(atk: 100);
            int lethalPicks = 0;

            for (int seed = 0; seed < 200; seed++)
            {
                var killable = Enemy(0, 50, 1000);   // 막타 가능
                var healthy = Enemy(1, 1000, 1000);  // 막타 불가
                var enemies = new List<ICombatant> { killable, healthy };

                var target = Policy(EnemyAi(lethalChance: 0.6f, lowHpBias: 0f), seed)
                    .ChoosePrimaryTarget(actor, SingleEnemyDamage(), NoAllies, enemies);
                if (ReferenceEquals(target, killable)) lethalPicks++;
            }

            // 기대 = 0.6(막타) + 0.4×0.5(기저 균등) = 80%. 양쪽 경계만 못박고 정확한 비율은 요구하지 않는다.
            Assert.Greater(lethalPicks, 120, $"막타 층이 사실상 꺼져 있다(처치 {lethalPicks}/200)");
            Assert.Less(lethalPicks, 200, $"확률이어야 하는데 매번 막타를 친다(처치 {lethalPicks}/200)");
        }

        [Test]
        public void 적_막타확률이_0이면_처치가능한_대상을_우선하지_않는다()
        {
            // 확률 0 = 막타 층 미사용(기존 적 AI 동작). 기저 균등이라 멀쩡한 대상도 뽑혀야 한다.
            var actor = Actor(atk: 100);
            int healthyPicks = 0;

            for (int seed = 0; seed < 200; seed++)
            {
                var killable = Enemy(0, 50, 1000);
                var healthy = Enemy(1, 1000, 1000);
                var enemies = new List<ICombatant> { killable, healthy };

                var target = Policy(EnemyAi(lethalChance: 0f, lowHpBias: 0f), seed)
                    .ChoosePrimaryTarget(actor, SingleEnemyDamage(), NoAllies, enemies);
                if (ReferenceEquals(target, healthy)) healthyPicks++;
            }

            Assert.Greater(healthyPicks, 60, $"막타 확률 0인데 처치 대상으로 쏠렸다(멀쩡 {healthyPicks}/200)");
        }

        [Test]
        public void 적_가중치가_0이면_HP와_무관하게_고른다()
        {
            // bias 0 = 완전 균등. 다친 대상이 있어도 항상 그쪽으로 쏠리지 않아야 한다.
            // wounded(HP 1)는 막타 대상이라 막타 층을 꺼야 기저 층만 측정된다.
            var actor = Actor(atk: 100);
            int healthyPicks = 0;

            for (int seed = 0; seed < 200; seed++)
            {
                var wounded = Enemy(0, 1, 1000);
                var healthy = Enemy(1, 1000, 1000);
                var enemies = new List<ICombatant> { wounded, healthy };

                var target = Policy(EnemyAi(lethalChance: 0f, lowHpBias: 0f), seed)
                    .ChoosePrimaryTarget(actor, SingleEnemyDamage(), NoAllies, enemies);
                if (ReferenceEquals(target, healthy)) healthyPicks++;
            }

            Assert.Greater(healthyPicks, 0, "bias 0인데 멀쩡한 대상이 한 번도 안 뽑히면 균등이 아니다");
        }

        [Test]
        public void PreviewDamage는_난수를_소비하지_않고_실제_피해_하한이다()
        {
            var attacker = new Stats { atk = 100 };
            var target = new Stats { def = 0 };

            var combat = Combat(seed: 7);
            int preview = combat.PreviewDamage(attacker, target, 1f);

            // 미리보기 후에도 실제 굴림 수열이 밀리지 않아야 한다: 같은 시드의 새 파이프라인과 결과가 일치.
            var fresh = Combat(seed: 7);
            for (int i = 0; i < 20; i++)
            {
                int rolled = combat.ComputeDamage(attacker, target, 1f).Amount;
                int expected = fresh.ComputeDamage(attacker, target, 1f).Amount;
                Assert.AreEqual(expected, rolled, "PreviewDamage가 난수 수열을 소비하면 안 된다");
                Assert.LessOrEqual(preview, rolled, "미리보기는 실제 피해의 하한이어야 한다");
            }
        }
    }
}
