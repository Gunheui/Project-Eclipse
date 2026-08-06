using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using NUnit.Framework;
using UnityEditor;

namespace Eclipse.Tests
{
    /// <summary>
    /// 적 기본 HP가 기획 표에서 벗어나는 것을 잡는다. 챕터 난이도 배수가 이 값 위에 곱해지므로
    /// 여기가 달라지면 런 전체의 체감 난이도가 통째로 밀린다.
    /// </summary>
    public class EnemyStatDriftTests
    {
        private static readonly Dictionary<string, int> ExpectedHp = new Dictionary<string, int>
        {
            ["enemy_slime"] = 300,
            ["enemy_hound"] = 1400,
            ["enemy_swordsman"] = 1300,
            ["enemy_blossom"] = 1650,
            ["enemy_spider"] = 900,
            ["enemy_elite_mirea"] = 3200,
            ["enemy_boss_barkan"] = 4800,
        };

        [Test]
        public void 적_기본_HP가_기획표와_일치한다()
        {
            var actual = AssetDatabase.FindAssets("t:EnemySO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EnemySO>)
                .ToDictionary(e => e.id, e => e.baseStats.hp);

            CollectionAssert.AreEquivalent(ExpectedHp.Keys, actual.Keys, "적 에셋 구성이 기획표와 다르다");

            var failures = ExpectedHp
                .Where(pair => actual[pair.Key] != pair.Value)
                .Select(pair => $"{pair.Key}: 기대 {pair.Value}, 실제 {actual[pair.Key]}")
                .ToList();

            Assert.That(failures, Is.Empty, "적 HP 드리프트 감지:\n" + string.Join("\n", failures));
        }
    }
}
