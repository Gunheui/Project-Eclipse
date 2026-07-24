using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 플레이어가 보유한 캐릭터 한 명. 캐릭터 정의(CharacterSO)에 계정별 진행값(레벨)을 얹는다.
    /// 초상·등급·기본 스탯은 Definition에서 읽고, 레벨만 이 객체가 보유한다.
    /// </summary>
    public class OwnedCharacter
    {
        /// <summary> 캐릭터 정의(공유·불변). </summary>
        public CharacterSO Definition { get; }

        /// <summary> 이 계정에서의 현재 레벨. 성장 도메인 밖에서는 못 쓴다(생성·복원은 ctor, 증가는 <see cref="IncreaseLevel"/>). </summary>
        public int Level { get; private set; }

        /// <summary> 돌파 단계(0 = 미돌파). 상한·강화 로직은 성장 시스템 소관. </summary>
        public int AscensionTier { get; set; }

        public OwnedCharacter(CharacterSO definition, int level, int ascensionTier = 0)
        {
            Definition = definition;
            Level = level;
            AscensionTier = ascensionTier;
        }

        /// <summary> 레벨을 1 올린다. 상한(<see cref="Data.GrowthCurve.maxLevel"/>) 강제는 성장 서비스 책임이다. </summary>
        public void IncreaseLevel() => Level++;
    }
}
