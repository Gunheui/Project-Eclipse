using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eclipse.Tests.View
{
    /// <summary>
    /// 유지 이펙트가 턴을 세는 규칙을 관측한다. 이펙트가 걸린 턴의 통지는 세지 않고,
    /// 「켜 두기」와 「턴마다」가 같은 구간을 덮어야 한다.
    /// </summary>
    public class VfxPlayerHoldTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private GameObject _layerPrefab;

        [SetUp]
        public void SetUp()
        {
            // 파티클 없는 빈 오브젝트로 충분하다. 이 테스트가 보는 건 스폰 횟수와 수명이다.
            _layerPrefab = Track(new GameObject("Layer"));
            _layerPrefab.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator 켜두기_2턴은_걸린_턴을_세지_않고_세_번째_통지에_걷힌다()
        {
            var player = Play(VfxHold.Continuous, holdTurns: 2);

            Assert.IsTrue(player.HasHold, "시전 직후에는 유지 중이어야 한다");
            Assert.IsTrue(player.AdvanceTurn(), "걸린 턴의 통지는 세지 않는다");
            Assert.IsTrue(player.AdvanceTurn(), "첫 턴을 세고도 한 턴 남는다");
            Assert.IsFalse(player.AdvanceTurn(), "두 턴을 다 세면 걷힌다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 턴마다_2턴은_켜두기와_같은_구간_동안_턴마다_다시_재생된다()
        {
            var player = Play(VfxHold.EachTurn, holdTurns: 2);
            var root = player.transform;

            Assert.AreEqual(1, root.childCount, "시전 턴 몫이 한 번 재생된다");
            player.AdvanceTurn();
            Assert.AreEqual(2, root.childCount, "걸린 턴의 통지에도 다음 턴 몫은 재생한다");
            player.AdvanceTurn();
            Assert.AreEqual(3, root.childCount, "남은 턴마다 다시 재생한다");

            Assert.IsFalse(player.AdvanceTurn(), "두 턴을 다 세면 걷힌다");
            Assert.AreEqual(3, root.childCount, "걷히는 턴에는 새로 띄우지 않는다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 유지_중_StopHold는_남은_턴과_무관하게_걷는다()
        {
            var player = Play(VfxHold.Continuous, holdTurns: 5);

            player.StopHold();

            Assert.IsFalse(player.HasHold, "사망·재바인딩에서는 남은 턴을 무시하고 끊는다");
            yield return null;
        }

        /// <summary>유지 레이어 하나짜리 스펙을 새 재생기에 걸고 그 재생기를 돌려준다.</summary>
        private VfxPlayer Play(VfxHold mode, int holdTurns)
        {
            var spec = ScriptableObject.CreateInstance<VfxSpec>();
            spec.layers.Add(new VfxLayer
            {
                prefab = _layerPrefab,
                holdTurns = holdTurns,
                holdMode = mode,
                awaitSeconds = 0f,
            });

            var player = Track(new GameObject("VfxPlayer")).AddComponent<VfxPlayer>();
            // 유지 레이어는 시작 지연이 없으면 등록·재생까지 동기로 끝나 대기 없이 관측할 수 있다.
            player.Play(spec, 1, _ => Vector3.zero, CancellationToken.None).Forget();
            return player;
        }

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }
    }
}
