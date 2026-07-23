using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 전투에 참여하는 유닛의 런타임 상태. CharacterSO(아군) 또는 EnemySO(적) 정의에서 생성되며,
    /// 현재 HP·스킬 잔여 쿨 등 전투 중 변하는 상태를 보유한다.
    /// 정의는 여러 유닛이 공유하고, 이 런타임 상태는 유닛별로 독립이다.
    /// </summary>
    public class Combatant : ICombatant, IDamageable
    {
        private readonly Stats _baseStats;
        private readonly List<SkillRuntime> _skills;
        private readonly List<StatusEffect> _effects = new();
        // 자기 턴 시작 시점에 붙어 있던 효과. 턴 끝 지속턴 감소는 이 목록만 대상으로 하므로
        // 턴 도중 적용된 효과는 그 턴에 깎이지 않는다(시전턴 면제).
        private readonly List<StatusEffect> _turnSnapshot = new();
        private readonly int _maxHp;

        public string DisplayName { get; }
        public Team Team { get; }
        public int SlotIndex { get; }

        public int MaxHp => _maxHp;
        public int CurrentHp { get; private set; }
        public bool IsAlive => CurrentHp > 0;
        public IReadOnlyList<SkillRuntime> Skills => _skills;

        /// <summary>
        /// 버프·디버프가 반영된 현재 유효 스탯. 정렬·데미지·힐은 이 값을 읽는다(기본 스탯이 아니라 이 값).
        /// 붙어 있는 효과가 바뀌면 다음 접근 때 자동으로 최신 값이 나온다.
        /// </summary>
        public Stats EffectiveStats => ComputeEffectiveStats();

        /// <summary> 붙어 있는 효과 중 도발이 있는지. </summary>
        public bool IsTaunting => _effects.Any(e => e.Type == EffectType.Taunt);

        /// <summary> 붙어 있는 실드들의 남은 흡수량 합. ApplyDamage가 HP보다 먼저 깎는 양과 같다. </summary>
        public int ShieldAbsorb => _effects.Where(e => e.Type == EffectType.Shield).Sum(e => e.RemainingAbsorb);

        /// <summary> 붙어 있는 지속 효과 목록(적용 순서). 표시용 읽기 전용 노출이다. </summary>
        public IReadOnlyList<StatusEffect> Effects => _effects;

        /// <summary>
        /// 피해를 적용한다. 실드가 있으면 리스트 순으로 먼저 흡수하고, 막지 못한 나머지만 HP를 깎는다.
        /// HP는 0 밑으로 내려가지 않는다. 흡수량이 0이 된 실드는 이 유닛의 자기 턴 정산에서 제거된다.
        /// </summary>
        /// <param name="amount">깎을 HP(0 이상 전제).</param>
        public void ApplyDamage(int amount)
        {
            int remaining = amount;
            foreach (var e in _effects)
            {
                if (remaining <= 0) break;
                if (e.Type == EffectType.Shield)
                    remaining = e.AbsorbDamage(remaining);
            }
            CurrentHp = Math.Max(0, CurrentHp - remaining);
        }

        /// <summary> 회복을 적용해 HP를 늘린다. HP는 최대 HP를 넘지 않는다. </summary>
        /// <param name="amount">채울 HP(0 이상 전제).</param>
        public void Heal(int amount)
        {
            CurrentHp = Math.Min(_maxHp, CurrentHp + amount);
        }

        /// <summary>
        /// 지속 효과를 부착한다. 버프·디버프는 같은 스탯의 기존 효과를 대체(refresh)하고,
        /// 도트·리젠·실드는 독립 인스턴스로 누적한다.
        /// </summary>
        /// <param name="effect">부착할 효과(세기는 이미 적용 시점 값으로 확정돼 있음).</param>
        public void ApplyEffect(StatusEffect effect)
        {
            if (effect.Type == EffectType.Buff || effect.Type == EffectType.Debuff)
                _effects.RemoveAll(e => e.Type == effect.Type && e.Stat == effect.Stat);
            else if (effect.Type == EffectType.Taunt)
                _effects.RemoveAll(e => e.Type == EffectType.Taunt); // 도발도 하나만 유지, 재적용 시 갱신
            _effects.Add(effect);
        }

        /// <summary>
        /// 자기 턴 시작 시 호출한다. 도트·리젠을 HP에 적용하고 만료 효과를 정리한 뒤,
        /// 이번 턴 끝에 지속턴을 깎을 효과 목록을 기록한다. 도트는 ApplyDamage를 거치므로 자기 실드에 먼저 흡수된다.
        /// 도트로 죽은 턴에는 행동하지 못하므로 스킬 쿨도 감소하지 않는다.
        /// </summary>
        public void OnTurnStart()
        {
            foreach (var e in _effects)
            {
                switch (e.Type)
                {
                    case EffectType.Dot: ApplyDamage(e.TickAmount); break;
                    case EffectType.Regen: Heal(e.TickAmount); break;
                }
            }
            _effects.RemoveAll(e => e.IsExpired);

            _turnSnapshot.Clear();
            _turnSnapshot.AddRange(_effects);

            if (!IsAlive) return;
            foreach (var skill in _skills)
                skill.ReduceCooldown();
        }

        /// <summary>
        /// 자기 턴 종료 시 호출한다. 턴 시작에 기록해 둔 효과만 지속턴을 1 줄이고, 만료된 효과를 제거한다.
        /// 턴 도중 새로 붙은 효과(자기 시전 버프, 갱신된 도발)는 기록에 없어 이번 턴에는 깎이지 않는다.
        /// </summary>
        public void OnTurnEnd()
        {
            foreach (var e in _turnSnapshot)
                e.TickDuration();
            _turnSnapshot.Clear();

            _effects.RemoveAll(e => e.IsExpired);
        }

        private Combatant(string displayName, Team team, int slotIndex, Stats baseStats, List<SkillRuntime> skills)
        {
            DisplayName = displayName;
            Team = team;
            SlotIndex = slotIndex;
            _baseStats = baseStats;
            _skills = skills;
            _maxHp = baseStats.hp;
            CurrentHp = _maxHp;
        }

        /// <summary> 아군 캐릭터를 전투 유닛으로 만든다. 스탯은 레벨 스케일되고, 스킬 슬롯을 런타임으로 감싼다. </summary>
        /// <param name="slotIndex">편성 슬롯 번호(0부터).</param>
        public static Combatant FromCharacter(OwnedCharacter owned, int slotIndex)
        {
            var def = owned.Definition;
            var stats = CharacterStats.ScaleToLevel(def, owned.Level);
            var skills = BuildSkills(false, def.basicSkill, def.normalSkill, def.ultimateSkill);
            return new Combatant(def.displayName, Team.Ally, slotIndex, stats, skills);
        }

        /// <summary> 적을 전투 유닛으로 만든다. 스탯은 스테이지 고정치를 그대로 쓴다(레벨 스케일 없음). </summary>
        /// <param name="slotIndex">편성 슬롯 번호(0부터).</param>
        public static Combatant FromEnemy(EnemySO enemy, int slotIndex)
        {
            var skills = BuildSkills(true, enemy.basicSkill, enemy.normalSkill, enemy.ultimateSkill);
            return new Combatant(enemy.displayName, Team.Enemy, slotIndex, enemy.baseStats, skills);
        }

        // 붙어 있는 버프·디버프를 기본 스탯에 접어 유효 스탯을 낸다.
        // 스탯별 배수 = 1 + 버프율 합 − 디버프율 합(percent-add). atk·spd는 1 미만,
        // def는 0 미만으로 내려가지 않는다(spd는 게이지 나눗셈 분모라 0 금지).
        // 최대 HP·치명 계열은 수정자 대상이 아니라 기본값을 그대로 쓴다.
        private Stats ComputeEffectiveStats()
        {
            float atkM = 1f, defM = 1f, spdM = 1f;
            foreach (var e in _effects)
            {
                if (e.Type != EffectType.Buff && e.Type != EffectType.Debuff) continue;
                float delta = e.Type == EffectType.Buff ? e.Value : -e.Value;
                // 수정 대상 스탯은 atk·def·spd뿐. 크리·hp 대상 효과는 지원하지 않아 무시된다.
                switch (e.Stat)
                {
                    case StatType.Atk: atkM += delta; break;
                    case StatType.Def: defM += delta; break;
                    case StatType.Spd: spdM += delta; break;
                }
            }

            return new Stats
            {
                hp = _baseStats.hp,
                atk = Math.Max(1, (int)Math.Round(_baseStats.atk * atkM, MidpointRounding.AwayFromZero)),
                def = Math.Max(0, (int)Math.Round(_baseStats.def * defM, MidpointRounding.AwayFromZero)),
                spd = Math.Max(1, (int)Math.Round(_baseStats.spd * spdM, MidpointRounding.AwayFromZero)),
                critRate = _baseStats.critRate,
                critDamage = _baseStats.critDamage,
            };
        }

        // null이 아닌 슬롯만 런타임으로 감싼다(적은 슬롯이 비어 있을 수 있다).
        // startOnCooldown이면 각 액티브를 자기 쿨만큼 잠근 채 시작한다(기본공격은 쿨 0이라 그대로 열림).
        private static List<SkillRuntime> BuildSkills(bool startOnCooldown, params SkillSO[] slots)
        {
            var list = new List<SkillRuntime>();
            foreach (var s in slots)
                if (s != null) list.Add(new SkillRuntime(s, startOnCooldown ? s.cooldownTurns : 0));
            return list;
        }
    }
}