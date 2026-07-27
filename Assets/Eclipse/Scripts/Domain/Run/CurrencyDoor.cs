using System;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 재화 문의 약속액·실지급액 계산. 지급은 하지 않는다 — 지갑 접촉은 IRewardService 한 곳이다.
    /// 롤 시점은 문 선택이 아니라 다음 방 클리어 후 공개 시점이다(8종 공통 지연 지급).
    /// </summary>
    public static class CurrencyDoor
    {
        // 확률 폭을 정수 롤로 바꾸는 분해능. ±30%면 [-3000, +3000]에서 굴린다.
        private const int JitterScale = 10000;

        /// <summary>
        /// 문 앞면에 적히는 "약 N"(폭 미적용 기대값). 난수를 쓰지 않으므로 화면을 몇 번 그려도 값이 흔들리지 않는다.
        /// </summary>
        /// <param name="depth">문 지점 깊이(1부터).</param>
        public static int PromisedAmount(DoorKind kind, int depth, float chapterCurrencyMultiplier,
            DoorCatalogSO catalog)
            => Round(BaseAmount(kind, depth, catalog) * chapterCurrencyMultiplier);

        /// <summary>
        /// 실제 지급액을 굴린다. 기본량 × 챕터 재화 배수 × (1 ± 폭), 반올림은 마지막에 1회.
        /// 교본은 비율 폭 대신 ±1 고정 폭이다.
        /// </summary>
        public static RewardEntry Roll(DoorKind kind, int depth, float chapterCurrencyMultiplier,
            DoorCatalogSO catalog, IRunRandom rng)
        {
            float baseAmount = BaseAmount(kind, depth, catalog) * chapterCurrencyMultiplier;
            int amount;
            if (kind == DoorKind.Manual)
            {
                amount = Round(baseAmount) + rng.NextInt(3) - 1;
            }
            else
            {
                int span = (int)Math.Round(catalog.currencyJitter * JitterScale);
                float factor = 1f + (rng.NextInt(span * 2 + 1) - span) / (float)JitterScale;
                amount = Round(baseAmount * factor);
            }
            return new RewardEntry { type = CurrencyOf(kind), amount = Math.Max(0, amount) };
        }

        /// <summary> 재화 문이 지급하는 재화 종류. 재화 문이 아니면 예외. </summary>
        public static CurrencyType CurrencyOf(DoorKind kind) => kind switch
        {
            DoorKind.Gold => CurrencyType.Gold,
            DoorKind.Manual => CurrencyType.Manual,
            DoorKind.Essence => CurrencyType.Essence,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "재화 문이 아니다."),
        };

        /// <summary> 재화 문 여부. 버프 문과 처리 경로를 가르는 판정. </summary>
        public static bool IsCurrency(DoorKind kind)
            => kind == DoorKind.Gold || kind == DoorKind.Manual || kind == DoorKind.Essence;

        private static float BaseAmount(DoorKind kind, int depth, DoorCatalogSO catalog) => kind switch
        {
            DoorKind.Gold => catalog.goldPerDepth * depth,
            DoorKind.Essence => catalog.essencePerDepth * depth,
            DoorKind.Manual => catalog.manualPerDepth * depth + catalog.manualBase,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "재화 문이 아니다."),
        };

        private static int Round(float value)
            => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}