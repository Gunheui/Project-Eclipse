using Eclipse.Data.Enums;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 캐릭터 한 명의 정의 데이터. 스탯·스킬·성장곡선·아트를 묶는다.
    /// 레벨·돌파 등 가변 진행값은 여기 두지 않는다(세이브 데이터 소관).
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Characters/Character Data")]
    public class CharacterSO : ScriptableObject
    {
        /// <summary> 참조·조회용 고정 키(표시명과 분리). </summary>
        public string id;

        /// <summary> UI 표시명(로컬라이즈 대상). </summary>
        public string displayName;

        /// <summary> 등급(R/SR/SSR) — 가챠 확률·UI 표기 기준. </summary>
        public Rarity rarity;

        /// <summary> 역할(탱커/딜러/서포터/힐러) — 편성 필터·밸런싱 그룹. </summary>
        public Role role;

        /// <summary> Lv1 기본 능력치 6종. 레벨 스케일 값은 growthCurve로 계산. </summary>
        public Stats baseStats;

        /// <summary> 성장 규칙 참조 — 여러 캐릭터가 공유·교체 가능. </summary>
        public GrowthCurve growthCurve;

        /// <summary> 기본 공격(쿨 0 — 항상 열려 있는 폴백). </summary>
        public SkillSO basicSkill;

        /// <summary> 일반 스킬(짧은 쿨). </summary>
        public SkillSO normalSkill;

        /// <summary> 궁극기(긴 쿨). </summary>
        public SkillSO ultimateSkill;

        /// <summary> 획득 경로(스타터/스토리지급/가챠픽업). </summary>
        public AcquisitionType acquisitionType;

        /// <summary> UI에 쓰는 전신 일러. 캐릭터 목록·상세·편성·로비·캐릭터 문이 이걸 쓴다. </summary>
        public Sprite portraitAssetRef;

        /// <summary>
        /// 초상 뒤에 겹치는 캐릭터 이펙트 레이어. portraitAssetRef와 같은 2048 프레임·같은 피벗이라
        /// 같은 RectTransform에 올리면 정렬이 맞는다. 비면 이펙트 없이 초상만 그린다.
        /// </summary>
        public Sprite portraitFxAssetRef;

        /// <summary>
        /// 캐릭터 목록 카드의 초상 세로 보정(px). 원본마다 발끝 아래 여백이 달라 카드 바닥선이 어긋난다.
        /// 양수면 위로 올린다.
        /// </summary>
        public float portraitListOffsetY;

        /// <summary>
        /// 편성 카드의 초상 세로 보정(px). 원본마다 얼굴 높이가 달라 같은 프레이밍에서도 잘리는 위치가 다르다.
        /// 양수면 위로 올린다.
        /// </summary>
        public float portraitCardOffsetY;

        /// <summary> 전투 씬에 세우는 그림. 월드 스프라이트라 임포트 PPU가 곧 전투 크기가 된다. </summary>
        public Sprite battlerAssetRef;

        /// <summary> 턴 순서 타임라인용 얼굴 아이콘(정사각 크롭). 비면 portraitAssetRef로 폴백. </summary>
        public Sprite faceIconAssetRef;
    }
}