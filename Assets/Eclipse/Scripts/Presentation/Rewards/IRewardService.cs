using System.Collections.Generic;
using Eclipse.Data;

namespace Eclipse.Presentation
{
    /// <summary> 재화 지급 창구 = 지갑 접촉 단일 지점. 지급 규칙이 로컬에서 서버 검증으로 바뀌어도 호출부는 그대로다. </summary>
    public interface IRewardService
    {
        /// <summary> 보상 목록을 합산해 지갑에 지급하고, 그 결과를 표시용 영수증으로 반환한다. </summary>
        /// <param name="entries">지급할 보상. 같은 재화는 합산되고 0 이하는 제외된다.</param>
        /// <returns>실제 지급된 보상(재화별 1건). 지급분이 없으면 빈 목록.</returns>
        IReadOnlyList<RewardEntry> Grant(IEnumerable<RewardEntry> entries);
    }
}
