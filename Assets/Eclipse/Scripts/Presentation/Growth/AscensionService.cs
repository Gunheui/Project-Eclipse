using Eclipse.Domain;

namespace Eclipse.Presentation
{
    /// <summary> 돌파 시도 결과. NoDuplicate는 [이음새] 가챠(중복 재료) 구현 시 사용한다. </summary>
    public enum AscensionResult
    {
        Success,
        NoDuplicate,
        MaxTier,
    }

    /// <summary>
    /// 돌파의 유일한 권위. 단계 증가·세이브를 하나의 트랜잭션으로 묶는다.
    /// [이음새] 돌파 재료(가챠 중복)가 아직 없어 재료 검사 없이 올린다.
    /// 현재 진입 경로는 에디터 디버그(AppLifetimeScope)뿐이다(돌파 재료 공급은 가챠 확장에서 열린다).
    /// </summary>
    public sealed class AscensionService
    {
        private readonly SaveService _save;

        public AscensionService(SaveService save)
        {
            _save = save;
        }

        /// <summary> 대상 캐릭터의 돌파 단계를 1 올린다. 상한(<see cref="OwnedCharacter.MaxAscensionTier"/>)이면 무변경 거부. </summary>
        public AscensionResult TryAscend(OwnedCharacter owned)
        {
            if (owned.AscensionTier >= OwnedCharacter.MaxAscensionTier)
                return AscensionResult.MaxTier;

            owned.AscensionTier++;
            _save.Save();
            return AscensionResult.Success;
        }
    }
}
