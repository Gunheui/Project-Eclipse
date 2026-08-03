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
            chapter.normalBackground = Background();
            chapter.eliteBackground = Background();
            chapter.bossBackground = Background();
            return chapter;
        }

        /// <summary> 어느 배경이 실려 왔는지만 구분하면 되므로 1픽셀짜리 서로 다른 인스턴스로 만든다. </summary>
        private static Sprite Background()
            => Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

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

        /// <summary> 문 5종 카탈로그(가중 27/26/30/16/20 — 라인업 합 200). 재화 계수는 기획 §4-4 값. </summary>
        public static DoorCatalogSO DoorCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<DoorCatalogSO>();
            catalog.doors = new[]
            {
                Door(DoorKind.CharacterBuff, 27), Door(DoorKind.Curse, 26), Door(DoorKind.Gold, 30),
                Door(DoorKind.Manual, 16), Door(DoorKind.Essence, 20),
            };
            catalog.goldPerDepth = 300;
            catalog.essencePerDepth = 60;
            catalog.currencyJitter = 0.30f;
            return catalog;
        }

        private static DoorDefinition Door(DoorKind kind, int weight)
            => new DoorDefinition
            {
                kind = kind,
                displayName = kind == DoorKind.CharacterBuff ? "{0}" : kind.ToString(),
                weight = weight,
                promiseText = kind == DoorKind.CharacterBuff ? "{0}" : kind.ToString(),
            };

        /// <summary>
        /// 정본 카탈로그와 같은 모양의 축소판 — 범용 3축 × 3등급 + 저주 4축 × 3등급 + 지정한 캐릭터의 유니크.
        /// </summary>
        /// <param name="uniqueTargetIds">유니크 카드를 만들어 줄 캐릭터 id. 비우면 유니크 없는 카탈로그다.</param>
        public static BuffCardCatalogSO CardCatalog(params string[] uniqueTargetIds)
        {
            var grades = new[] { CardGrade.Common, CardGrade.Rare, CardGrade.Epic };
            var cards = new List<BuffCard>();
            foreach (var axis in new[] { StatType.Atk, StatType.Def, StatType.Spd })
                foreach (var grade in grades)
                    cards.Add(new BuffCard
                    {
                        id = $"buff_{axis}_{grade}", displayName = $"buff_{axis}", grade = grade,
                        deltas = new[] { new StatDelta { axis = axis, value = 0.15f } },
                    });
            foreach (var axis in new[] { StatType.Hp, StatType.Atk, StatType.Def, StatType.Spd })
                foreach (var grade in grades)
                    cards.Add(new BuffCard
                    {
                        id = $"curse_{axis}_{grade}", displayName = $"curse_{axis}", grade = grade,
                        targetsEnemies = true,
                        deltas = new[] { new StatDelta { axis = axis, value = -0.12f } },
                    });
            foreach (var id in uniqueTargetIds)
                cards.Add(new BuffCard
                {
                    id = $"unique_{id}", displayName = $"unique_{id}", grade = CardGrade.Unique,
                    description = $"{id} 전용 효과", requiredCharacterId = id,
                    deltas = new[] { new StatDelta { axis = StatType.Atk, value = 0.25f } },
                });

            var catalog = ScriptableObject.CreateInstance<BuffCardCatalogSO>();
            catalog.cards = cards.ToArray();
            return catalog;
        }
    }
}