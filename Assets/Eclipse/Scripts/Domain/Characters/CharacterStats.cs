using Eclipse.Data;
using UnityEngine;

namespace Eclipse.Domain
{
    /// <summary>
    /// 캐릭터 정의와 레벨로부터 현재 스탯을 계산하는 도메인 서비스.
    /// 표시(상세 화면)와 전투(Combatant)가 같은 공식을 공유한다.
    /// </summary>
    public static class CharacterStats
    {
        /// <summary>
        /// 정의의 기본 스탯을 성장곡선으로 레벨 스케일해 현재 스탯을 반환한다.
        /// HP·ATK·DEF만 스케일하고, SPD·치명확률·치명배율은 기본값을 유지한다.
        /// </summary>
        /// <param name="definition">캐릭터 정의(기본 스탯·성장곡선 보유).</param>
        /// <param name="level">현재 레벨(1 이상).</param>
        /// <returns>레벨이 반영된 현재 스탯 스냅샷.</returns>
        public static Stats ScaleToLevel(CharacterSO definition, int level)
        {
            var baseStats = definition.baseStats;
            var curve = definition.growthCurve;
            return new Stats
            {
                hp = Mathf.RoundToInt(curve.StatAtLevel(baseStats.hp, level)),
                atk = Mathf.RoundToInt(curve.StatAtLevel(baseStats.atk, level)),
                def = Mathf.RoundToInt(curve.StatAtLevel(baseStats.def, level)),
                spd = baseStats.spd,
                critRate = baseStats.critRate,
                critDamage = baseStats.critDamage,
            };
        }
    }
}
