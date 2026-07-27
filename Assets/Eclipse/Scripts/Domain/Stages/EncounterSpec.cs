using System.Collections.Generic;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 방 하나의 적 편성 결과. 스탯 배수는 담지 않고 마리별 스펙만 싣는다.
    /// 최종 스탯은 전투원을 조립할 때 <see cref="CharacterStats.BuildEnemyStats"/>가 계산한다.
    /// </summary>
    public readonly struct EncounterSpec
    {
        public EncounterSpec(IReadOnlyList<EnemyInstanceSpec> enemies)
        {
            Enemies = enemies;
        }

        /// <summary> 마리별 스펙. 배열 순서가 전장 슬롯 순서(SlotIndex)다. </summary>
        public IReadOnlyList<EnemyInstanceSpec> Enemies { get; }
    }

    /// <summary> 적 한 마리의 조우 스펙. 같은 적이라도 변이·정예 여부에 따라 다른 개체가 된다. </summary>
    public readonly struct EnemyInstanceSpec
    {
        public EnemyInstanceSpec(EnemySO enemy, MutationSO mutation, bool isElite)
        {
            Enemy = enemy;
            Mutation = mutation;
            IsElite = isElite;
        }

        /// <summary> 적 정의. </summary>
        public EnemySO Enemy { get; }

        /// <summary> 침식 변이. 변이가 없으면 null이다. </summary>
        public MutationSO Mutation { get; }

        /// <summary> 정예 조우로 생성된 개체인지 알려 준다. </summary>
        public bool IsElite { get; }
    }
}
