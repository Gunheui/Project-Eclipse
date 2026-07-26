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

        private CharacterSO Character(string id, int maxLevel = 0)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.id = id;
            if (maxLevel > 0)
            {
                var curve = ScriptableObject.CreateInstance<GrowthCurve>();
                curve.growthRate = 0.07f;
                curve.maxLevel = maxLevel;
                so.growthCurve = curve;
                _assets.Add(curve);
            }
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
            var ownedB = new OwnedCharacter(b, 12, ascensionTier: 1, skillLevels: new[] { 2, 1, 3 });
            var save = new PlayerSave(new List<OwnedCharacter> { ownedA, ownedB });
            save.Party[0] = ownedA;
            save.Party[3] = ownedB;
            save.PityCounter = 42;
            save.PickupGuaranteed = true;

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
            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, restoredA.SkillLevels, "미강화 기본값");
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, restoredB.SkillLevels, "스킬 레벨 왕복 보존");
            Assert.AreEqual(42, restored.PityCounter);
            Assert.IsTrue(restored.PickupGuaranteed);

            // 파티: 슬롯 인덱스 유지 + 보유 목록과 동일 인스턴스(참조 동등성 기반 편성 검증의 전제).
            Assert.AreSame(restoredA, restored.Party[0]);
            Assert.IsNull(restored.Party[1]);
            Assert.IsNull(restored.Party[2]);
            Assert.AreSame(restoredB, restored.Party[3]);

            var wallet2 = new CurrencyWallet(data.essence, data.gold, data.manual);
            Assert.AreEqual(0, wallet2.Essence.CurrentValue, "시작 보석 0(가챠 구현 전)");
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
            Assert.AreEqual(SaveData.CurrentVersion, corrupt.version);
            Assert.AreEqual(0, corrupt.owned.Count);

            // 버전 불일치 — 부분 역직렬화된 반쪽 상태 대신 신규 계정.
            File.WriteAllText(_path, "{\"version\":99,\"gold\":42}");
            Assert.AreEqual(1000, SaveService.LoadOrNew(_path).gold);
        }

        [Test]
        public void LoadOrNew_구버전_세이브는_마이그레이션_없이_신규_계정으로_리셋된다()
        {
            // 확정 정책: 버전 불일치 = 신규 계정. v1 → v2 필드별 마이그레이션 경로는 의도적으로 없다.
            File.WriteAllText(_path,
                "{\"version\":1,\"gold\":9999,\"essence\":3000,\"owned\":[{\"id\":\"char_a\",\"level\":20,\"ascension\":2}]}");

            var data = SaveService.LoadOrNew(_path);

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0, data.owned.Count, "구버전 보유 목록은 승계되지 않는다");
            Assert.AreEqual(1000, data.gold, "신규 계정 기본값");
            Assert.AreEqual(0, data.essence, "신규 계정 기본값(보석 0)");
        }

        [Test]
        public void BuildPlayerSave_스킬레벨과_돌파를_기본값과_경계로_정규화한다()
        {
            var data = new SaveData
            {
                owned = new List<OwnedEntry>
                {
                    new OwnedEntry { id = "char_a", level = 3, skillLevels = null, ascension = 99 },
                    new OwnedEntry { id = "char_b", level = 3, skillLevels = new[] { 9, 0 }, ascension = -5 },
                },
            };
            var catalog = new Dictionary<string, CharacterSO>
            {
                ["char_a"] = Character("char_a"),
                ["char_b"] = Character("char_b"),
            };

            var restored = SaveService.BuildPlayerSave(data, catalog);

            var a = restored.OwnedCharacters[0];
            var b = restored.OwnedCharacters[1];
            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, a.SkillLevels, "null 배열 → 기본값 1로 채움");
            Assert.AreEqual(OwnedCharacter.MaxAscensionTier, a.AscensionTier, "상한 초과 → 상한 고정");
            CollectionAssert.AreEqual(new[] { 3, 1, 1 }, b.SkillLevels, "범위 밖 값 클램프 + 길이 부족 채움");
            Assert.AreEqual(0, b.AscensionTier, "음수 → 0 고정");
        }

        [Test]
        public void BuildPlayerSave_범위_밖_레벨은_성장곡선_상한으로_고정한다()
        {
            // 손상·수기수정 세이브 또는 maxLevel 하향 밸런스 변경 — 그대로 두면 BuildAllyStats이
            // 전투 진입·상세 화면에서 예외를 던지므로 복원 경계에서 고정한다.
            var data = new SaveData
            {
                owned = new List<OwnedEntry>
                {
                    new OwnedEntry { id = "char_a", level = 99 },
                    new OwnedEntry { id = "char_b", level = 0 },
                },
            };
            var catalog = new Dictionary<string, CharacterSO>
            {
                ["char_a"] = Character("char_a", maxLevel: 30),
                ["char_b"] = Character("char_b", maxLevel: 30),
            };

            var restored = SaveService.BuildPlayerSave(data, catalog);

            Assert.AreEqual(30, restored.OwnedCharacters[0].Level, "상한 초과 → maxLevel 고정");
            Assert.AreEqual(1, restored.OwnedCharacters[1].Level, "하한 미만 → 1 고정");
            // 고정된 레벨로 전투·상세 화면 경로가 던지지 않는다.
            Assert.DoesNotThrow(() => CharacterStats.BuildAllyStats(
                restored.OwnedCharacters[0].Definition, restored.OwnedCharacters[0].Level, 0, null));
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
