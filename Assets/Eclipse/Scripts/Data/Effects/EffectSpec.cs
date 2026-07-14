using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 이펙트 트윈의 이징(시간 대비 변화 곡선). Data는 DOTween을 참조하지 않으므로 자체 enum으로 두고,
    /// View 재생기가 DG.Tweening.Ease로 매핑한다.
    /// </summary>
    public enum EffectEase { Linear, OutQuad, InQuad, OutCubic, OutBack, InOutQuad }

    /// <summary>
    /// 이펙트를 이루는 스프라이트 레이어 하나. 스폰 후 지정 시간 동안 스케일·회전·이동·페이드로 애니메이션된다.
    /// 스프라이트는 흰색 마스크라 tint로 속성색을 입힌다.
    /// </summary>
    [Serializable]
    public class EffectLayer
    {
        [Tooltip("표시할 스프라이트(흰색 마스크).")]
        public Sprite sprite;

        [Tooltip("앵커(시전자/대상 위치) 기준 로컬 오프셋.")]
        public Vector2 offset;

        [Tooltip("재생 시작까지의 지연(초). 레이어를 어긋나게 겹칠 때 쓴다.")]
        public float startDelay;

        [Tooltip("이 레이어의 애니메이션 지속(초).")]
        public float duration = 0.2f;

        [Tooltip("시작 스케일(배수).")]
        public Vector2 startScale = Vector2.one;

        [Tooltip("끝 스케일(배수).")]
        public Vector2 endScale = Vector2.one;

        [Tooltip("초당 회전 각(도). 0이면 회전 없음.")]
        public float spinDegPerSec;

        [Tooltip("지속 동안의 이동량(로컬).")]
        public Vector2 moveBy;

        [Tooltip("시작 색(알파 포함). 흰 마스크에 곱해져 속성색이 된다.")]
        public Color tint = Color.white;

        [Tooltip("끝 알파. 보통 0으로 페이드아웃.")]
        public float endAlpha;

        [Tooltip("스케일·이동·페이드에 적용할 이징.")]
        public EffectEase ease = EffectEase.OutQuad;

        [Tooltip("정렬 순서. 큰 값이 위에 그려진다.")]
        public int sortingOrder;

        [Tooltip("가산(Additive) 블렌드. 흰 마스크를 발광처럼 보이게 한다.")]
        public bool additive;
    }

    /// <summary>
    /// 스프라이트 레이어들의 조합으로 이루어진 이펙트 한 개의 정의. 스킬이 시전/피격용으로 참조한다.
    /// 정지 스프라이트를 스폰해 DOTween으로 애니메이션하는 방식(프레임 시퀀스 아님).
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Effects/Effect Spec")]
    public class EffectSpec : ScriptableObject
    {
        [Tooltip("겹쳐 재생할 스프라이트 레이어들. startDelay로 타이밍을 어긋나게 준다.")]
        public List<EffectLayer> layers = new();
    }
}
