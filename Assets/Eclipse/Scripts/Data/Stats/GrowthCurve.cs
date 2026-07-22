using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 스탯이 레벨에 따라 어떻게 오르는지를 정의하는 '성장 규칙' 데이터.
    /// 스탯 수치 자체는 담지 않고, 계수(g·maxLevel)와 스케일 공식만 가진다.
    /// 여러 캐릭터가 하나의 곡선 에셋을 공유·교체할 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Growth/Growth Curve")]
    public class GrowthCurve : ScriptableObject
    {
        /// <summary> 성장률. 레벨당 base × growthRate 만큼 선형 증가(기본 0.07). </summary>
        public float growthRate;

        /// <summary> 도달 가능한 최대 레벨(기본 30). </summary>
        public int maxLevel;

        /// <summary>
        /// 주어진 base 스탯의 지정 레벨 값을 반환한다.
        /// 공식: base × (1 + growthRate × (level − 1)). level 범위 검증은 호출부 책임.
        /// </summary>
        public float StatAtLevel(float baseStat, int level)
        {
            var currStat = baseStat * (1 + growthRate * (level - 1));
            return currStat;
        }
    }
}