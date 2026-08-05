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

    }
}
