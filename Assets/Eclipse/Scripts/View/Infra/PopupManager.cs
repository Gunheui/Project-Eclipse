using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Eclipse.View.Infra
{
    /// <summary>
    /// 모달 팝업의 생명주기를 소유한다. 팝업을 온디맨드로 생성·주입하고, 열림→결과 대기→닫힘을
    /// 처리한 뒤 결과를 호출자에게 돌려준다. 팝업이 하나라도 떠 있는 동안 배경 dim을 켠다.
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        [Serializable]
        private struct PopupEntry
        {
            public PopupId Id;
            public GameObject Prefab;
        }

        [SerializeField] private Transform popupRoot;
        [SerializeField] private GameObject dim;
        [SerializeField] private PopupEntry[] entries;

        private readonly Dictionary<PopupId, GameObject> _prefabs = new Dictionary<PopupId, GameObject>();
        private readonly List<IPopup> _stack = new List<IPopup>();

        private IObjectResolver _resolver;

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        private void Awake()
        {
            foreach (var entry in entries)
                _prefabs[entry.Id] = entry.Prefab;
        }

        /// <summary>
        /// 팝업을 띄우고 사용자 응답이 정해질 때까지 기다린 뒤 결과를 돌려준다.
        /// </summary>
        /// <typeparam name="TResult">팝업이 돌려주는 응답 타입.</typeparam>
        public UniTask<TResult> Show<TResult>(PopupId id) => Show<TResult>(id, null);

        /// <summary> 문구를 채운 확인/취소 팝업을 띄우고 응답을 기다린다. </summary>
        /// <returns>확인이면 true, 취소면 false.</returns>
        public UniTask<bool> ShowConfirm(string title, string body)
            => Show<bool>(PopupId.Confirm, go => go.GetComponent<ConfirmPopupView>().SetContent(title, body));

        /// <summary>
        /// 확인 버튼만 있는 안내 팝업을 띄우고 닫힐 때까지 기다린다. 물어보는 게 아니라 알리는 것이라
        /// 결과가 없다. 확인/취소 팝업의 취소를 감춰 쓰므로 전용 프리팹이 없다.
        /// </summary>
        public async UniTask ShowAlert(string title, string body)
            => await Show<bool>(PopupId.Confirm,
                go => go.GetComponent<ConfirmPopupView>().SetContent(title, body, showCancel: false));

        /// <param name="configure">띄우기 전에 팝업 인스턴스를 손볼 훅. 호출자에게 내부를 열지 않으려고 비공개로 둔다.</param>
        private async UniTask<TResult> Show<TResult>(PopupId id, Action<GameObject> configure)
        {
            if (!_prefabs.TryGetValue(id, out var prefab))
                throw new InvalidOperationException($"PopupManager: '{id}'에 등록된 프리팹이 없습니다. entries 매핑을 확인하세요.");

            var go = _resolver.Instantiate(prefab, popupRoot);
            var popup = go.GetComponent<IPopup<TResult>>();
            if (popup == null)
            {
                Destroy(go);
                throw new InvalidOperationException($"PopupManager: 프리팹 '{prefab.name}'에 IPopup<{typeof(TResult).Name}> 구현이 없습니다.");
            }
            configure?.Invoke(go);

            _stack.Add(popup);
            if (_stack.Count == 1)
                dim.SetActive(true);

            try
            {
                await popup.Open();
                var result = await popup.Result;
                await popup.Close();
                return result;
            }
            finally
            {
                _stack.Remove(popup);
                Destroy(go);
                if (_stack.Count == 0)
                    dim.SetActive(false);
            }
        }
    }
}
