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
    /// PopupManager가 Show 도중 예외가 나도 dim(입력차단막)을 반드시 끄고 팝업을 파괴하는지 관측한다.
    /// dim이 켜진 채 남으면 배경 입력이 전면 차단되는 soft-lock이 된다.
    /// </summary>
    public class PopupManagerTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private PopupManager _mgr;
        private Transform _popupRoot;
        private GameObject _dim;
        private FakePopup _source;
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

        private void CreateManager()
        {
            _resolver = new ContainerBuilder().Build();

            var mgrGo = Track(new GameObject("PopupManager"));
            mgrGo.SetActive(false);
            _mgr = mgrGo.AddComponent<PopupManager>();

            _popupRoot = Track(new GameObject("PopupRoot")).transform;
            _dim = Track(new GameObject("Dim"));
            _dim.SetActive(false);

            typeof(PopupManager).GetField("popupRoot", Flags).SetValue(_mgr, _popupRoot);
            typeof(PopupManager).GetField("dim", Flags).SetValue(_mgr, _dim);

            var entriesField = typeof(PopupManager).GetField("entries", Flags);
            entriesField.SetValue(_mgr, Array.CreateInstance(entriesField.FieldType.GetElementType(), 0));

            mgrGo.SetActive(true);

            var src = Track(new GameObject("ConfirmSource"));
            src.SetActive(false);
            _source = src.AddComponent<FakePopup>();

            var prefabs = (Dictionary<PopupId, GameObject>)typeof(PopupManager).GetField("_prefabs", Flags).GetValue(_mgr);
            prefabs[PopupId.Confirm] = src;

            _mgr.Construct(_resolver);
        }

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        [UnityTest]
        public IEnumerator Show_예외라도_dim을_끄고_팝업을_파괴한다()
        {
            CreateManager();
            _source.ThrowOnOpen = true;

            yield return Swallow(_mgr.Show<bool>(PopupId.Confirm)).ToCoroutine();

            Assert.IsFalse(_dim.activeSelf, "예외 경로에서도 dim이 꺼져야 soft-lock이 안 생긴다");
            yield return null; // Destroy 처리 대기
            Assert.AreEqual(0, _popupRoot.childCount, "실패한 팝업 클론은 파괴돼야 한다");
        }

        [UnityTest]
        public IEnumerator Show_정상완료시_dim을_끄고_결과를_돌려준다()
        {
            CreateManager();

            var result = false;
            yield return Capture(_mgr.Show<bool>(PopupId.Confirm), r => result = r).ToCoroutine();

            Assert.IsTrue(result, "FakePopup은 true를 돌려준다");
            Assert.IsFalse(_dim.activeSelf);
            yield return null;
            Assert.AreEqual(0, _popupRoot.childCount);
        }

        private static async UniTask Swallow(UniTask<bool> task)
        {
            try { await task; }
            catch { /* 정리 불변식(dim off·파괴)만 관측한다 */ }
        }

        private static async UniTask Capture(UniTask<bool> task, Action<bool> sink)
        {
            sink(await task);
        }
    }
}
