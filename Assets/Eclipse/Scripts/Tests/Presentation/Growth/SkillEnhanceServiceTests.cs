using System.Collections.Generic;
using System.IO;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    /// <summary>
    /// 스킬 강화 트랜잭션 검증. 실제 지갑·세이브(임시 파일)로 프로덕션 경로를 거치고,
    /// 특히 골드+교본 2재화 원자 차감(한쪽만 빠지는 반쪽 결제 금지)을 확인한다.
    /// </summary>
    public sealed class SkillEnhanceServiceTests
    {
        private string _path;
        private readonly List<Object> _assets = new List<Object>();
        private readonly List<CurrencyWallet> _wallets = new List<CurrencyWallet>();

        private CurrencyWallet _wallet;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "eclipse_skill_enhance_test.json");
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

        private OwnedCharacter Owned(int[] skillLevels = null)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.id = "char_test";
            _assets.Add(so);
            return new OwnedCharacter(so, 1, 0, skillLevels);
        }

        private SkillEnhanceService Service(OwnedCharacter owned, int gold, int manual)
        {
            _wallet = new CurrencyWallet(0, gold, manual);
            _wallets.Add(_wallet);
            var save = new SaveService(new PlayerSave(new List<OwnedCharacter> { owned }), _wallet, new ChapterProgress(), _path);
            var config = ScriptableObject.CreateInstance<GrowthConfigSO>(); // 기본값: 골드 500×스킬Lv + 교본 5
            _assets.Add(config);
            return new SkillEnhanceService(new CurrencyService(_wallet), save, config, new CharacterGrowthSignals());
        }

        [Test]
        public void 성공하면_골드와_교본이_함께_차감되고_영속된다()
        {
            var owned = Owned();
            var service = Service(owned, gold: 1000, manual: 10); // 비용 = 500 × 1 + 교본 5

            var result = service.TryEnhance(owned, 1);

            Assert.That(result, Is.EqualTo(SkillEnhanceResult.Success));
            CollectionAssert.AreEqual(new[] { 1, 2, 1 }, owned.SkillLevels, "대상 슬롯만 +1");
            var reloaded = SaveService.LoadOrNew(_path);
            CollectionAssert.AreEqual(new[] { 1, 2, 1 }, reloaded.owned[0].skillLevels, "증가된 스킬 레벨이 영속됐다");
            Assert.That(reloaded.gold, Is.EqualTo(500), "1000 − 500 차감이 영속됐다");
            Assert.That(reloaded.manual, Is.EqualTo(5), "10 − 5 차감이 영속됐다");
        }

        [Test]
        public void 비용은_현재_스킬레벨에_비례한다()
        {
            var owned = Owned(new[] { 2, 1, 1 });
            var service = Service(owned, gold: 1000, manual: 5); // 비용 = 500 × 2 = 1000

            var result = service.TryEnhance(owned, 0);

            Assert.That(result, Is.EqualTo(SkillEnhanceResult.Success));
            Assert.That(_wallet.Gold.CurrentValue, Is.EqualTo(0), "1000 전액 차감");
        }

        [Test]
        public void 교본이_부족하면_골드도_차감하지_않는다()
        {
            var owned = Owned();
            var service = Service(owned, gold: 1000, manual: 4); // 교본 5 필요 > 잔액 4

            var result = service.TryEnhance(owned, 0);

            Assert.That(result, Is.EqualTo(SkillEnhanceResult.InsufficientCurrency));
            Assert.That(_wallet.Gold.CurrentValue, Is.EqualTo(1000), "골드 무변경(원자성)");
            Assert.That(_wallet.Manual.CurrentValue, Is.EqualTo(4), "교본 무변경");
            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, owned.SkillLevels, "스킬 레벨 무변경");
            Assert.That(File.Exists(_path), Is.False, "세이브 미호출");
        }

        [Test]
        public void 골드가_부족하면_교본도_차감하지_않는다()
        {
            var owned = Owned();
            var service = Service(owned, gold: 499, manual: 10); // 골드 500 필요 > 잔액 499

            var result = service.TryEnhance(owned, 0);

            Assert.That(result, Is.EqualTo(SkillEnhanceResult.InsufficientCurrency));
            Assert.That(_wallet.Manual.CurrentValue, Is.EqualTo(10), "교본 무변경(원자성)");
            Assert.That(_wallet.Gold.CurrentValue, Is.EqualTo(499), "골드 무변경");
        }

        [Test]
        public void 만렙_스킬은_결제하지_않고_거부한다()
        {
            var owned = Owned(new[] { 3, 1, 1 });
            var service = Service(owned, gold: 100000, manual: 100);

            var result = service.TryEnhance(owned, 0);

            Assert.That(result, Is.EqualTo(SkillEnhanceResult.MaxSkillLevel));
            Assert.That(_wallet.Gold.CurrentValue, Is.EqualTo(100000), "결제 없음");
            Assert.That(File.Exists(_path), Is.False, "세이브도 결제도 없었다");
        }

        [Test]
        public void 슬롯_범위를_벗어나면_예외를_던진다()
        {
            var owned = Owned();
            var service = Service(owned, gold: 1000, manual: 10);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => service.TryEnhance(owned, -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => service.TryEnhance(owned, OwnedCharacter.SkillSlotCount));
        }
    }
}
