using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Eclipse.View.Infra
{
    /// <summary>
    /// 전면 화면을 스택으로 관리한다. push 시점에 프리팹을 온디맨드로 생성·주입하고,
    /// 주입이 끝난 뒤에만 화면의 OnEnter를 호출한다. pop 시 화면은 파괴한다.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        [Serializable]
        private struct ScreenEntry
        {
            public ScreenId Id;
            public GameObject Prefab;
        }

        private readonly struct StackEntry
        {
            public readonly ScreenId Id;
            public readonly GameObject Go;
            public readonly IScreen Screen;

            public StackEntry(ScreenId id, GameObject go, IScreen screen)
            {
                Id = id;
                Go = go;
                Screen = screen;
            }
        }

        [SerializeField] private Transform screenRoot;
        [SerializeField] private ScreenEntry[] entries;

        private readonly Dictionary<ScreenId, GameObject> _prefabs = new Dictionary<ScreenId, GameObject>();
        private readonly List<StackEntry> _stack = new List<StackEntry>();

        private IObjectResolver _resolver;
        
        // 화면 전환 진행 중 여부. 연타로 화면이 겹치는 것을 막는다.
        private bool _isTransitioning;

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
        /// 화면을 전면에 올린다. 이미 스택에 있으면 그 위를 전부 되감아 기존 인스턴스로 복귀한다.
        /// 전환이 진행 중이면 호출을 무시한다(연타로 화면이 겹치는 것을 막는다).
        /// </summary>
        public async UniTask Push(ScreenId id)
        {
            if (_isTransitioning)
                return;

            _isTransitioning = true;
            try
            {
                if (Contains(id))
                {
                    while (_stack.Count > 0 && _stack[^1].Id != id)
                        await TearDownTop();
                    return;
                }

                if (!_prefabs.TryGetValue(id, out var prefab))
                    throw new InvalidOperationException($"ScreenManager: '{id}'에 등록된 프리팹이 없습니다. entries 매핑을 확인하세요.");

                var go = _resolver.Instantiate(prefab, screenRoot);
                var screen = go.GetComponent<IScreen>();
                if (screen == null)
                {
                    Destroy(go);
                    throw new InvalidOperationException($"ScreenManager: 프리팹 '{prefab.name}'에 IScreen 구현이 없습니다.");
                }

                _stack.Add(new StackEntry(id, go, screen));
                await screen.OnEnter();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 최상단 화면을 되돌린다. 루트 화면은 되돌리지 않는다.
        /// 전환이 진행 중이면 호출을 무시한다.
        /// </summary>
        public async UniTask Pop()
        {
            if (_isTransitioning || _stack.Count <= 1)
                return;

            _isTransitioning = true;
            try
            {
                await TearDownTop();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>최상단 화면을 스택에서 빼고 파괴한다. OnExit가 예외를 던져도 파괴는 보장된다.</summary>
        private async UniTask TearDownTop()
        {
            var top = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);

            try
            {
                await top.Screen.OnExit();
            }
            finally
            {
                Destroy(top.Go);
            }
        }

        private bool Contains(ScreenId id)
        {
            return _stack.Any(entry => entry.Id == id);
        }
    }
}
