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

        [Tooltip("전장 누적 행동 상한. 이 횟수를 넘으면 미클리어(패배)로 종료 — 무한 루프 방지.")]
        public int globalActionCap = 200;

        [Tooltip("적 AI가 처치 가능한 대상을 실제로 마무리할 확률. 1 = 아군 오토처럼 항상 막타, 0 = 막타 층 미사용. " +
                 "확률 판정에 실패하면 저HP 가중랜덤으로 내려간다 — 힐러가 살릴 여지를 남기려는 값.")]
        [Range(0f, 1f)]
        public float enemyLethalChance = 0.6f;

        [Tooltip("적 AI가 다친 대상을 노리는 강도. 가중치 = 1 + 이 값 × (1 − HP비율). " +
                 "0 = HP 무관 완전 균등, 클수록 저HP 집중(난이도 상승). 하드 저격을 피하려는 값이라 과하게 올리지 말 것.")]
        public float enemyLowHpBias = 0.5f;
    }
}
