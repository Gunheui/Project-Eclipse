using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 데미지 공식의 전역 밸런스 계수. 코드 재빌드 없이 인스펙터에서 튜닝하는 값만 담는다.
    /// (내부 정밀도 상수인 ATB 임계값은 밸런스 값이 아니라 스케줄러 코드에 둔다 — 데이터화 대상 아님.)
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Battle/Battle Constants")]
    public class BattleConstantsSO : ScriptableObject
    {
        [Tooltip("비율경감 계수 k — 경감계수 = ATK/(ATK+DEF×k). 클수록 DEF가 강해진다.")]
        public float defenseK = 1f;

        [Tooltip("데미지 난수변동 하한 (예: 0.95 = −5%).")]
        public float varianceMin = 0.95f;

        [Tooltip("데미지 난수변동 상한 (예: 1.05 = +5%).")]
        public float varianceMax = 1.05f;
    }
}
