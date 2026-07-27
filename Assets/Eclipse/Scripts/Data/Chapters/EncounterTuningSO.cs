using System;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary> 방 깊이 한 단계의 인카운터 생성 규칙. </summary>
    [Serializable]
    public struct DepthPool
    {
        /// <summary> 방 깊이. 1부터 시작하며 보스 방 직전까지를 덮는다. </summary>
        public int depth;

        /// <summary> 이 깊이에서 나올 수 있는 적. 슬롯마다 여기서 균등 추첨한다. </summary>
        public EnemySO[] allowedPool;

        /// <summary> 마리수 하한. 1 이상이다. </summary>
        public int minCount;

        /// <summary> 마리수 상한. 전장 슬롯이 4칸이라 4를 넘길 수 없다. </summary>
        public int maxCount;

        /// <summary> 마리당 변이 적중 확률. 마리마다 독립으로 굴린다. </summary>
        [Range(0f, 1f)] public float mutationChance;
    }

    /// <summary>
    /// 챕터가 공유하는 인카운터 튜닝 데이터. 깊이 곡선은 방이 달라도 같고,
    /// 챕터별 난이도 차이는 <see cref="ChapterSO.enemyStatMultiplier"/>가 담당한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Chapters/Encounter Tuning Data")]
    public sealed class EncounterTuningSO : ScriptableObject
    {
        /// <summary> 일반 방의 깊이별 규칙. 깊이를 빠짐없이 한 번씩 덮어야 한다. </summary>
        public DepthPool[] depths;

        /// <summary> 보스 방의 보스. 일반 풀에는 들어가지 않는다. </summary>
        public EnemySO boss;

        /// <summary> 보스와 함께 나오는 수하. 보스 방 편성은 고정이다. </summary>
        public EnemySO[] bossAdds;

        /// <summary> 변이 후보. 변이가 적중하면 이 중 하나를 균등 선택한다. </summary>
        public MutationSO[] mutations;

        /// <summary> 정예 인카운터가 받는 전 스탯 배수. </summary>
        public float eliteStatMultiplier = 1.15f;
    }
}
