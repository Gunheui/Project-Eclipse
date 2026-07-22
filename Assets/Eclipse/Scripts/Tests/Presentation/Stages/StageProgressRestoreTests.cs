using Eclipse.Data;
using Eclipse.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    /// <summary>
    /// StageProgress 복원(Restore)·스냅샷(Snapshot) 검증 — 장이 lazy 등록되므로
    /// "등록 전 복원(보류→소비)"과 "등록 후 복원(제자리 갱신)" 두 경로가 모두 성립해야 한다.
    /// </summary>
    public sealed class StageProgressRestoreTests
    {
        private ChapterSO _chapter;

        [SetUp]
        public void SetUp()
        {
            _chapter = ScriptableObject.CreateInstance<ChapterSO>();
            _chapter.id = "chapter_01";
            _chapter.stages = new StageSO[3];
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_chapter);
        }

        [Test]
        public void Restore_BeforeRegistration_AppliesClampedValueOnFirstAccess()
        {
            var progress = new StageProgress();
            progress.Restore("chapter_01", 99); // 총 스테이지 수(3) 초과 — 등록 시점에 클램프되어야 한다.

            Assert.AreEqual(3, progress.ClearedCountOf(_chapter).CurrentValue);
            progress.Dispose();
        }

        [Test]
        public void Restore_AfterRegistration_UpdatesExistingPropertyInPlace()
        {
            var progress = new StageProgress();
            var cleared = progress.ClearedCountOf(_chapter); // 등록 + 구독 대상 확보

            progress.Restore("chapter_01", 2);
            Assert.AreEqual(2, cleared.CurrentValue); // 같은 ReactiveProperty가 갱신됐다(구독 유지).

            progress.Restore("chapter_01", -5);
            Assert.AreEqual(0, cleared.CurrentValue); // 음수는 0으로 클램프.
            progress.Dispose();
        }

        [Test]
        public void Snapshot_IncludesPendingChaptersBeforeFirstAccess()
        {
            var progress = new StageProgress();
            progress.Restore("chapter_02", 1); // 복원만 하고 화면은 연 적 없음 — 저장 시 유실되면 안 된다.

            CollectionAssert.Contains(progress.Snapshot(), ("chapter_02", 1));
            progress.Dispose();
        }

        [Test]
        public void Snapshot_AfterRegistration_EmitsSingleRecordPerChapter()
        {
            var progress = new StageProgress();
            progress.Restore("chapter_01", 2);
            progress.ClearedCountOf(_chapter); // 보류값 소비·등록

            var records = System.Linq.Enumerable.ToList(progress.Snapshot());
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual(("chapter_01", 2), records[0]);
            progress.Dispose();
        }
    }
}
