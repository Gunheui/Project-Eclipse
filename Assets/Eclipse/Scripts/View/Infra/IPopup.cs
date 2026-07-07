using Cysharp.Threading.Tasks;

namespace Eclipse.View.Infra
{
    /// <summary>
    /// PopupManager가 결과 타입과 무관하게 다루는 모달 팝업 공통 계약.
    /// </summary>
    public interface IPopup
    {
        /// <summary>팝업을 띄우고 등장 연출이 끝나면 완료된다.</summary>
        UniTask Open();

        /// <summary>팝업을 닫고 퇴장 연출이 끝나면 완료된다.</summary>
        UniTask Close();
    }

    /// <summary>
    /// 결과를 돌려주는 팝업. 호출자는 이 타입으로 사용자의 응답을 수령한다.
    /// </summary>
    /// <typeparam name="TResult">사용자 응답 타입(예: 확인/취소 = bool).</typeparam>
    public interface IPopup<TResult> : IPopup
    {
        /// <summary>사용자 응답이 정해지면 완료되는 결과. 한 번만 await 한다.</summary>
        UniTask<TResult> Result { get; }
    }
}