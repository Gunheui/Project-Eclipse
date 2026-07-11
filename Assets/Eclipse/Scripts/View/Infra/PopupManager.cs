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
        public async UniTask<TResult> Show<TResult>(PopupId id)
        {
            if (!_prefabs.TryGetValue(id, out var prefab))
                throw new InvalidOperationException($"PopupManager: '{id}'에 등록된 프리팹이 없습니다. entries 배선을 확인하세요.");

            var go = _resolver.Instantiate(prefab, popupRoot);
            var popup = go.GetComponent<IPopup<TResult>>();
            if (popup == null)
            {
                Destroy(go);
                throw new InvalidOperationException($"PopupManager: 프리팹 '{prefab.name}'에 IPopup<{typeof(TResult).Name}> 구현이 없습니다.");
            }

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
