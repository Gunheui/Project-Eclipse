using Cysharp.Threading.Tasks;

namespace Eclipse.View.Infra
{
    /// <summary>
    /// ScreenManager가 스택으로 관리하는 전면 화면 계약.
    /// 화면은 자신의 등장/퇴장 연출을 직접 수행하고 완료 시점을 UniTask로 알린다.
    /// </summary>
    public interface IScreen
    {
        /// <summary>
        /// 스택에 올라 전면에 설 때 매니저가 호출. 등장 연출을 수행하고 표시 데이터 구독을 시작한다.
        /// 의존성 주입이 끝난 뒤에만 호출된다.
        /// </summary>
        UniTask OnEnter();

        /// <summary>스택에서 제거될 때 매니저가 호출. 퇴장 연출을 수행하고 구독을 해지한다.</summary>
        UniTask OnExit();
    }
}