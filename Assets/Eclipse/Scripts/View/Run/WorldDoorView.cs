using System;
using System.Collections.Generic;
using Eclipse.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Eclipse.View
{
    /// <summary>
    /// 전장에 세우는 문 하나. 티어별 프레임을 걸고, 그림과 보상 심볼을 프레임 상단의 원형 거울 안에
    /// 마스크로 잘라 그린다. 프레임 탭이 선택 입력이다(Bind의 onTapped).
    /// </summary>
    public class WorldDoorView : MonoBehaviour, IPointerClickHandler
    {
        /// <summary> 티어 하나의 소품 — 프레임 그림·그림자 그림과 프레임의 거울 중심(아트 픽셀, 좌상단 원점). </summary>
        [Serializable]
        private struct TierLook
        {
            public Sprite frame;
            public Sprite shadow;
            public Vector2 mirrorCenterPx;
        }

        [SerializeField] private SpriteRenderer frameRenderer;
        [SerializeField] private SpriteRenderer shadowRenderer;
        [SerializeField] private SpriteRenderer iconRenderer;

        // 걸린 보상 심볼 자리. 배치 순서가 해소 순서라 0번이 좌하단, 1번이 우상단이다. 미드보스 문이 아니면 전부 꺼진다.
        [SerializeField] private SpriteRenderer[] rewardSymbols;

        // 거울 자리의 원형 마스크. 거울 안 렌더러들은 프리팹에서 VisibleInsideMask로 잠가 둔다.
        [SerializeField] private SpriteMask mirrorMask;

        // 탭 판정 영역. 프레임 크기로 에디터에서 고정한다 — 그림 크기와 무관하게 손가락 여유를 보장한다.
        [SerializeField] private BoxCollider2D tapArea;

        // 인덱스 = DoorTier. 프레임은 스케일 1로 원척으로 그린다 — 거울 좌표 변환이 원척을 전제한다.
        [SerializeField] private TierLook[] tierLooks;

        // 거울 지름(아트 픽셀). 세 프레임이 같은 지름을 쓴다.
        [SerializeField] private float mirrorDiameterPx = 124f;

        // 심볼 지름을 거울 지름에 대한 비율로 정한다. 둘을 대각선으로 어긋 놓아 나란히보다 크게 잡은 값이다.
        private const float RewardSymbolScale = 0.58f;

        // 선택 대기 중 프레임에 켜는 아웃라인 색. 일반 문은 전투의 아군 강조와 같은 녹색 계열,
        // 미드보스·보스 문은 보라색으로 격을 가른다.
        private static readonly Color PromiseOutline = new(0.306f, 0.608f, 0.478f, 1f);
        private static readonly Color BossOutline = new(0.651f, 0.420f, 0.878f, 1f);

        // 두께는 월드 단위로 정의하고 셰이더에 넘길 때 PPU로 환산한다(배틀러 아웃라인과 같은 규약).
        private const float OutlineWorldThickness = 0.03f;

        // 실루엣 판정 컷오프. 그림자는 이제 별도 렌더러(shadowRenderer)로 프레임 밖에서 그리므로
        // 프레임 아트 자체에는 반투명부가 없다 — 이 값은 안전망으로만 남긴다.
        private const float OutlineAlphaCutoff = 0.6f;

        // Eclipse/SpriteOutlineURP2D의 아웃라인 프로퍼티. MaterialPropertyBlock으로 문마다 따로 덮어쓴다.
        private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
        private static readonly int OutlineAlphaCutoffId = Shader.PropertyToID("_OutlineAlphaCutoff");

        // 아웃라인 오버라이드 전달용. 첫 사용 때 만들어 재사용한다(머티리얼 인스턴스 복제를 피한다).
        private MaterialPropertyBlock _mpb;

        private Action<WorldDoorView> _onTapped;

        /// <summary> 이 문이 표시 중인 선택지. 탭이 확정되면 이 값의 보상이 그대로 보류된다. </summary>
        public DoorOption Option { get; private set; }

        /// <summary> 문을 한 선택지에 연결해 세운다. 프레임·거울 위치·거울 안 그림이 티어에 맞게 다시 걸린다. </summary>
        /// <param name="onTapped">프레임을 탭했을 때 이 문으로 호출된다.</param>
        public void Bind(DoorOption option, Action<WorldDoorView> onTapped)
        {
            Option = option;
            _onTapped = onTapped;
            gameObject.SetActive(true);

            var look = tierLooks[(int)option.Tier];
            frameRenderer.sprite = look.frame;
            if (shadowRenderer != null) shadowRenderer.sprite = look.shadow;
            Vector3 mirror = MirrorLocal(look);
            float diameter = mirrorDiameterPx / look.frame.pixelsPerUnit;

            if (mirrorMask != null)
            {
                mirrorMask.transform.localPosition = mirror;
                ScaleToHeight(mirrorMask.transform, mirrorMask.sprite, diameter);
            }
            FitIcon(option.Icon, option.FlipIcon, mirror, diameter);
            ShowRewardSymbols(option.RewardIcons, mirror, diameter);
            SetTappable(true);
        }

        /// <summary> 문을 내리고 탭 통지를 끊는다. 이미 내려가 있어도 호출 안전하다. </summary>
        public void Clear()
        {
            _onTapped = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 탭을 받을지 정한다. 하나가 확정되면 세워 둔 문 모두 꺼서 중복 선택을 막는다.
        /// 아웃라인이 이 상태를 따라가므로, 선택 대기 중에만 문이 강조된다.
        /// </summary>
        public void SetTappable(bool on)
        {
            if (tapArea != null) tapArea.enabled = on;
            ApplyOutline(on);
        }

        /// <summary> 프레임 아웃라인을 켜고 끈다. 색은 티어가 정한다 — 일반은 녹색, 미드보스·보스는 보라색. </summary>
        private void ApplyOutline(bool on)
        {
            if (frameRenderer == null) return;

            _mpb ??= new MaterialPropertyBlock();
            frameRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(OutlineEnabledId, on ? 1f : 0f);
            if (on)
            {
                _mpb.SetColor(OutlineColorId, Option.Tier == DoorTier.Promise ? PromiseOutline : BossOutline);
                float ppu = frameRenderer.sprite != null ? frameRenderer.sprite.pixelsPerUnit : 100f;
                _mpb.SetFloat(OutlineThicknessId, OutlineWorldThickness * ppu);
                _mpb.SetFloat(OutlineAlphaCutoffId, OutlineAlphaCutoff);
            }
            frameRenderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// EventSystem 클릭 콜백. Collider2D + 카메라의 Physics2DRaycaster로 월드 스프라이트 탭이 전달된다.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData) => _onTapped?.Invoke(this);

        /// <summary> 거울 중심을 아트 픽셀 좌표에서 프레임 로컬 좌표로 바꾼다. </summary>
        private static Vector3 MirrorLocal(TierLook look)
        {
            var sprite = look.frame;
            float ppu = sprite.pixelsPerUnit;
            // 아트 좌표는 좌상단 원점이라 Y를 뒤집는다. 피벗(하단 중앙)만큼은 두 축 모두 빼 준다.
            return new Vector3(
                (look.mirrorCenterPx.x - sprite.pivot.x) / ppu,
                (sprite.rect.height - look.mirrorCenterPx.y - sprite.pivot.y) / ppu,
                0f);
        }

        /// <summary> 그림을 거울 지름에 맞춰 거울 중심에 앉힌다. 비면 렌더러를 끈다. </summary>
        private void FitIcon(Sprite sprite, bool flip, Vector3 mirror, float diameter)
        {
            if (iconRenderer == null) return;

            iconRenderer.sprite = sprite;
            iconRenderer.enabled = sprite != null;
            if (sprite == null) return;

            iconRenderer.flipX = flip;
            float scale = ScaleToHeight(iconRenderer.transform, sprite, diameter);
            var center = sprite.bounds.center;
            // flipX는 피벗 기준 반전이라 중심 보정의 x 부호도 함께 뒤집는다.
            iconRenderer.transform.localPosition = mirror
                + new Vector3((flip ? center.x : -center.x) * scale, -center.y * scale, 0f);
        }

        /// <summary> 걸린 보상 심볼을 거울 안 좌하단·우상단 대각선에 놓는다. 심볼이 없는 자리는 끈다. </summary>
        private void ShowRewardSymbols(IReadOnlyList<Sprite> icons, Vector3 mirror, float diameter)
        {
            if (rewardSymbols == null) return;

            // 심볼이 거울에 안쪽으로 딱 붙는 대각 오프셋. 중심 거리 = 거울 반지름 - 심볼 반지름, 그 값을 축별로 나눈다.
            float offset = (1f - RewardSymbolScale) * 0.5f * diameter / 1.41421356f;
            for (int i = 0; i < rewardSymbols.Length; i++)
            {
                if (rewardSymbols[i] == null) continue;

                var sprite = icons != null && i < icons.Count ? icons[i] : null;
                rewardSymbols[i].sprite = sprite;
                rewardSymbols[i].enabled = sprite != null;
                if (sprite == null) continue;

                ScaleToHeight(rewardSymbols[i].transform, sprite, diameter * RewardSymbolScale);
                int sign = i == 0 ? -1 : 1;
                rewardSymbols[i].transform.localPosition = mirror + new Vector3(sign * offset, sign * offset, 0f);
            }
        }

        /// <summary> 그림을 지정한 로컬 높이로 등비 축소한다. </summary>
        /// <returns>적용한 배율. 위치를 함께 맞출 때 이 값이 필요하다.</returns>
        private static float ScaleToHeight(Transform target, Sprite sprite, float height)
        {
            float source = sprite.bounds.size.y;
            float scale = source > 0f ? height / source : 1f;
            target.localScale = new Vector3(scale, scale, 1f);
            return scale;
        }
    }
}
