using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 스테이지 선택 화면 표시 데이터이자 전투 진입 편성의 소유자.
    /// 선택 시 <see cref="enemies"/>가 전투 진입 파라미터로 넘어간다.
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Stages/Stage Data")]
    public sealed class StageSO : ScriptableObject
    {
        /// <summary> 참조·조회용 고정 키(표시명과 분리). </summary>
        public string id;

        /// <summary> UI 표시명(로컬라이즈 대상). </summary>
        public string displayName;

        /// <summary> 스테이지 설명(선택 화면 상세 문구). </summary>
        [TextArea] public string description;

        /// <summary> 스테이지 항목 썸네일 스프라이트. </summary>
        public Sprite thumbnail;

        /// <summary> 전투 배경 스프라이트. 방을 옮겨도 씬은 그대로 두고 이 스프라이트만 교체한다. </summary>
        public Sprite background;

        /// <summary> 이 스테이지 적 스탯에 곱하는 난이도 배수. </summary>
        public float enemyStatMultiplier = 1f;

        /// <summary> 스테이지 안에서 얻는 재화에 곱하는 배수. </summary>
        public float currencyMultiplier = 1f;

        /// <summary> 입장에 드는 스태미나. [보류 스태미나] 읽는 곳이 아직 없다. </summary>
        public int staminaCost = 6;

        /// <summary> 클리어할 때 캐릭터 픽 보상을 주는지 정한다. 챕터 마지막 스테이지만 true다. </summary>
        public bool grantsAscensionPick;

        /// <summary> 보스 스테이지 여부. 항목에 보스 프레임을 표시하고 장의 마지막 스테이지로 배치한다. </summary>
        public bool isBoss;

        /// <summary> 이 스테이지의 적 편성. 배열 순서가 전장 슬롯 순서(SlotIndex)이며, 전투 진입 시 앞에서부터 참전한다. </summary>
        public EnemySO[] enemies;

        /// <summary> 승리할 때마다 지급하는 보상. 반복 클리어에도 매번 그대로 지급된다. </summary>
        public RewardEntry[] clearRewards;

        /// <summary> 최초 클리어에 한해 <see cref="clearRewards"/>에 더해 지급하는 보상. 같은 재화면 합산된다. </summary>
        public RewardEntry[] firstClearRewards;
    }
}
