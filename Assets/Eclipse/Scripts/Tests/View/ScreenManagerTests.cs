using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Eclipse.View.Infra;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Eclipse.Tests.View
{
    /// <summary>
    /// ScreenManager의 다중 Pop 되감기·재진입 가드·예외 시 파괴 보장을 PlayMode에서 관측한다.
    /// MonoBehaviour라 private 직렬화 필드는 리플렉션으로 배선하고, 화면 클론은 screenRoot 자식으로
    /// 붙는 성질을 이용해 파괴 여부를 확인한다.
    /// </summary>
    public class ScreenManagerTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private ScreenManager _mgr;
        private Transform _screenRoot;
        private Dictionary<ScreenId, FakeScreen> _sources;
        private IObjectResolver _resolver;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null)
                    UnityEngine.Object.Destroy(go);
            _spawned.Clear();
            _resolver?.Dispose();
        }

        // 3종 화면(Lobby/CharacterList/CharacterDetail)의 소스 프리팹을 만들고 매니저를 배선한다.
        private void CreateManager()
        {
            _resolver = new ContainerBuilder().Build();

            var mgrGo = Track(new GameObject("ScreenManager"));
            mgrGo.SetActive(false);
            _mgr = mgrGo.AddComponent<ScreenManager>();

            _screenRoot = Track(new GameObject("ScreenRoot")).transform;
            typeof(ScreenManager).GetField("screenRoot", Flags).SetValue(_mgr, _screenRoot);

            // Awake가 entries를 순회하므로 null이면 NRE — 빈 배열로 채우고, _prefabs는 직접 주입한다.
            var entriesField = typeof(ScreenManager).GetField("entries", Flags);
            entriesField.SetValue(_mgr, Array.CreateInstance(entriesField.FieldType.GetElementType(), 0));

            mgrGo.SetActive(true);

            _sources = new Dictionary<ScreenId, FakeScreen>();
            var prefabs = (Dictionary<ScreenId, GameObject>)typeof(ScreenManager).GetField("_prefabs", Flags).GetValue(_mgr);
            foreach (var id in (ScreenId[])Enum.GetValues(typeof(ScreenId)))
            {
                var src = Track(new GameObject(id + "Source"));
                src.SetActive(false);
                _sources[id] = src.AddComponent<FakeScreen>();
                prefabs[id] = src;
            }

            _mgr.Construct(_resolver);
        }

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        private int StackCount()
        {
            var stack = (ICollection)typeof(ScreenManager).GetField("_stack", Flags).GetValue(_mgr);
            return stack.Count;
        }

        [UnityTest]
        public IEnumerator Push_ExistingId_되감기로_중간화면_전부_파괴하고_루트복귀()
        {
            CreateManager();

            yield return _mgr.Push(ScreenId.Lobby).ToCoroutine();
            yield return _mgr.Push(ScreenId.CharacterList).ToCoroutine();
            yield return _mgr.Push(ScreenId.CharacterDetail).ToCoroutine();
            Assert.AreEqual(3, StackCount());

            var listClone = _screenRoot.GetChild(1).gameObject;
            var detailClone = _screenRoot.GetChild(2).gameObject;

            yield return _mgr.Push(ScreenId.Lobby).ToCoroutine();

            Assert.AreEqual(1, StackCount(), "루트만 남아야 한다");
            yield return null; // Destroy는 프레임 끝에 처리된다

            Assert.IsTrue(listClone == null, "중간 화면(List)이 파괴돼야 한다");
            Assert.IsTrue(detailClone == null, "중간 화면(Detail)이 파괴돼야 한다");
            Assert.AreEqual(1, _screenRoot.childCount, "화면 클론은 루트 하나만 살아있어야 한다");
        }

        [UnityTest]
        public IEnumerator Push_전환중_재호출은_무시된다()
        {
            CreateManager();

            yield return _mgr.Push(ScreenId.Lobby).ToCoroutine();

            _sources[ScreenId.CharacterList].EnterDelayFrames = 3; // OnEnter를 3프레임 붙잡는다

            var transitioning = _mgr.Push(ScreenId.CharacterList); // OnEnter에서 대기
            var dropped = _mgr.Push(ScreenId.CharacterDetail);     // 전환 중 → 무시돼야 한다

            yield return transitioning.ToCoroutine();
            yield return dropped.ToCoroutine();

            Assert.AreEqual(2, StackCount(), "재진입 호출이 무시돼 스택은 Lobby+List만 2단");
            Assert.AreEqual(0, _sources[ScreenId.CharacterDetail].EnterCount, "Detail은 진입조차 안 해야 한다");
            Assert.AreEqual(2, _screenRoot.childCount);
        }

        [UnityTest]
        public IEnumerator Pop_OnExit_예외라도_화면은_파괴된다()
        {
            CreateManager();

            yield return _mgr.Push(ScreenId.Lobby).ToCoroutine();
            _sources[ScreenId.CharacterList].ThrowOnExit = true;
            yield return _mgr.Push(ScreenId.CharacterList).ToCoroutine();

            var listClone = _screenRoot.GetChild(1).gameObject;

            yield return Swallow(_mgr.Pop()).ToCoroutine();

            Assert.AreEqual(1, StackCount(), "예외가 나도 스택에서는 빠져야 한다");
            yield return null; // Destroy 처리 대기

            Assert.IsTrue(listClone == null, "OnExit 예외에도 GameObject는 파괴돼 유령이 남지 않아야 한다");
            Assert.AreEqual(1, _screenRoot.childCount);
        }

        private static async UniTask Swallow(UniTask task)
        {
            try { await task; }
            catch { /* 예외 경로에서도 정리 불변식이 지켜지는지만 관측한다 */ }
        }
    }
}
