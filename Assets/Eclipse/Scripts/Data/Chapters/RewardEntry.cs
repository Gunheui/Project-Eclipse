using System;
using Eclipse.Data.Enums;

namespace Eclipse.Data
{
    /// <summary>
    /// 보상 한 건 = 재화 종류와 수량. 지갑의 증감 API(Grant)에 그대로 먹일 수 있는 최소 단위이며,
    /// 보상 목록의 원소이자 결과 팝업에 표시되는 항목이기도 하다.
    /// </summary>
    [Serializable]
    public struct RewardEntry
    {
        /// <summary> 지급할 재화 종류. </summary>
        public CurrencyType type;

        /// <summary> 지급 수량. 0 이하는 지급·표시 모두에서 제외된다. </summary>
        public int amount;
    }
}
