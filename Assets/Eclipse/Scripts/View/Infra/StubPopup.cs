using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Eclipse.View.Infra
{
    /// <summary>
    /// 확인/취소 결과(bool)를 돌려주는 임시 팝업. 버튼 이벤트가 Confirm/Cancel을 호출하면 Result가 완료된다.
    /// </summary>
    public class StubPopup : MonoBehaviour, IPopup<bool>
    {
        private readonly UniTaskCompletionSource<bool> _tcs = new UniTaskCompletionSource<bool>();

        public UniTask<bool> Result => _tcs.Task;

        public UniTask Open()
        {
            Debug.Log($"{name} Open");
            return UniTask.CompletedTask;
        }

        public UniTask Close()
        {
            Debug.Log($"{name} Close");
            return UniTask.CompletedTask;
        }

        public void Confirm() => _tcs.TrySetResult(true);

        public void Cancel() => _tcs.TrySetResult(false);
    }
}
