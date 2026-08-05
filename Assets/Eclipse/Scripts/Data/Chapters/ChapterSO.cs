using System;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary> 방의 종류. 정예는 방4 미드보스, 보스는 마지막 방이다. </summary>
    public enum RoomKind { Normal, Elite, Boss }

    /// <summary> 챕터 런의 방 한 칸. 순서가 곧 방 진행 순서다. </summary>
    [Serializable]
    public struct RoomLayout
    {
        /// <summary> 방의 종류. </summary>
        public RoomKind kind;

        /// <summary> 인카운터 생성 깊이. Normal/Elite만 유효하고 Boss 행은 무시한다(생성기가 고정 편성). </summary>
        public int depth;

        /// <summary> 이 방 클리어 후 문 지점이 열리는지 정한다. </summary>
        public bool doorAfter;
    }

    /// <summary> 정산 표의 한 행. 재화 3종 고정 슬롯. </summary>
    [Serializable]
    public struct SettlementRow
    {
        public int gold;
        public int manual;
        public int essence;
    }

    /// <summary>
    /// 챕터 = 런 1판의 정의 데이터. 방 배치·난이도 계수·도달 깊이 정산 표를 보유한다.
    /// 챕터 2~5 확장은 이 에셋의 행 추가로 한다(코드 무수정).
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Chapters/Chapter Data")]
    public sealed class ChapterSO : ScriptableObject
    {
        /// <summary> 진행/해금 상태의 조회 키. 세이브 데이터와 매핑된다(표시명과 분리). </summary>
        public string id;

        /// <summary> 장 번호. 패널·내비 라벨의 표기 기준(예: 1 → "01" / "1장"). </summary>
        public int number;

        /// <summary> 장 표시명(로컬라이즈 대상). </summary>
        public string displayName;

        /// <summary> 장 설명(편성 화면 문구). </summary>
        [TextArea] public string description;

        /// <summary> 방 배치. 배열 순서가 방 진행 순서이며 마지막 행이 보스다. 챕터 1 = 7행. </summary>
        public RoomLayout[] rooms;

        /// <summary> 미드보스 문에 거는 초상. 로드 검증 대상이 아니라 비어 있으면 그림 없이 선다. </summary>
        public Sprite midBossPortrait;

        /// <summary> 최종보스 문 거울에 거는 보스 얼굴. 비어 있으면 빈 거울로 선다. </summary>
        public Sprite bossFace;

        public Sprite normalBackground;

        /// <summary> 정예 전투 배경. 방4에 고정으로 서지 않고 미드보스 문을 고른 전투에만 쓴다. </summary>
        public Sprite eliteBackground;

        public Sprite bossBackground;

        /// <summary> 이 챕터 적 스탯에 곱하는 난이도 배수. </summary>
        public float enemyStatMultiplier = 1f;

        /// <summary> 이 챕터에서 얻는 재화에 곱하는 배수. </summary>
        public float currencyMultiplier = 1f;

        /// <summary> 도달 깊이 정산 표. 인덱스 = 넘긴 방 수라 행 수는 방 수 + 1이어야 한다. </summary>
        public SettlementRow[] settlement;

        /// <summary> 챕터 클리어 시 정산에 더하는 승리 보너스. </summary>
        public SettlementRow victoryBonus;
    }
}
