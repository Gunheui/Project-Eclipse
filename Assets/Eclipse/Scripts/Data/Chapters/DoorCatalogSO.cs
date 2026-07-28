using System;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 약속 문 5종. 값은 카탈로그 배열 인덱스와 무관한 식별자다.
    /// 정수값을 명시로 못박아 둔다. 순번이 밀리면 이미 저장된 카탈로그 행의 종류가 조용히 바뀐다.
    /// </summary>
    public enum DoorKind { CharacterBuff = 3, Curse = 4, Gold = 5, Manual = 6, Essence = 7 }

    /// <summary> 약속 문 한 종의 정의. </summary>
    [Serializable]
    public struct DoorDefinition
    {
        /// <summary> 문 종류. </summary>
        public DoorKind kind;

        /// <summary> 표시명(예: "골드의 문"). </summary>
        public string displayName;

        /// <summary> 추첨 가중치. 합이 0이면 로드 검증에서 걸린다. </summary>
        public int weight;

        /// <summary> 문에 적히는 약속 문구. </summary>
        public string promiseText;

        /// <summary> 문 아이콘. </summary>
        public Sprite icon;
    }

    /// <summary>
    /// 약속 문 카탈로그(단일 에셋). 문 5종 정의와 재화 문 지급 공식 계수를 보유한다 —
    /// 계수는 문 데이터라 문 카탈로그가 소유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Chapters/Door Catalog")]
    public sealed class DoorCatalogSO : ScriptableObject
    {
        /// <summary> 문 정의 5종. </summary>
        public DoorDefinition[] doors;

        /// <summary> 골드 문 기본량 = 이 값 × 문 지점 깊이. </summary>
        public int goldPerDepth = 300;

        /// <summary> 보석 문 기본량 = 이 값 × 문 지점 깊이. </summary>
        public int essencePerDepth = 60;

        /// <summary> 골드·보석 문 지급액의 랜덤 폭(비율). </summary>
        [Range(0f, 1f)] public float currencyJitter = 0.30f;
    }
}