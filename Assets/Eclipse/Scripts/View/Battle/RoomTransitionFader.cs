using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 방 전환용 전체 화면 페이드 오버레이 1장. 페이드 중에는 레이캐스트를 막아
    /// 전환 사이 중복 입력을 함께 차단한다. 씬 시작 상태는 불투명(첫 방 페이드 인으로 열린다).
    /// </summary>
    public class RoomTransitionFader : MonoBehaviour
    {
        [SerializeField] private Image overlay;
        [SerializeField] private float duration = 0.25f;

        /// <summary> 화면을 검게 덮는다. 완료 시점부터 배경 스왑·재조립이 안전하다. </summary>
        public async UniTask FadeOutAsync()
        {
            overlay.gameObject.SetActive(true);
            overlay.raycastTarget = true;
            await overlay.DOFade(1f, duration).ToUniTask(TweenCancelBehaviour.Complete,
                this.GetCancellationTokenOnDestroy());
        }

        /// <summary> 덮개를 걷는다. 완료 후 레이캐스트 차단을 풀어 입력을 되살린다. </summary>
        public async UniTask FadeInAsync()
        {
            await overlay.DOFade(0f, duration).ToUniTask(TweenCancelBehaviour.Complete,
                this.GetCancellationTokenOnDestroy());
            overlay.raycastTarget = false;
            overlay.gameObject.SetActive(false);
        }
    }
}