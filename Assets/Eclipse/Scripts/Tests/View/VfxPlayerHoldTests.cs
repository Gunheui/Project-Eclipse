using System;
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
    /// 유지 레이어를 붙든 재생기의 동작을 관측한다. 수명은 배틀러가 정하므로 여기서 보는 것은
    /// 등록·걷기와 「턴마다」 재생뿐이다.
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
                    UnityEngine.Object.Destroy(go);
            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator 유지_레이어는_재생_직후_붙들려_있다()
        {
            var player = Play(VfxHold.Continuous);

            Assert.IsTrue(player.HasHold, "유지 레이어는 대기가 끝나도 재생기를 붙든다");
            Assert.AreEqual(1, player.transform.childCount, "시전 즉시 한 번 재생된다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator StopHold는_붙든_레이어를_모두_걷는다()
        {
            var player = Play(VfxHold.Continuous);

            player.StopHold();

            Assert.IsFalse(player.HasHold, "출처 효과가 풀리거나 배틀러가 죽으면 남은 것 없이 끊는다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 턴마다는_통지받을_때마다_다시_재생된다()
        {
            var player = Play(VfxHold.EachTurn);
            var root = player.transform;

            Assert.AreEqual(1, root.childCount, "시전 턴 몫이 한 번 재생된다");
            player.FlashEachTurn();
            Assert.AreEqual(2, root.childCount, "턴 통지마다 한 번씩 다시 띄운다");
            player.FlashEachTurn();
            Assert.AreEqual(3, root.childCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 켜두기는_통지를_받아도_다시_띄우지_않는다()
        {
            var player = Play(VfxHold.Continuous);

            player.FlashEachTurn();

            Assert.AreEqual(1, player.transform.childCount, "켜 둔 인스턴스 하나가 그대로 유지된다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator follow를_끈_레이어만_스폰한_자리에_남는다()
        {
            var spec = ScriptableObject.CreateInstance<VfxSpec>();
            spec.layers.Add(Layer(holdTurns: 0));
            spec.layers.Add(Layer(holdTurns: 0, follow: false));
            var anchor = Vector3.zero;
            var player = Play(spec, _ => anchor);
            var following = player.transform.GetChild(0);
            var pinned = player.transform.GetChild(1);

            anchor = new Vector3(3f, 0f, 0f);
            yield return null;

            Assert.AreEqual(3f, following.position.x, 0.001f, "켠 인스턴스는 앵커를 매 프레임 다시 읽는다");
            Assert.AreEqual(0f, pinned.position.x, 0.001f, "끈 인스턴스는 스폰한 자리에 남는다");
        }

        [UnityTest]
        public IEnumerator 배틀러가_사라지면_추적을_멈춘다()
        {
            var spec = ScriptableObject.CreateInstance<VfxSpec>();
            spec.layers.Add(Layer(holdTurns: 0));
            var anchor = Vector3.zero;
            using var cts = new CancellationTokenSource();
            var player = Track(new GameObject("VfxPlayer")).AddComponent<VfxPlayer>();
            player.Play(spec, 1, _ => anchor, cts.Token).Forget();
            var once = player.transform.GetChild(0);

            cts.Cancel();
            anchor = new Vector3(3f, 0f, 0f);
            yield return null;

            Assert.AreEqual(0f, once.position.x, 0.001f, "취소 뒤에는 앵커를 다시 읽지 않는다");
        }

        /// <summary>유지 레이어 하나짜리 스펙을 새 재생기에 걸고 그 재생기를 돌려준다.</summary>
        private VfxPlayer Play(VfxHold mode)
        {
            var spec = ScriptableObject.CreateInstance<VfxSpec>();
            spec.layers.Add(Layer(holdTurns: 1, mode));
            return Play(spec, _ => Vector3.zero);
        }

        private VfxPlayer Play(VfxSpec spec, Func<VfxAnchor, Vector3> anchorAt)
        {
            var player = Track(new GameObject("VfxPlayer")).AddComponent<VfxPlayer>();
            // 유지 레이어는 시작 지연이 없으면 등록·재생까지 동기로 끝나 대기 없이 관측할 수 있다.
            player.Play(spec, 1, anchorAt, CancellationToken.None).Forget();
            return player;
        }

        private VfxLayer Layer(int holdTurns, VfxHold mode = VfxHold.Continuous, bool follow = true) => new VfxLayer
        {
            prefab = _layerPrefab,
            holdTurns = holdTurns,
            holdMode = mode,
            awaitSeconds = 0f,
            follow = follow,
        };

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }
    }
}
