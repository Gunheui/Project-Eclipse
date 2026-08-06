using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 이펙트를 놓을 기준점. 높이 세 종류는 재생하는 배틀러의 실루엣에서 나오고, 진영 두 종류는
    /// 시전자 기준 자기 편·상대 편의 범위 중심이다.
    /// </summary>
    public enum VfxAnchor { Foot, Center, Overhead, AllAllies, AllEnemies }

    /// <summary>
    /// 유지 이펙트를 보여 주는 방식. Continuous는 켜 둔 채로 두고, EachTurn은 턴마다 잠깐씩 다시 재생한다.
    /// </summary>
    public enum VfxHold { Continuous, EachTurn }

    /// <summary>
    /// 파티클 이펙트를 이루는 프리팹 레이어 하나. 여러 겹을 시차를 두고 쌓아 스킬 하나의 연출을 만든다.
    /// </summary>
    [Serializable]
    public class VfxLayer
    {
        [Tooltip("재생할 파티클 프리팹.")]
        public GameObject prefab;

        [Tooltip("재생 위치 기준점. 진영 앵커는 시전용 스펙에만 쓴다. 피격용은 대상마다 재생돼 같은 자리에 겹친다.")]
        public VfxAnchor anchor;

        [Tooltip("앵커 기준 오프셋(월드 단위).")]
        public Vector2 offset;

        [Tooltip("재생 시작까지의 지연(초). 레이어를 어긋나게 겹칠 때 쓴다.")]
        public float startDelay;

        [Tooltip("회전(오일러 각). 3D 평면 프리팹을 카메라 정면으로 세울 때 쓴다.")]
        public Vector3 rotation;

        [Tooltip("프리팹 원본 크기에 곱할 배율.")]
        public float scale = 1f;

        [Tooltip("파티클 시작색을 아래 색으로 갈아 끼운다. 끄면 프리팹 원본색을 쓴다.")]
        public bool overrideColor;

        [Tooltip("갈아 끼울 색.")]
        public Color color = Color.white;

        [Tooltip("정렬 순서. 발밑 5, 타격·피격 15가 기준이다.")]
        public int sortingOrder = 15;

        [Tooltip("반복 재생 횟수.")]
        public int repeatCount = 1;

        [Tooltip("반복 사이 간격(초).")]
        public float repeatInterval = 0.15f;

        [Tooltip("이 레이어를 기다릴 시간(초). 프리팹 재생 길이와는 별개로, 턴이 넘어가는 시점을 정한다.")]
        public float awaitSeconds = 0.4f;

        [Tooltip("0보다 크면 이 레이어를 유지 이펙트로 쓴다. 유지 기간은 스킬이 건 지속 효과가 정하므로 값의 크기는 쓰이지 않는다.")]
        public int holdTurns;

        [Tooltip("유지 방식. 유지 이펙트가 아니면 쓰이지 않는다.")]
        public VfxHold holdMode;
    }

    /// <summary>
    /// 파티클 프리팹 레이어들로 이루어진 이펙트 한 개의 정의. 스킬이 시전용·피격용으로 참조한다.
    /// 흰 스프라이트를 트윈으로 굴리는 방식은 <see cref="EffectSpec"/>이 따로 담당한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Effects/Vfx Spec")]
    public class VfxSpec : ScriptableObject
    {
        [Tooltip("겹쳐 재생할 파티클 레이어들. startDelay로 타이밍을 어긋나게 준다.")]
        public List<VfxLayer> layers = new();
    }
}
