using System;

namespace Eclipse.Data
{
    /// <summary>
    /// 유닛의 전투 능력치 6종을 묶은 값 타입.
    /// 캐릭터 기본 스탯과 전투 중 현재 스탯이 이 구조를 공유한다.
    /// </summary>
    [Serializable]
    public struct Stats
    {
        /// <summary> 최대 체력. </summary>
        public int hp;

        /// <summary> 공격력 — 데미지·힐 계산의 기본값. </summary>
        public int atk;

        /// <summary> 방어력 — 받는 피해 경감. </summary>
        public int def;

        /// <summary> 속도 — 라운드 행동 순서 결정. </summary>
        public int spd;

        /// <summary> 치명타 확률 (0.3 = 30%). </summary>
        public float critRate;

        /// <summary> 치명타 배율 (2.0 = 2배 피해). </summary>
        public float critDamage;
    }
}