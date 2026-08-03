using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eclipse.Tests
{
    public class PartyFormationViewModelTests
    {
        private static OwnedCharacter Owned(string name)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.id = name;
            so.displayName = name;
            return new OwnedCharacter(so, 1);
        }

        private static ChapterSO[] Chapters()
        {
            var chapter = ScriptableObject.CreateInstance<ChapterSO>();
            chapter.id = "chapter_t";
            return new[] { chapter };
        }

        private sealed class FakeSpriteProvider : ISpriteProvider
        {
            public UniTask<Sprite> LoadPortraitAsync(CharacterSO definition, CancellationToken ct = default)
                => UniTask.FromResult<Sprite>(null);
        }

        private sealed class FakeSceneFlow : ISceneFlow
        {
            public int ToBattleCount;
            public bool FailTransition;

            public UniTask ToBattleAsync()
            {
                ToBattleCount++;
                return FailTransition
                    ? UniTask.FromException(new InvalidOperationException("씬 로드 실패"))
                    : UniTask.CompletedTask;
            }

            public UniTask ToMainAsync() => UniTask.CompletedTask;
        }

        private static PartyFormationViewModel Formation(PlayerSave save, NavigationContext nav, ISceneFlow flow)
            => new PartyFormationViewModel(Chapters(), save, nav, flow, saveService: null,
                new FakeSpriteProvider(), new CharacterGrowthSignals());

        private static (PartyFormationViewModel vm, NavigationContext nav, FakeSceneFlow flow, List<OwnedCharacter> roster) Build(int rosterCount)
        {
            var roster = Enumerable.Range(0, rosterCount).Select(i => Owned("C" + i)).ToList();
            var nav = new NavigationContext();
            var flow = new FakeSceneFlow();
            var vm = Formation(new PlayerSave(roster), nav, flow);
            return (vm, nav, flow, roster);
        }

        /// <summary> 로스터 앞에서부터 4슬롯을 채운다. 런 시작에는 4인이 다 차 있어야 한다. </summary>
        private static void FillParty(PartyFormationViewModel vm, IReadOnlyList<OwnedCharacter> roster)
        {
            for (int i = 0; i < PartyFormationViewModel.SlotCount; i++)
                vm.AssignToSlot(i, roster[i]);
        }

        [Test]
        public void 초기화_시_4슬롯_모두_빈칸_count0_enter불가()
        {
            var (vm, _, _, _) = Build(4);

            Assert.AreEqual(4, vm.Slots.Count);
            Assert.IsTrue(vm.Slots.All(s => s.Value == null), "모든 슬롯이 빈칸");
            Assert.AreEqual(0, vm.PartyCount.CurrentValue);
            Assert.IsFalse(vm.CanEnter.CurrentValue, "빈 편성은 진입 불가");

            vm.Dispose();
        }

        [Test]
        public void AssignToSlot_탭한_슬롯에_배치하고_중간_빈칸을_유지()
        {
            var (vm, _, _, roster) = Build(4);

            Assert.IsTrue(vm.AssignToSlot(0, roster[1]));
            Assert.IsTrue(vm.AssignToSlot(2, roster[3]));

            Assert.AreSame(roster[1], vm.Slots[0].Value);
            Assert.IsNull(vm.Slots[1].Value, "건너뛴 슬롯은 빈칸으로 남는다");
            Assert.AreSame(roster[3], vm.Slots[2].Value);
            Assert.AreEqual(2, vm.PartyCount.CurrentValue);
            Assert.IsFalse(vm.CanEnter.CurrentValue, "4인이 되기 전에는 진입 불가");

            vm.Dispose();
        }

        [Test]
        public void SlotOccupants는_배치된_캐릭터의_항목_VM을_내보낸다()
        {
            var (vm, _, _, roster) = Build(4);

            vm.AssignToSlot(1, roster[2]);

            var item = vm.SlotOccupants[1].CurrentValue;
            Assert.IsNotNull(item);
            Assert.AreSame(roster[2], item.Owned, "슬롯이 그 캐릭터의 항목 VM을 낸다");
            Assert.AreSame(vm.Roster.First(r => r.Owned == roster[2]), item, "로스터와 같은 인스턴스를 공유한다");
            Assert.IsNull(vm.SlotOccupants[0].CurrentValue, "빈칸은 null");

            vm.ClearSlot(1);
            Assert.IsNull(vm.SlotOccupants[1].CurrentValue, "비우면 다시 null");

            vm.Dispose();
        }

        [Test]
        public void CanEnter는_4슬롯이_다_차야_true()
        {
            var (vm, _, _, roster) = Build(5);
            for (int i = 0; i < 3; i++)
                vm.AssignToSlot(i, roster[i]);

            Assert.IsFalse(vm.CanEnter.CurrentValue, "3인은 진입 불가");

            vm.AssignToSlot(3, roster[3]);
            Assert.IsTrue(vm.CanEnter.CurrentValue, "4인이 차면 진입 가능");

            vm.ClearSlot(1);
            Assert.IsFalse(vm.CanEnter.CurrentValue, "한 칸을 비우면 다시 불가");

            vm.Dispose();
        }

        [Test]
        public void AssignToSlot_이미_다른_슬롯에_있으면_이동해_중복을_막는다()
        {
            var (vm, _, _, roster) = Build(4);
            vm.AssignToSlot(0, roster[0]);

            vm.AssignToSlot(3, roster[0]);

            Assert.IsNull(vm.Slots[0].Value, "원래 슬롯은 비워진다");
            Assert.AreSame(roster[0], vm.Slots[3].Value);
            Assert.AreEqual(1, vm.PartyCount.CurrentValue, "이동이지 추가가 아니다");

            vm.Dispose();
        }

        [Test]
        public void AssignToSlot_기존_점유자는_교체된다()
        {
            var (vm, _, _, roster) = Build(4);
            vm.AssignToSlot(1, roster[0]);

            vm.AssignToSlot(1, roster[2]);

            Assert.AreSame(roster[2], vm.Slots[1].Value);
            Assert.AreEqual(1, vm.PartyCount.CurrentValue);

            vm.Dispose();
        }

        [Test]
        public void AssignToSlot_미소유_null_범위밖_슬롯을_거부하고_슬롯_불변()
        {
            var (vm, _, _, roster) = Build(4);
            var outsider = Owned("X");

            Assert.IsFalse(vm.AssignToSlot(0, outsider), "미소유 거부");
            Assert.IsFalse(vm.AssignToSlot(0, null), "null 거부");
            Assert.IsFalse(vm.AssignToSlot(4, roster[0]), "범위 밖 슬롯 거부");
            Assert.IsTrue(vm.Slots.All(s => s.Value == null), "거부 시 슬롯 불변");

            vm.Dispose();
        }

        [Test]
        public void ClearSlot_해당_슬롯만_비우고_나머지는_당겨지지_않는다()
        {
            var (vm, _, _, roster) = Build(4);
            vm.AssignToSlot(0, roster[0]);
            vm.AssignToSlot(1, roster[1]);

            vm.ClearSlot(0);

            Assert.IsNull(vm.Slots[0].Value);
            Assert.AreSame(roster[1], vm.Slots[1].Value, "뒤 슬롯은 제자리에 남는다");
            Assert.AreEqual(1, vm.PartyCount.CurrentValue);

            vm.Dispose();
        }

        [Test]
        public void StartRun_슬롯_위치를_보존해_SelectedParty에_기록()
        {
            var (vm, nav, flow, roster) = Build(4);
            vm.AssignToSlot(0, roster[2]);
            vm.AssignToSlot(1, roster[0]);
            vm.AssignToSlot(2, roster[3]);
            vm.AssignToSlot(3, roster[1]);

            vm.StartRun();

            CollectionAssert.AreEqual(new[] { roster[2], roster[0], roster[3], roster[1] },
                nav.SelectedParty.ToList(),
                "편성 칸이 전투 자리이므로 로스터 순서가 아니라 슬롯 순서로 실린다");
            Assert.AreEqual(1, flow.ToBattleCount, "전투 씬으로 진입한다");

            vm.Dispose();
        }

        [Test]
        public void 편성은_PlayerSave에_남아_VM을_새로_만들어도_복원된다()
        {
            var roster = Enumerable.Range(0, 4).Select(i => Owned("C" + i)).ToList();
            var save = new PlayerSave(roster);
            var vm = Formation(save, new NavigationContext(), new FakeSceneFlow());
            vm.AssignToSlot(2, roster[1]);
            vm.Dispose(); // 전투 진입으로 씬 스코프가 내려간 상황

            var revisited = Formation(save, new NavigationContext(), new FakeSceneFlow());

            Assert.AreSame(roster[1], revisited.Slots[2].Value, "전투를 다녀와도 편성 위치가 그대로 남는다");
            Assert.IsNull(revisited.Slots[0].Value, "빈 칸도 그대로");
            Assert.AreEqual(1, revisited.PartyCount.CurrentValue);
            Assert.IsFalse(revisited.CanEnter.CurrentValue, "복원됐어도 4인 미달이면 진입 불가");

            revisited.ClearSlot(2);
            Assert.IsNull(save.Party[2], "비우기도 저장에 반영된다");

            revisited.Dispose();
        }

        [Test]
        public void StartRun_빈_편성이면_아무_일도_하지_않는다()
        {
            var (vm, nav, flow, _) = Build(4);

            vm.StartRun();

            Assert.IsNull(nav.SelectedParty);
            Assert.AreEqual(0, flow.ToBattleCount, "빈 편성은 진입하지 않는다");

            vm.Dispose();
        }

        [Test]
        public void StartRun_4인_미달이면_진입하지_않는다()
        {
            var (vm, nav, flow, roster) = Build(4);
            for (int i = 0; i < 3; i++)
                vm.AssignToSlot(i, roster[i]);

            vm.StartRun();

            Assert.IsNull(nav.SelectedParty, "빈칸이 있는 파티는 실리지 않는다");
            Assert.AreEqual(0, flow.ToBattleCount, "전투 씬에 들어간 뒤 터지지 않도록 여기서 끊는다");

            vm.Dispose();
        }

        [Test]
        public void StartRun_전환에_실패하면_다시_시도할_수_있다()
        {
            var (vm, _, flow, roster) = Build(4);
            FillParty(vm, roster);
            flow.FailTransition = true;
            LogAssert.ignoreFailingMessages = true; // 실패한 전환은 예외를 다시 던져 드러낸다

            vm.StartRun();

            flow.FailTransition = false;
            vm.StartRun();

            Assert.AreEqual(2, flow.ToBattleCount, "실패로 화면이 남았으면 진입 버튼이 다시 살아나야 한다");

            LogAssert.ignoreFailingMessages = false;
            vm.Dispose();
        }

        [Test]
        public void StartRun_재진입은_무시된다()
        {
            var (vm, nav, flow, roster) = Build(5);
            FillParty(vm, roster);
            vm.StartRun();
            var first = nav.SelectedParty;

            vm.AssignToSlot(0, roster[4]); // 벤치 캐릭터로 교체 — 4인은 그대로 유지된다
            vm.StartRun();

            Assert.AreEqual(1, flow.ToBattleCount, "두 번째 진입은 무시된다");
            Assert.AreSame(first, nav.SelectedParty, "첫 기록이 유지된다");

            vm.Dispose();
        }
    }
}
