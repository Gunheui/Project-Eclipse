using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View.Infra
{
    /// <summary>
    /// 확인/취소 팝업. 문구는 띄우는 쪽이 넘기므로 이 뷰는 어떤 조작을 묻는지 모른다.
    /// </summary>
    public class ConfirmPopupView : MonoBehaviour, IPopup<bool>
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;

        private readonly UniTaskCompletionSource<bool> _choice = new();

        /// <summary> 사용자 응답. 확인이면 true, 취소면 false다. </summary>
        public UniTask<bool> Result => _choice.Task;

        /// <summary> 제목과 본문을 채운다. 띄우기 전에 한 번 부른다. </summary>
        public void SetContent(string title, string body)
        {
            titleText.text = title;
            bodyText.text = body;
        }

        private void Awake()
        {
            confirmButton.OnClickAsObservable().Subscribe(_ => _choice.TrySetResult(true)).AddTo(this);
            cancelButton.OnClickAsObservable().Subscribe(_ => _choice.TrySetResult(false)).AddTo(this);
        }

        /// <summary>팝업을 띄운다. 등장 연출이 없어 즉시 완료된다.</summary>
        public UniTask Open() => UniTask.CompletedTask;

        /// <summary>팝업을 닫는다. 파괴는 PopupManager가 한다.</summary>
        public UniTask Close() => UniTask.CompletedTask;
    }
}
