using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 적 유닛의 정의 데이터. 스테이지에 배치되는 고정 스탯과 스킬 슬롯을 담는다.
    /// 성장곡선·등급·획득 경로가 없다는 점에서 <see cref="CharacterSO"/>와 구분되며,
    /// 전투 런타임(Combatant)은 아군 정의와 공유한다. AI 프로파일은 스테이지가 지정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Characters/Enemy Data")]
    public class EnemySO : ScriptableObject
    {
        /// <summary> 식별자. </summary>
        public string id;

        /// <summary> 표시명. </summary>
        public string displayName;

        /// <summary> 스테이지 고정 스탯. 레벨 스케일이 없다(적은 성장하지 않는다). </summary>
        public Stats baseStats;

        /// <summary> 기본 공격. 쿨 0 폴백 슬롯. </summary>
        public SkillSO basicSkill;

        /// <summary> 일반 스킬. 없으면 null. </summary>
        public SkillSO normalSkill;

        /// <summary> 궁극기. 없으면 null. </summary>
        public SkillSO ultimateSkill;

        /// <summary> 전장에 표시할 배틀러 스프라이트. </summary>
        public Sprite battlerAssetRef;
    }
}
