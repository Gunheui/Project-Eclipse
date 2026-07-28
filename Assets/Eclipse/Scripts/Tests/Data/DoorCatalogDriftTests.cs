using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEditor;

namespace Eclipse.Tests
{
    /// <summary>
    /// 실제 문 카탈로그 에셋의 가중치가 기획 표에서 벗어나는 것을 잡는다. 캐릭터 문은 파티 슬롯 4개로
    /// 갈라지므로 라인업 합은 27×4 + 26 + 30 + 16 + 20 = 200이고, 이 합이 문 등장률 전체의 분모다.
    /// </summary>
    public class DoorCatalogDriftTests
    {
        private static readonly Dictionary<DoorKind, int> ExpectedWeight = new Dictionary<DoorKind, int>
        {
            [DoorKind.CharacterBuff] = 27,
            [DoorKind.Curse] = 26,
            [DoorKind.Gold] = 30,
            [DoorKind.Manual] = 16,
            [DoorKind.Essence] = 20,
        };

        private const int ExpectedLineupTotal = 200;

        private static DoorCatalogSO Load()
            => AssetDatabase.FindAssets("t:DoorCatalogSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DoorCatalogSO>)
                .Single();

        [Test]
        public void 문_가중치가_기획표와_일치한다()
        {
            var actual = Load().doors.ToDictionary(d => d.kind, d => d.weight);

            CollectionAssert.AreEquivalent(ExpectedWeight.Keys, actual.Keys, "문 카탈로그 구성이 기획표와 다르다");

            var failures = ExpectedWeight
                .Where(pair => actual[pair.Key] != pair.Value)
                .Select(pair => $"{pair.Key}: 기대 {pair.Value}, 실제 {actual[pair.Key]}")
                .ToList();

            Assert.That(failures, Is.Empty, "문 가중치 드리프트 감지:\n" + string.Join("\n", failures));
        }

        [Test]
        public void 라인업_가중_합이_이백이다()
        {
            var doors = Load().doors;
            int total = doors.Sum(d =>
                d.kind == DoorKind.CharacterBuff ? d.weight * PlayerSave.PartySlotCount : d.weight);

            Assert.AreEqual(ExpectedLineupTotal, total);
        }

        [Test]
        public void 캐릭터_문_문구는_이름_자리를_비워_둔다()
        {
            var character = Load().doors.Single(d => d.kind == DoorKind.CharacterBuff);

            Assert.IsTrue(character.displayName.Contains("{0}"), "표시명에 파티원 이름 자리가 있어야 한다");
            Assert.IsTrue(character.promiseText.Contains("{0}"), "약속 문구에 파티원 이름 자리가 있어야 한다");
        }

        [Test]
        public void 문_문구에_확률과_금액이_적혀_있지_않다()
        {
            var offenders = Load().doors
                // 이름 자리 "{0}"의 0은 표기 숫자가 아니므로 검사 전에 걷어 낸다.
                .Where(d => Strip(d.promiseText).Contains("%") || Strip(d.promiseText).Any(char.IsDigit))
                .Select(d => $"{d.kind}: {d.promiseText}")
                .ToList();

            Assert.That(offenders, Is.Empty, "문에는 종류와 약속만 적는다:\n" + string.Join("\n", offenders));
        }

        private static string Strip(string promise) => promise.Replace("{0}", string.Empty);
    }
}
