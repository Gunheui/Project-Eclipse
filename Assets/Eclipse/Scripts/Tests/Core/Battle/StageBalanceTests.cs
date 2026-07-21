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
    // 실제 게임 데이터(chapter_01 · BattleConstants · 로스터 캐릭터)로 전 스테이지를 헤드리스 오토 전투로 돌려
    // 밸런스를 실측한다. 합/불만 보는 게 아니라 스테이지별 소요 행동 수를 로그로 남겨 튜닝 곡선을 만든다.
    // 실패는 코드가 아니라 EnemySO 스탯 / BattleConstants.asset 데이터로 잡는다.
    public class StageBalanceTests
    {
        private const string ChapterPath = "Assets/Eclipse/GameData/Chapters/chapter_01.asset";
        private const string ConstantsPath = "Assets/Eclipse/GameData/Battle/BattleConstants.asset";

        // AppLifetimeScope.prefab의 더미 로스터 순서·레벨과 같은 편성(전투는 앞 4명만 참전).
        private static readonly string[] PartyNames = { "Mira", "Rien", "Kai", "Sera" };
        private const int PartyLevel = 1;

        private static readonly int[] Seeds = { 1, 2, 3, 4, 5 };

        private sealed class FakeSceneFlow : ISceneFlow
        {
            public UniTask ToBattleAsync() => UniTask.CompletedTask;
            public UniTask ToMainAsync() => UniTask.CompletedTask;
        }

        private static ChapterSO Chapter => AssetDatabase.LoadAssetAtPath<ChapterSO>(ChapterPath);

        // (스테이지 인덱스, 시드) 전 조합. 스테이지 5 × 시드 5 = 25판.
        // Returns(null): [UnityTest]는 IEnumerator를 돌려주므로 TestCaseData에 기대 반환값을 명시해야 한다.
        private static IEnumerable<TestCaseData> Cases()
        {
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterSO>(ChapterPath);
            for (int i = 0; i < chapter.stages.Length; i++)
                foreach (int seed in Seeds)
                    yield return new TestCaseData(i, seed)
                        .SetName($"스테이지{i + 1}_시드{seed}")
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

        private static BattleFactory BuildFactory(BattleConstantsSO constants, int seed)
        {
            var targeting = new TargetResolver();
            var combat = new CombatPipeline(new DamagePipeline(
                constants.defenseK, constants.varianceMin, constants.varianceMax,
                new SeededRandom(BattleSeed.For(seed, BattleSeed.Stream.Damage))));
            var executor = new SkillExecutor(combat, targeting);
            return new BattleFactory(constants, targeting, combat, executor, new FakeSceneFlow(),
                new StageProgress(), new NavigationContext());
        }

        [UnityTest]
        [TestCaseSource(nameof(Cases))]
        public IEnumerator 전_스테이지가_오토로_승리하고_행동_캡_안에서_끝난다(int stageIndex, int seed)
            => UniTask.ToCoroutine(async () =>
            {
                var chapter = Chapter;
                var stage = chapter.stages[stageIndex];
                var constants = AssetDatabase.LoadAssetAtPath<BattleConstantsSO>(ConstantsPath);

                var vm = BuildFactory(constants, seed)
                    .Create(BuildParty(), stage.enemies, seed, startAuto: true);

                await vm.RunBattleAsync(null, CancellationToken.None);

                // 남은 HP까지 남긴다 — 승패만으로는 "간발의 차"와 "구조적으로 무리"가 구분되지 않는다.
                string survivors = string.Join(" ", vm.Combatants.Select(u =>
                    $"{(u.IsAlly ? "아" : "적")}{u.Name}:{u.CurrentHp.CurrentValue}/{u.MaxHp}"));
                Debug.Log($"[밸런스] {stage.id} 시드{seed} → {vm.Result.CurrentValue} / 행동 {vm.ActionCount.CurrentValue} | {survivors}");

                Assert.AreEqual(BattleResult.Victory, vm.Result.CurrentValue,
                    $"{stage.id}(시드 {seed}) 오토 승리 실패 — EnemySO 스탯 또는 BattleConstants로 조정한다");
                Assert.Less(vm.ActionCount.CurrentValue, constants.globalActionCap,
                    $"{stage.id}(시드 {seed})가 행동 캡에 닿았다 — 전투가 늘어진다");

                vm.Dispose();
            });
    }
}
