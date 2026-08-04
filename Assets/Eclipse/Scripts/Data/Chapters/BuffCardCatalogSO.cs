using System;
using Eclipse.Data.Enums;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary> 카드 등급. 추첨 가중과 표시색이 이 축 하나를 따라간다. </summary>
    public enum CardGrade { Common, Rare, Epic, Unique }

    /// <summary>
    /// 버프 카드 한 장. 효과는 카드당 하나이며, 범용·저주는 스탯 증감으로 유니크는 스킬 수정으로 기술한다.
    /// </summary>
    [Serializable]
    public struct BuffCard
    {
        /// <summary> 참조·조회용 고정 키. </summary>
        public string id;

        /// <summary> 표시명. 같은 효과의 세 등급이 이름을 공유한다. </summary>
        public string displayName;

        public CardGrade grade;

        /// <summary>
        /// 스탯 증감. 저주도 부호 그대로 음수로 적는다 — 적 스탯이 아군과 같은 덧셈 경로를 탄다.
        /// 유니크는 스탯을 건드리지 않아 비운다.
        /// </summary>
        public StatDelta[] deltas;

        /// <summary> 유니크 카드의 효과 한 줄. 범용·저주는 증감값에서 문구를 만들어 쓰므로 비워 둔다. </summary>
        public string description;

        /// <summary> 이 카드를 쓸 수 있는 캐릭터 id. 유니크 카드만 채운다. </summary>
        public string requiredCharacterId;

        /// <summary> true면 배정 없이 런 전역 적 디버프로 들어간다(저주 풀). </summary>
        public bool targetsEnemies;

        /// <summary> 유니크 카드가 고칠 스킬 자리. 유니크가 아니면 읽히지 않는다. </summary>
        public SkillSlot targetSkill;

        /// <summary> <see cref="targetSkill"/>의 효과 목록에 덧붙일 효과 한 건. 유니크 카드만 채운다. </summary>
        public SkillEffect addedEffect;
    }

    /// <summary> 버프 카드 카탈로그(단일 에셋). 카드 추가는 코드 무수정·행 추가다. </summary>
    [CreateAssetMenu(menuName = "Eclipse/Chapters/Buff Card Catalog")]
    public sealed class BuffCardCatalogSO : ScriptableObject
    {
        public BuffCard[] cards;

        // 등급 가중은 행마다 적지 않고 이 노브 넷에서 파생한다.
        public int commonWeight = 60;
        public int rareWeight = 30;
        public int epicWeight = 10;
        public int uniqueWeight = 60;

        /// <summary> 이 등급 카드 한 장의 추첨 가중. </summary>
        /// <exception cref="ArgumentOutOfRangeException">등급이 노브에 없을 때.</exception>
        public int WeightOf(CardGrade grade) => grade switch
        {
            CardGrade.Common => commonWeight,
            CardGrade.Rare => rareWeight,
            CardGrade.Epic => epicWeight,
            CardGrade.Unique => uniqueWeight,
            _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "등급 가중이 정의되지 않았다."),
        };
    }
}
