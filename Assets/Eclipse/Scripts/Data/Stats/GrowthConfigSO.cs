using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 전역 성장 상수. 특정 캐릭터가 아니라 성장 시스템 전체에 적용되는 밸런스 값을 담는다.
    /// 캐릭터별 성장 규칙은 <see cref="GrowthCurve"/>가, 이 계정 무관 상수는 이 에셋이 보유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Growth/Growth Config")]
    public class GrowthConfigSO : ScriptableObject
    {
        [Tooltip("레벨업 비용 계수. 1회 비용 = 이 값 × 현재 레벨(길드 금화).")]
        public int levelUpCostCoefficient = 100;
    }
}
