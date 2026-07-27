using System.IO;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    /// <summary>
    /// ChapterProgress 복원·스냅샷·멱등 마킹 검증 + 세이브 직렬화 키 왕복 —
    /// 개명 리팩터가 chapters[] 직렬화 계약(chapterId/cleared)을 건드리지 않았음의 증거.
    /// </summary>
    public sealed class ChapterProgressTests
    {
        private ChapterSO _chapter;

        [SetUp]
        public void SetUp()
        {
            _chapter = ScriptableObject.CreateInstance<ChapterSO>();
            _chapter.id = "chapter_01";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_chapter);
        }

        [Test]
        public void MarkCleared_클리어를_기록하고_재호출은_무동작이다()
        {
            var progress = new ChapterProgress();
            Assert.IsFalse(progress.IsCleared(_chapter));

            progress.MarkCleared(_chapter);
            progress.MarkCleared(_chapter);

            Assert.IsTrue(progress.IsCleared(_chapter));
            Assert.AreEqual(1, progress.Snapshot().Single().cleared, "멱등 — 값은 1을 넘지 않는다");
        }

        [Test]
        public void Restore_값을_0과_1로_좁혀_복원한다()
        {
            var progress = new ChapterProgress();

            progress.Restore("chapter_01", 3); // 구 세이브의 "클리어한 스테이지 수" 흡수
            Assert.IsTrue(progress.IsCleared(_chapter));

            progress.Restore("chapter_01", -5);
            Assert.IsFalse(progress.IsCleared(_chapter));
        }

        [Test]
        public void Snapshot_복원만_된_장도_유실_없이_포함한다()
        {
            var progress = new ChapterProgress();
            progress.Restore("chapter_02", 1); // 화면을 연 적 없는 장 — 저장 시 유실되면 안 된다

            CollectionAssert.Contains(progress.Snapshot(), ("chapter_02", 1));
        }

        [Test]
        public void 세이브_직렬화_키가_왕복에서_보존된다()
        {
            var path = Path.Combine(Path.GetTempPath(), "eclipse_chapter_roundtrip.json");
            File.Delete(path);
            try
            {
                var progress = new ChapterProgress();
                progress.MarkCleared(_chapter);
                using var wallet = new CurrencyWallet();
                new SaveService(new PlayerSave(System.Array.Empty<OwnedCharacter>().ToList()),
                    wallet, progress, path).Save();

                // 키 이름이 바뀌면 구 세이브가 조용히 초기화된다 — JSON 원문에서 키를 직접 박제한다.
                var json = File.ReadAllText(path);
                StringAssert.Contains("\"chapterId\":\"chapter_01\"", json);
                StringAssert.Contains("\"cleared\":1", json);

                var restored = new ChapterProgress();
                SaveService.ApplyChapters(SaveService.LoadOrNew(path), restored);
                Assert.IsTrue(restored.IsCleared(_chapter), "저장 → 로드 → 복원이 값을 보존한다");
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
