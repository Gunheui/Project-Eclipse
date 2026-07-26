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
    /// 돌파 트랜잭션 검증. 실제 세이브(임시 파일)로 프로덕션 경로를 태우고,
    /// 단계 증가·영속과 상한 거부를 확인한다.
    /// </summary>
    public sealed class AscensionServiceTests
    {
        private string _path;
        private readonly List<Object> _assets = new List<Object>();
        private readonly List<CurrencyWallet> _wallets = new List<CurrencyWallet>();

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "eclipse_ascension_test.json");
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

        private (OwnedCharacter owned, AscensionService service) Setup(int ascensionTier)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.id = "char_test";
            _assets.Add(so);
            var owned = new OwnedCharacter(so, 1, ascensionTier);
            var wallet = new CurrencyWallet();
            _wallets.Add(wallet);
            var save = new SaveService(new PlayerSave(new List<OwnedCharacter> { owned }), wallet, new StageProgress(), _path);
            return (owned, new AscensionService(save));
        }

        [Test]
        public void 성공하면_단계가_오르고_영속된다()
        {
            var (owned, service) = Setup(ascensionTier: 1);

            var result = service.TryAscend(owned);

            Assert.That(result, Is.EqualTo(AscensionResult.Success));
            Assert.That(owned.AscensionTier, Is.EqualTo(2));
            var reloaded = SaveService.LoadOrNew(_path);
            Assert.That(reloaded.owned[0].ascension, Is.EqualTo(2), "증가된 돌파 단계가 영속됐다");
        }

        [Test]
        public void 최대_단계면_무변경으로_거부한다()
        {
            var (owned, service) = Setup(ascensionTier: OwnedCharacter.MaxAscensionTier);

            var result = service.TryAscend(owned);

            Assert.That(result, Is.EqualTo(AscensionResult.MaxTier));
            Assert.That(owned.AscensionTier, Is.EqualTo(OwnedCharacter.MaxAscensionTier), "무변경");
            Assert.That(File.Exists(_path), Is.False, "세이브 미호출");
        }
    }
}
