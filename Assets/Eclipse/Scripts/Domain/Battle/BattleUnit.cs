using System;
using System.Collections.Generic;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 전투에 참여하는 유닛의 런타임 상태. CharacterSO(아군) 또는 EnemySO(적) 정의에서 생성되며,
    /// 현재 HP·스킬 잔여 쿨 등 전투 중 변하는 상태를 보유한다.
    /// 정의는 여러 유닛이 공유하고, 이 런타임 상태는 유닛별로 독립이다.
    /// </summary>
    public class BattleUnit : ICombatant, IDamageable
    {
        private readonly Stats _baseStats;
        private readonly List<SkillRuntime> _skills;
        private readonly int _maxHp;

        public string DisplayName { get; }
        public Team Team { get; }
        public int SlotIndex { get; }

        /// <summary> 버프·디버프 반영 유효 스탯. 지금은 기본 스탯 스냅샷과 같다(수정자 적용은 이후 범위). </summary>
        public Stats EffectiveStats => _baseStats;

        public int CurrentHp { get; private set; }
        public bool IsAlive => CurrentHp > 0;
        public IReadOnlyList<SkillRuntime> Skills => _skills;

        /// <summary> 피해를 적용해 HP를 줄인다. HP는 0 밑으로 내려가지 않는다. </summary>
        /// <param name="amount">깎을 HP(0 이상 전제).</param>
        public void ApplyDamage(int amount)
        {
            CurrentHp = Math.Max(0, CurrentHp - amount);
        }

        /// <summary> 회복을 적용해 HP를 늘린다. HP는 최대 HP를 넘지 않는다. </summary>
        /// <param name="amount">채울 HP(0 이상 전제).</param>
        public void Heal(int amount)
        {
            CurrentHp = Math.Min(_maxHp, CurrentHp + amount);
        }

        private BattleUnit(string displayName, Team team, int slotIndex, Stats baseStats, List<SkillRuntime> skills)
        {
            DisplayName = displayName;
            Team = team;
            SlotIndex = slotIndex;
            _baseStats = baseStats;
            _skills = skills;
            _maxHp = baseStats.hp;
            CurrentHp = _maxHp;
        }

        /// <summary>
        /// 아군 캐릭터를 전투 유닛으로 만든다. 스탯은 레벨 스케일되고, 액티브 스킬 슬롯을 런타임으로 감싼다.
        /// </summary>
        /// <param name="owned">보유 캐릭터(정의·레벨).</param>
        /// <param name="slotIndex">편성 슬롯 번호(0부터).</param>
        public static BattleUnit FromCharacter(OwnedCharacter owned, int slotIndex)
        {
            var def = owned.Definition;
            var stats = CharacterStats.ScaleToLevel(def, owned.Level);
            var skills = BuildSkills(def.basicSkill, def.normalSkill, def.ultimateSkill);
            return new BattleUnit(def.displayName, Team.Ally, slotIndex, stats, skills);
        }

        /// <summary>
        /// 적을 전투 유닛으로 만든다. 스탯은 스테이지 고정치를 그대로 쓴다(레벨 스케일 없음).
        /// </summary>
        /// <param name="enemy">적 정의.</param>
        /// <param name="slotIndex">편성 슬롯 번호(0부터).</param>
        public static BattleUnit FromEnemy(EnemySO enemy, int slotIndex)
        {
            var skills = BuildSkills(enemy.basicSkill, enemy.normalSkill, enemy.ultimateSkill);
            return new BattleUnit(enemy.displayName, Team.Enemy, slotIndex, enemy.baseStats, skills);
        }

        // null이 아닌 슬롯만 런타임으로 감싼다(적은 슬롯이 비어 있을 수 있다).
        private static List<SkillRuntime> BuildSkills(params SkillSO[] slots)
        {
            var list = new List<SkillRuntime>();
            foreach (var s in slots)
                if (s != null) list.Add(new SkillRuntime(s));
            return list;
        }
    }
}