using System.Collections.Generic;
using System.IO;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    /// <summary>
    /// 레벨업 트랜잭션 검증. 실제 지갑·세이브(임시 파일)로 프로덕션 경로를 태우고,
    /// 성공/거부 각 경로의 부수효과(재화 차감·레벨 증가·영속)를 되읽어 확인한다.
    /// </summary>
    public sealed class GrowthServiceTests
    {
        private string _path;
        private readonly List<Object> _assets = new List<Object>();
        private readonly List<CurrencyWallet> _wallets = new List<CurrencyWallet>();

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "eclipse_growth_test.json");
            File.Delete(_path);
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(_path);
            foreach (var w in _wallets) w.Dispose();
            _wallets.Clear();
            foreach (var a in _assets) Object.DestroyImmediate(a);
            _assets.Clear();
        }

        private OwnedCharacter Owned(int level, int hp = 1000, int atk = 175, int def = 60, int spd = 120)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.id = "char_test";
            so.baseStats = new Stats { hp = hp, atk = atk, def = def, spd = spd, critRate = 0.3f, critDamage = 2.0f };
            var curve = ScriptableObject.CreateInstance<GrowthCurve>();
            curve.growthRate = 0.07f;
            curve.maxLevel = 30;
            so.growthCurve = curve;
            _assets.Add(so);
            _assets.Add(curve);
            return new OwnedCharacter(so, level);
        }

        private GrowthService Service(OwnedCharacter owned, int gold, int costCoefficient = 100)
        {
            var wallet = new CurrencyWallet(0, gold, 0);
            _wallets.Add(wallet);
            var save = new SaveService(new PlayerSave(new List<OwnedCharacter> { owned }), wallet, new StageProgress(), _path);
            var config = ScriptableObject.CreateInstance<GrowthConfigSO>();
            config.levelUpCostCoefficient = costCoefficient;
            _assets.Add(config);
            return new GrowthService(new CurrencyService(wallet), save, config);
        }

        [Test]
        public void 성공하면_비용_차감_레벨증가_세이브가_함께_일어난다()
        {
            var owned = Owned(level: 5);
            var service = Service(owned, gold: 1000); // 비용 = 100 × 5 = 500

            var result = service.TryLevelUp(owned);

            Assert.That(result, Is.EqualTo(LevelUpResult.Success));
            Assert.That(owned.Level, Is.EqualTo(6), "레벨 +1");
            // 세이브 영속 확인: 파일을 되읽어 증가된 레벨이 쓰였는지 본다(Save 호출 증거).
            var reloaded = SaveService.LoadOrNew(_path);
            Assert.That(reloaded.owned[0].level, Is.EqualTo(6), "증가된 레벨이 영속됐다");
            Assert.That(reloaded.gold, Is.EqualTo(500), "1000 − 500 차감이 영속됐다");
        }

        [Test]
        public void 만렙이면_결제하지_않고_거부한다()
        {
            var owned = Owned(level: 30);
            var service = Service(owned, gold: 100000);

            var result = service.TryLevelUp(owned);

            Assert.That(result, Is.EqualTo(LevelUpResult.MaxLevel));
            Assert.That(owned.Level, Is.EqualTo(30), "무변경");
            Assert.That(File.Exists(_path), Is.False, "세이브도 결제도 없었다");
        }

        [Test]
        public void 금화가_부족하면_레벨을_올리지_않고_거부한다()
        {
            var owned = Owned(level: 5);
            var service = Service(owned, gold: 100); // 비용 500 > 잔액 100

            var result = service.TryLevelUp(owned);

            Assert.That(result, Is.EqualTo(LevelUpResult.InsufficientGold));
            Assert.That(owned.Level, Is.EqualTo(5), "레벨 무변경");
            Assert.That(File.Exists(_path), Is.False, "세이브 미호출");
        }

        [Test]
        public void 레벨업_결과가_다음_전투_스탯에_반영되고_SPD_치명은_불변이다()
        {
            var owned = Owned(level: 1);
            var before = Combatant.FromCharacter(owned, 0).EffectiveStats;

            Service(owned, gold: 1000).TryLevelUp(owned); // Lv1 → Lv2
            var after = Combatant.FromCharacter(owned, 0).EffectiveStats;

            Assert.That(after.hp, Is.GreaterThan(before.hp), "HP 반영");
            Assert.That(after.atk, Is.GreaterThan(before.atk), "ATK 반영");
            Assert.That(after.def, Is.GreaterThan(before.def), "DEF 반영");
            Assert.That(after.spd, Is.EqualTo(before.spd), "SPD 불변(ATB 순서 회귀)");
            Assert.That(after.critRate, Is.EqualTo(before.critRate));
            Assert.That(after.critDamage, Is.EqualTo(before.critDamage));
        }

        [Test]
        public void 전투_생성_스탯은_도메인_스케일과_동일하다_MAR22()
        {
            // 상세 화면(CharacterDetailViewModel)과 전투(Combatant.FromCharacter)가 같은 정본 빌더
            // (CharacterStats.BuildAllyStats)를 태우는지 확증한다. 전투가 도메인 스케일을 벗어나 하드코딩되면 이 테스트가 깨진다.
            var owned = Owned(level: 12);
            var domain = CharacterStats.BuildAllyStats(owned.Definition, owned.Level, owned.AscensionTier, null);

            var combat = Combatant.FromCharacter(owned, 0).EffectiveStats; // 버프 없음 → 기본 스탯

            Assert.That(combat.hp, Is.EqualTo(domain.hp));
            Assert.That(combat.atk, Is.EqualTo(domain.atk));
            Assert.That(combat.def, Is.EqualTo(domain.def));
            Assert.That(combat.spd, Is.EqualTo(domain.spd));
        }
    }
}
