using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using UnityEngine;

namespace Eclipse.Tests
{
    /// <summary> 런 루프 테스트가 공유하는 인메모리 데이터 조립기(챕터·튜닝·카탈로그·파티). </summary>
    public static class RunFixtures
    {
        public static Stats S(int hp, int atk, int def, int spd)
            => new Stats { hp = hp, atk = atk, def = def, spd = spd, critRate = 0f, critDamage = 1.5f };

        public static SkillSO Skill(string id)
        {
            var s = ScriptableObject.CreateInstance<SkillSO>();
            s.id = id;
            s.displayName = id;
            s.cooldownTurns = 0;
            s.effects = new List<SkillEffect>
            {
                new SkillEffect { type = EffectType.Damage, target = TargetSelector.SingleEnemy, value = 1f }
            };
            return s;
        }

        public static EnemySO Enemy(string id)
        {
            var so = ScriptableObject.CreateInstance<EnemySO>();
            so.id = id;
            so.displayName = id;
            so.baseStats = S(500, 50, 0, 90);
            so.basicSkill = Skill(id + "_b");
            return so;
        }

        public static OwnedCharacter Owned(string id, int level = 1)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.id = id;
            so.displayName = id;
            so.baseStats = S(1000, 100, 0, 100);
            so.growthCurve = ScriptableObject.CreateInstance<GrowthCurve>();
            so.growthCurve.maxLevel = 30;
            so.basicSkill = Skill(id + "_b");
            return new OwnedCharacter(so, level);
        }

        public static List<OwnedCharacter> Party(int count)
            => Enumerable.Range(0, count).Select(i => Owned("ally_" + i)).ToList();

        /// <summary> 깊이 1~5를 전부 덮는 최소 튜닝. 풀은 slime 단일, 변이 0%. </summary>
        public static EncounterTuningSO Tuning()
        {
            var slime = Enemy("slime");
            var tuning = ScriptableObject.CreateInstance<EncounterTuningSO>();
            tuning.depths = Enumerable.Range(1, 5)
                .Select(d => new DepthPool
                {
                    depth = d, allowedPool = new[] { slime }, minCount = 1, maxCount = 2, mutationChance = 0f
                })
                .ToArray();
            tuning.boss = Enemy("boss");
            tuning.bossAdds = new[] { Enemy("add") };
            tuning.mutations = new[] { Mutation("mut_hp", StatType.Hp, 1.5f) };
            tuning.eliteStatMultiplier = 1.15f;
            return tuning;
        }

        public static MutationSO Mutation(string id, StatType axis, float multiplier)
        {
            var m = ScriptableObject.CreateInstance<MutationSO>();
            m.id = id;
            m.statAxis = axis;
            m.multiplier = multiplier;
            return m;
        }

        /// <summary> 방 배치로 챕터를 만든다. 정산 표는 넘긴 방 수 × 100골드로 채우고 승리 보너스는 골드 400이다. </summary>
        public static ChapterSO Chapter(params RoomLayout[] rooms)
        {
            var chapter = ScriptableObject.CreateInstance<ChapterSO>();
            chapter.id = "chapter_t";
            chapter.rooms = rooms;
            chapter.enemyStatMultiplier = 1f;
            chapter.currencyMultiplier = 1f;
            chapter.settlement = Enumerable.Range(0, rooms.Length + 1)
                .Select(i => new SettlementRow { gold = i * 100, manual = 0, essence = 0 })
                .ToArray();
            chapter.victoryBonus = new SettlementRow { gold = 400, manual = 0, essence = 0 };
            return chapter;
        }

        public static RoomLayout Normal(int depth, bool doorAfter)
            => new RoomLayout { kind = RoomKind.Normal, depth = depth, doorAfter = doorAfter };

        public static RoomLayout Elite(int depth, bool doorAfter)
            => new RoomLayout { kind = RoomKind.Elite, depth = depth, doorAfter = doorAfter };

        public static RoomLayout Boss()
            => new RoomLayout { kind = RoomKind.Boss, depth = 0, doorAfter = false };

        /// <summary> 기획 §3-1의 챕터 1 배치와 같은 7방 챕터. </summary>
        public static ChapterSO DocChapter()
            => Chapter(Normal(1, true), Normal(2, true), Normal(3, true), Elite(4, true),
                Normal(5, true), Normal(5, false), Boss());

        /// <summary> 문 8종 카탈로그(가중 15/15/13/11/13/15/8/10). 재화 계수는 기획 §4-4 값. </summary>
        public static DoorCatalogSO DoorCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<DoorCatalogSO>();
            catalog.doors = new[]
            {
                Door(DoorKind.Attack, 15), Door(DoorKind.Guard, 15), Door(DoorKind.Haste, 13),
                Door(DoorKind.Bond, 11), Door(DoorKind.Curse, 13), Door(DoorKind.Gold, 15),
                Door(DoorKind.Manual, 8), Door(DoorKind.Essence, 10),
            };
            catalog.goldPerDepth = 300;
            catalog.essencePerDepth = 60;
            catalog.manualBase = 1;
            catalog.manualPerDepth = 2;
            catalog.currencyJitter = 0.30f;
            return catalog;
        }

        private static DoorDefinition Door(DoorKind kind, int weight)
            => new DoorDefinition { kind = kind, displayName = kind.ToString(), weight = weight, promiseText = kind.ToString() };

        /// <summary>
        /// 계열 3장 × (공격/수호/질풍) + 특수 2장 + 저주 4장 + 인연 카드(대상 id 지정)로 이루어진 카탈로그.
        /// </summary>
        public static BuffCardCatalogSO CardCatalog(params string[] bondTargetIds)
        {
            var cards = new List<BuffCard>();
            foreach (var (tag, axis) in new[]
                     {
                         (CardTag.Attack, StatType.Atk), (CardTag.Guard, StatType.Def), (CardTag.Haste, StatType.Spd)
                     })
                for (int i = 0; i < 3; i++)
                    cards.Add(new BuffCard
                    {
                        id = $"{tag}_{i}", displayName = $"{tag}_{i}", tag = tag, weight = 25,
                        deltas = new[] { new StatDelta { axis = axis, value = 0.15f } },
                    });
            for (int i = 0; i < 2; i++)
                cards.Add(new BuffCard
                {
                    id = $"special_{i}", displayName = $"special_{i}", tag = CardTag.Special, weight = 12,
                    specialPointRestricted = true,
                    deltas = new[] { new StatDelta { axis = StatType.Hp, value = 0.07f } },
                });
            foreach (var (axis, i) in new[] { StatType.Hp, StatType.Atk, StatType.Def, StatType.Spd }
                         .Select((a, i) => (a, i)))
                cards.Add(new BuffCard
                {
                    id = $"curse_{i}", displayName = $"curse_{i}", tag = CardTag.Curse, weight = 25,
                    targetsEnemies = true,
                    deltas = new[] { new StatDelta { axis = axis, value = 0.12f } },
                });
            foreach (var id in bondTargetIds)
                cards.Add(new BuffCard
                {
                    id = $"bond_{id}", displayName = $"bond_{id}", tag = CardTag.Bond, weight = 25,
                    requiredCharacterId = id,
                    deltas = new[] { new StatDelta { axis = StatType.Atk, value = 0.25f } },
                });

            var catalog = ScriptableObject.CreateInstance<BuffCardCatalogSO>();
            catalog.cards = cards.ToArray();
            return catalog;
        }
    }
}