using System;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary> 버프 카드의 풀 분류. Special은 공격·수호·질풍 풀에 낮은 가중으로 혼입된다. </summary>
    public enum CardTag { Attack, Guard, Haste, Special, Bond, Curse }

    /// <summary> 버프 카드 한 장. 효과는 스탯 증감(StatDelta) 목록으로만 기술한다. </summary>
    [Serializable]
    public struct BuffCard
    {
        /// <summary> 참조·조회용 고정 키. </summary>
        public string id;

        /// <summary> 표시명. </summary>
        public string displayName;

        /// <summary> 풀 분류. </summary>
        public CardTag tag;

        /// <summary>
        /// 스탯 증감 목록. 아군 카드는 부호 그대로 더하고(감소는 음수),
        /// 저주(targetsEnemies) 카드는 약화 크기를 양수로 적는다 — 적 스탯 계산이 합을 빼는 방향이다.
        /// </summary>
        public StatDelta[] deltas;

        /// <summary> 인연 카드의 대상 캐릭터 id. 인연(Bond) 카드만 채운다. </summary>
        public string requiredCharacterId;

        /// <summary> 풀 안 추첨 가중치. </summary>
        public int weight;

        /// <summary> true면 문 지점 2 이후에만 후보에 든다(특수 카드 노출 제한). </summary>
        public bool specialPointRestricted;

        /// <summary> true면 배정 없이 런 전역 적 디버프로 들어간다(저주 풀). </summary>
        public bool targetsEnemies;
    }

    /// <summary> 버프 카드 카탈로그(단일 에셋, 20장). 카드 추가는 코드 무수정·행 추가다. </summary>
    [CreateAssetMenu(menuName = "Eclipse/Chapters/Buff Card Catalog")]
    public sealed class BuffCardCatalogSO : ScriptableObject
    {
        public BuffCard[] cards;
    }
}