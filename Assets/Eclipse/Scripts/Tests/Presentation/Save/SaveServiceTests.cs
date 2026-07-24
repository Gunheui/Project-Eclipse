using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    /// <summary>
    /// 세이브 저장→로드 라운드트립 검증. 임시 경로에 실제 파일을 쓰고, 프로덕션 복원 경로
    /// (LoadOrNew·BuildPlayerSave·ApplyChapters)를 그대로 태워 되읽는다.
    /// </summary>
    public sealed class SaveServiceTests
    {
        private string _path;
        private readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "eclipse_save_test.json");
            File.Delete(_path);
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(_path);
            foreach (var asset in _assets)
                Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private CharacterSO Character(string id)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.id = id;
            _assets.Add(so);
            return so;
        }

        private ChapterSO Chapter(string id, int stageCount)
        {
            var so = ScriptableObject.CreateInstance<ChapterSO>();
            so.id = id;
            so.stages = new StageSO[stageCount];
            _assets.Add(so);
            return so;
        }

        [Test]
        public void SaveLoad_RoundTrips_Roster_Party_Currency_Progress()
        {
            // 저장 측 상태: 캐릭터 2종(레벨 5/12), 파티 [a, 빈, 빈, b](중간 빈칸 불변식), 재화 변경, 1클리어.
            var a = Character("char_a");
            var b = Character("char_b");
            var ownedA = new OwnedCharacter(a, 5);
            var ownedB = new OwnedCharacter(b, 12, ascensionTier: 1);
            var save = new PlayerSave(new List<OwnedCharacter> { ownedA, ownedB });
            save.Party[0] = ownedA;
            save.Party[3] = ownedB;

            var wallet = new CurrencyWallet();
            new CurrencyService(wallet).Grant(CurrencyType.Gold, 500);

            var progress = new StageProgress();
            var chapter = Chapter("chapter_01", stageCount: 3);
            progress.TryMarkCleared(chapter, 0);

            new SaveService(save, wallet, progress, _path).Save();

            // 복원은 새 카탈로그(같은 id의 새 SO 인스턴스)로 — id 기반 복원임을 증명한다.
            var data = SaveService.LoadOrNew(_path);
            var a2 = Character("char_a");
            var b2 = Character("char_b");
            var catalog = new Dictionary<string, CharacterSO> { ["char_a"] = a2, ["char_b"] = b2 };
            var restored = SaveService.BuildPlayerSave(data, catalog);

            Assert.AreEqual(2, restored.OwnedCharacters.Count);
            var restoredA = restored.OwnedCharacters.Single(o => o.Definition == a2);
            var restoredB = restored.OwnedCharacters.Single(o => o.Definition == b2);
            Assert.AreEqual(5, restoredA.Level);
            Assert.AreEqual(12, restoredB.Level);
            Assert.AreEqual(1, restoredB.AscensionTier);

            // 파티: 슬롯 인덱스 유지 + 보유 목록과 동일 인스턴스(참조 동등성 기반 편성 검증의 전제).
            Assert.AreSame(restoredA, restored.Party[0]);
            Assert.IsNull(restored.Party[1]);
            Assert.IsNull(restored.Party[2]);
            Assert.AreSame(restoredB, restored.Party[3]);

            var wallet2 = new CurrencyWallet(data.essence, data.gold, data.manual);
            Assert.AreEqual(3000, wallet2.Essence.CurrentValue);
            Assert.AreEqual(1500, wallet2.Gold.CurrentValue);
            Assert.AreEqual(0, wallet2.Manual.CurrentValue);

            var progress2 = new StageProgress();
            SaveService.ApplyChapters(data, progress2);
            Assert.AreEqual(1, progress2.ClearedCountOf(Chapter("chapter_01", 3)).CurrentValue);

            wallet.Dispose();
            wallet2.Dispose();
            progress.Dispose();
            progress2.Dispose();
        }

        [Test]
        public void LoadOrNew_MissingCorruptOrWrongVersion_ReturnsFreshAccount()
        {
            // 파일 없음.
            Assert.AreEqual(0, SaveService.LoadOrNew(_path).owned.Count);

            // 손상 파일.
            File.WriteAllText(_path, "{broken json");
            var corrupt = SaveService.LoadOrNew(_path);
            Assert.AreEqual(1, corrupt.version);
            Assert.AreEqual(0, corrupt.owned.Count);

            // 버전 불일치 — 부분 역직렬화된 반쪽 상태 대신 신규 계정.
            File.WriteAllText(_path, "{\"version\":99,\"gold\":42}");
            Assert.AreEqual(1000, SaveService.LoadOrNew(_path).gold);
        }

        [Test]
        public void BuildPlayerSave_UnknownCharacterId_SkippedWithoutThrowing()
        {
            var data = new SaveData
            {
                owned = new List<OwnedEntry> { new OwnedEntry { id = "ghost", level = 3 } },
                party = new[] { "ghost", "", "", "" },
            };

            var restored = SaveService.BuildPlayerSave(data, new Dictionary<string, CharacterSO>());

            Assert.AreEqual(0, restored.OwnedCharacters.Count);
            Assert.IsNull(restored.Party[0]);
        }
    }
}
