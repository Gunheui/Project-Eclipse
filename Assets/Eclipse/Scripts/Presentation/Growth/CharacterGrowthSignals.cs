using System;
using Eclipse.Domain;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 캐릭터 성장이 확정됐음을 알리는 신호. 성장 서비스가 발행하고, 그 캐릭터를 표시 중인 ViewModel들이 받아 자기 값을 갱신한다.
    /// </summary>
    public sealed class CharacterGrowthSignals : IDisposable
    {
        private readonly Subject<OwnedCharacter> _changed = new();

        /// <summary> 값이 바뀐 캐릭터. 구독자는 자기가 들고 있는 인스턴스와 참조가 같은지 보고 거른다. </summary>
        public Observable<OwnedCharacter> Changed => _changed;

        /// <summary> 성장이 실제로 반영됐을 때만 호출한다. 실패한 시도는 발행하지 않는다. </summary>
        public void Notify(OwnedCharacter owned) => _changed.OnNext(owned);

        public void Dispose() => _changed.Dispose();
    }
}
