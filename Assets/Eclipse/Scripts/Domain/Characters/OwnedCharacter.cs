using System;
using System.Collections.Generic;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 플레이어가 보유한 캐릭터 한 명. 캐릭터 정의(CharacterSO)에 계정별 진행값(레벨·스킬 레벨·돌파)을 덧붙인다.
    /// 초상·등급·기본 스탯은 Definition에서 읽고, 성장값만 이 객체가 보유한다.
    /// </summary>
    public class OwnedCharacter
    {
        /// <summary> 액티브 스킬 슬롯 수. 세이브의 스킬 레벨 배열 길이와 같다. </summary>
        public const int SkillSlotCount = 3;

        /// <summary> 스킬 레벨 상한. 스킬 레벨은 1부터 시작한다. </summary>
        public const int MaxSkillLevel = 5;

        /// <summary> 돌파 단계 상한(0 = 미돌파). </summary>
        public const int MaxAscensionTier = 3;

        private readonly int[] _skillLevels;

        /// <summary> 캐릭터 정의(공유·불변). </summary>
        public CharacterSO Definition { get; }

        /// <summary> 이 계정에서의 현재 레벨. 성장 도메인 밖에서는 못 쓴다(생성·복원은 ctor, 증가는 <see cref="IncreaseLevel"/>). </summary>
        public int Level { get; private set; }

        /// <summary> 돌파 단계(0 = 미돌파). 상한·강화 로직은 성장 시스템 소관. </summary>
        public int AscensionTier { get; set; }

        /// <summary> 액티브 슬롯별 스킬 레벨. 길이는 항상 <see cref="SkillSlotCount"/>, 값은 [1, <see cref="MaxSkillLevel"/>]. </summary>
        public IReadOnlyList<int> SkillLevels => _skillLevels;

        /// <param name="skillLevels">슬롯별 스킬 레벨. null·길이 부족은 1로 채우고, 범위 밖 값은 [1, 상한]으로 고정한다(세이브 복원 방어).</param>
        public OwnedCharacter(CharacterSO definition, int level, int ascensionTier = 0, int[] skillLevels = null)
        {
            Definition = definition;
            Level = level;
            AscensionTier = Math.Clamp(ascensionTier, 0, MaxAscensionTier);
            _skillLevels = new int[SkillSlotCount];
            for (int i = 0; i < SkillSlotCount; i++)
            {
                int stored = skillLevels != null && i < skillLevels.Length ? skillLevels[i] : 1;
                _skillLevels[i] = Math.Clamp(stored, 1, MaxSkillLevel);
            }
        }

        /// <summary> 레벨을 1 올린다. 상한(<see cref="Data.GrowthCurve.maxLevel"/>) 강제는 성장 서비스 책임이다. </summary>
        public void IncreaseLevel() => Level++;

        /// <summary> 해당 슬롯의 스킬 레벨을 1 올린다. 상한(<see cref="MaxSkillLevel"/>) 강제는 성장 서비스 책임이다. </summary>
        public void IncreaseSkillLevel(int skillSlot) => _skillLevels[skillSlot]++;
    }
}
