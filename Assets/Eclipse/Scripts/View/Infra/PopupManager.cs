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
    /// 처리한 뒤 결과를 호출자에게 돌려준다. 팝업이 떠 있는 동안 dim을 최상단 팝업 바로 아래에 두어
    /// 아래 팝업과 배경 UI가 입력을 받지 않게 한다.
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
        private readonly List<Transform> _stack = new List<Transform>();

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

            // dim이 팝업과 같은 부모에 있어야 형제 순서로 팝업 사이에 끼워 넣을 수 있다.
            dim.transform.SetParent(popupRoot, false);
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

            _stack.Add(go.transform);
            dim.SetActive(true);
            PlaceDimUnderTop();

            try
            {
                await popup.Open();
                var result = await popup.Result;
                await popup.Close();
                return result;
            }
            finally
            {
                _stack.Remove(go.transform);
                Destroy(go);
                if (_stack.Count == 0)
                    dim.SetActive(false);
                else
                    PlaceDimUnderTop();
            }
        }

        /// <summary> dim을 최상단 팝업 바로 아래 형제로 옮겨 그 아래 전부의 입력을 막는다. </summary>
        private void PlaceDimUnderTop()
        {
            // SetSiblingIndex는 제거 후 삽입이라 올릴 때와 내릴 때 인덱스 보정이 달라진다.
            // 맨 뒤로 두 번 보내면 방향과 무관하게 dim → 최상단 팝업 순서가 된다.
            dim.transform.SetAsLastSibling();
            _stack[^1].SetAsLastSibling();
        }
    }
}
