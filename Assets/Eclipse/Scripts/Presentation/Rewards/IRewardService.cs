using System.Collections.Generic;
using Eclipse.Data;

namespace Eclipse.Presentation
{
    /// <summary> 전투 승리 보상의 지급 창구. 지급 규칙이 로컬 테이블에서 서버 검증으로 바뀌어도 호출부는 그대로다. </summary>
    public interface IRewardService
    {
        /// <summary>
        /// 스테이지 승리 보상을 계산해 지갑에 지급하고, 그 합산 결과를 결과 화면용 영수증으로 반환한다.
        /// </summary>
        /// <param name="stage">클리어한 스테이지. null이거나 보상 배열이 비면 지급하지 않는다.</param>
        /// <param name="firstClear">true면 최초 클리어 보상까지 더한다.</param>
        /// <returns>실제 지급된 보상(재화별 1건 합산, 0 이하 제외). 지급분이 없으면 빈 목록.</returns>
        IReadOnlyList<RewardEntry> GrantVictory(StageSO stage, bool firstClear);
    }
}
