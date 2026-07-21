using System.Collections.Generic;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    public class StageSelectTests
    {
        // --- 인메모리 데이터 빌더 ---

        private static StageSO Stage(string id, bool isBoss)
        {
            var s = ScriptableObject.CreateInstance<StageSO>();
            s.id = id;
            s.displayName = id;
            s.isBoss = isBoss;
            return s;
        }

        // 마지막 스테이지를 보스로 두는 장. StageProgress 데모 init과 맞추려면 id="chapter_01".
        private static ChapterSO Chapter(string id, int number, int stageCount)
        {
            var c = ScriptableObject.CreateInstance<ChapterSO>();
            c.id = id;
            c.number = number;
            c.displayName = id;
            c.stages = new StageSO[stageCount];
            for (int i = 0; i < stageCount; i++)
                c.stages[i] = Stage($"{id}_{i + 1}", isBoss: i == stageCount - 1);
            return c;
        }

        private static StageSelectViewModel BuildVm(NavigationContext nav = null)
            => new StageSelectViewModel(new[] { Chapter("chapter_01", 1, 5) }, new StageProgress(), nav ?? new NavigationContext());

        // --- StateOf 공식: 경계 인덱스에서 3상태가 정확한가 ---

        [Test]
        public void StateOf_인덱스와_클리어수_경계에서_3상태를_정확히_계산()
        {
            Assert.AreEqual(StageState.Open, StageProgress.StateOf(0, 0), "미클리어 장의 1스테이지는 열림");
            Assert.AreEqual(StageState.Cleared, StageProgress.StateOf(0, 2), "클리어 수보다 앞이면 클리어");
            Assert.AreEqual(StageState.Cleared, StageProgress.StateOf(1, 2));
            Assert.AreEqual(StageState.Open, StageProgress.StateOf(2, 2), "클리어 수와 같은 인덱스가 열림");
            Assert.AreEqual(StageState.Locked, StageProgress.StateOf(3, 2), "클리어 수보다 뒤는 잠김");
            Assert.AreEqual(StageState.Cleared, StageProgress.StateOf(4, 5), "전부 깬 장의 보스는 클리어");
            Assert.AreEqual(StageState.Open, StageProgress.StateOf(4, 4), "앞 4개 클리어면 보스가 열림");
        }

        // --- 데모 초기값 ---

        [Test]
        public void 데모_init은_chapter_01을_미클리어로_시드()
        {
            using var progress = new StageProgress();
            Assert.AreEqual(0, progress.ClearedCountOf("chapter_01").CurrentValue,
                "부팅 직후부터 1스테이지만 열려 해금 사슬 전체를 시연할 수 있다");
        }

        [Test]
        public void ClearedCountOf는_미등록_장이면_예외()
        {
            using var progress = new StageProgress();
            Assert.Throws<KeyNotFoundException>(() => progress.ClearedCountOf("chapter_99"));
        }

        // --- TryMarkCleared: 순차만 수용, 비순차·미등록·음수 거부 ---

        [Test]
        public void TryMarkCleared는_현재_열린_스테이지만_순차_수용()
        {
            using var progress = new StageProgress(); // chapter_01 = 0 → index 0이 열림

            Assert.IsTrue(progress.TryMarkCleared("chapter_01", 0), "열린 스테이지 클리어는 수용");
            Assert.AreEqual(1, progress.ClearedCountOf("chapter_01").CurrentValue, "클리어 수가 1 증가");
        }

        [Test]
        public void TryMarkCleared는_비순차_미등록_음수를_거부()
        {
            using var progress = new StageProgress(); // chapter_01 = 0

            Assert.IsFalse(progress.TryMarkCleared("chapter_01", 4), "건너뛴 인덱스는 거부");
            Assert.IsFalse(progress.TryMarkCleared("chapter_99", 0), "미등록 장은 거부");
            Assert.IsFalse(progress.TryMarkCleared("chapter_01", -1), "음수 인덱스는 거부");
            Assert.AreEqual(0, progress.ClearedCountOf("chapter_01").CurrentValue, "거부 시 값 불변");

            progress.TryMarkCleared("chapter_01", 0);
            Assert.IsFalse(progress.TryMarkCleared("chapter_01", 0), "이미 깬 인덱스는 거부");
            Assert.AreEqual(1, progress.ClearedCountOf("chapter_01").CurrentValue, "재클리어로 값이 늘지 않는다");
        }

        [Test]
        public void TryMarkCleared는_장의_모든_스테이지_클리어_후_상한을_넘지_않는다()
        {
            using var progress = new StageProgress(); // chapter_01 = 0, 총 5스테이지

            Assert.IsTrue(progress.TryMarkCleared("chapter_01", 0));
            Assert.IsTrue(progress.TryMarkCleared("chapter_01", 1));
            Assert.IsTrue(progress.TryMarkCleared("chapter_01", 2));
            Assert.IsTrue(progress.TryMarkCleared("chapter_01", 3));
            Assert.IsTrue(progress.TryMarkCleared("chapter_01", 4)); // 보스까지 → cleared=5
            Assert.AreEqual(5, progress.ClearedCountOf("chapter_01").CurrentValue);

            Assert.IsFalse(progress.TryMarkCleared("chapter_01", 5), "존재하지 않는 인덱스(상한)는 거부");
            Assert.AreEqual(5, progress.ClearedCountOf("chapter_01").CurrentValue, "상한 초과 시 값 불변");
        }

        // --- Select: 잠긴 아이템은 선택 차단, 열린/클리어 아이템만 선택 기록(화면 전환은 View 몫) ---

        [Test]
        public void Select는_잠긴_스테이지면_선택을_차단()
        {
            var vm = BuildVm(); // cleared=0 → index 1~4는 Locked

            var locked = vm.Items[3];
            Assert.AreEqual(StageState.Locked, locked.State.CurrentValue);

            Assert.IsFalse(vm.Select(locked), "잠긴 스테이지는 false를 돌려 편성 진입을 막는다");
            Assert.IsNull(vm.SelectedStage.Value, "잠긴 스테이지는 선택되지 않는다");

            vm.Dispose();
        }

        [Test]
        public void Select는_열린_스테이지면_선택하고_true를_반환()
        {
            var vm = BuildVm(); // cleared=0 → index 0만 Open

            var open = vm.Items[0];
            Assert.AreEqual(StageState.Open, open.State.CurrentValue);

            Assert.IsTrue(vm.Select(open), "열린 스테이지는 true를 돌려 편성 진입을 허용한다");
            Assert.AreSame(open, vm.SelectedStage.Value, "선택 상태가 갱신된다");

            vm.Dispose();
        }

        [Test]
        public void Select는_선택_스테이지를_캐리어에_기록하고_이전_파티를_클리어()
        {
            var nav = new NavigationContext { SelectedParty = new List<OwnedCharacter>() };
            var vm = BuildVm(nav); // cleared=0 → index 0만 Open

            var open = vm.Items[0];
            vm.Select(open);

            Assert.AreSame(open.Stage, nav.SelectedStage,
                "편성·전투 스코프가 읽을 SelectedStage 캐리어에 선택 StageSO가 실린다");
            Assert.AreSame(vm.SelectedChapter.Value, nav.SelectedChapter,
                "전투 후 클리어 마킹이 스테이지 인덱스를 파생할 장도 함께 실린다");
            Assert.IsNull(nav.SelectedParty, "새 편성 시작이므로 이전 파티는 클리어된다");

            vm.Dispose();
        }

        [Test]
        public void Select는_잠긴_스테이지면_캐리어를_건드리지_않는다()
        {
            var nav = new NavigationContext();
            var vm = BuildVm(nav); // cleared=0 → index 3은 Locked

            vm.Select(vm.Items[3]);

            Assert.IsNull(nav.SelectedStage, "잠긴 스테이지는 캐리어에 실리지 않는다");

            vm.Dispose();
        }
    }
}
