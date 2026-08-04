using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Core;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.Service;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eclipse.Tests
{
    // 실제 게임 데이터(chapter_01 방 배치 · encounter_tuning · BattleConstants · 로스터)로 방 7행을
    // 시드별 헤드리스 오토 전투로 돌려 밸런스를 실측한다. 합/불이 아니라 방별 결과·행동 수·생존 HP를
    // 로그로 남겨 튜닝 곡선(보스 게이트 실측 포함)을 만든다 — 기획 §11 #4의 계측기.
    public class RunBalanceTests
    {
        private const string ChapterPath = "Assets/Eclipse/GameData/Chapters/chapter_01.asset";
        private const string TuningPath = "Assets/Eclipse/GameData/Chapters/encounter_tuning.asset";
        private const string ConstantsPath = "Assets/Eclipse/GameData/Battle/BattleConstants.asset";

        // AppLifetimeScope.prefab의 더미 로스터 순서·레벨과 같은 편성(전투는 앞 4명만 참전).
        private static readonly string[] PartyNames = { "Selene", "Kael", "Ria", "Eliana" };
        private const int PartyLevel = 1;

        private static readonly int[] Seeds = { 1, 2, 3, 4, 5 };

        // (방 인덱스, 시드) 전 조합. 방 7 × 시드 5 = 35판.
        // Returns(null): [UnityTest]는 IEnumerator를 돌려주므로 TestCaseData에 기대 반환값을 명시해야 한다.
        private static IEnumerable<TestCaseData> Cases()
        {
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterSO>(ChapterPath);
            for (int i = 0; i < chapter.rooms.Length; i++)
                foreach (int seed in Seeds)
                    yield return new TestCaseData(i, seed)
                        .SetName($"방{i + 1}_시드{seed}")
                        .Returns(null);
        }

        private static List<OwnedCharacter> BuildParty()
        {
            return PartyNames
                .Select(name => AssetDatabase.LoadAssetAtPath<CharacterSO>(
                    $"Assets/Eclipse/GameData/Characters/{name}.asset"))
                .Select(so => new OwnedCharacter(so, PartyLevel))
                .ToList();
        }

        // 방 인덱스까지 전진시킨 런 세션을 만든다 — 팩토리가 파티·챕터 계수를 세션에서 읽는다(무버프 기준 실측).
        private static ChapterRunSession SessionAt(ChapterSO chapter, EncounterTuningSO tuning, int roomIndex, int seed)
        {
            var session = new ChapterRunSession(chapter, tuning, BuildParty(), seed);
            for (int i = 0; i < roomIndex; i++)
                session.AdvanceRoom();
            return session;
        }

        [UnityTest]
        [TestCaseSource(nameof(Cases))]
        public IEnumerator 방별_오토_전투를_실측하고_종결을_보장한다(int roomIndex, int seed)
            => UniTask.ToCoroutine(async () =>
            {
                var chapter = AssetDatabase.LoadAssetAtPath<ChapterSO>(ChapterPath);
                var tuning = AssetDatabase.LoadAssetAtPath<EncounterTuningSO>(TuningPath);
                var constants = AssetDatabase.LoadAssetAtPath<BattleConstantsSO>(ConstantsPath);
                var room = chapter.rooms[roomIndex];

                // 프로덕션과 같은 스트림 분리로 방 인카운터를 생성한다. 방마다 같은 시드를 쓰면
                // 생성기 소비 순서가 달라 방별 인카운터가 겹치지 않도록 시드에 방 인덱스를 접는다.
                int roomSeed = seed * 100 + roomIndex;
                var generator = new EncounterGenerator(tuning,
                    new SeededRandom(RunSeed.For(roomSeed, RunSeed.Stream.Encounter)),
                    new SeededRandom(RunSeed.For(roomSeed, RunSeed.Stream.Mutation)));
                var encounter = room.kind == RoomKind.Boss
                    ? generator.Generate(EncounterGenerator.BossDepth, false)
                    : generator.Generate(room.depth, room.kind == RoomKind.Elite);

                var session = SessionAt(chapter, tuning, roomIndex, roomSeed);
                var vm = new BattleFactory(constants, session, tuning)
                    .Create(encounter, RunSeed.ForRoomBattle(roomSeed, roomIndex), startAuto: true);

                await vm.RunBattleAsync(null, CancellationToken.None);

                // 남은 HP까지 남긴다 — 승패만으로는 "간발의 차"와 "구조적으로 무리"가 구분되지 않는다.
                string survivors = string.Join(" ", vm.Combatants.Select(u =>
                    $"{(u.IsAlly ? "아" : "적")}{u.Name}:{u.CurrentHp.CurrentValue}/{u.MaxHp}"));
                Debug.Log($"[밸런스] 방{roomIndex + 1}({room.kind}) 시드{seed} → {vm.Result.CurrentValue} " +
                    $"/ 행동 {vm.ActionCount.CurrentValue} | {survivors}");

                // 계측기라 승패는 단정하지 않는다(보스 게이트는 패배가 정상 신호일 수 있다).
                // 단, 전투가 캡 안에서 종결되지 않으면 밸런스가 아니라 구조 문제다.
                Assert.AreNotEqual(BattleResult.InProgress, vm.Result.CurrentValue, "전투가 종결됐다");
                Assert.Less(vm.ActionCount.CurrentValue, constants.globalActionCap,
                    $"방{roomIndex + 1}(시드 {seed})가 행동 캡에 닿았다 — 전투가 늘어진다");

                vm.Dispose();
            });
    }
}
