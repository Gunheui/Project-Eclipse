using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Eclipse.Data;
using Eclipse.Presentation;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Eclipse.View
{
    /// <summary>
    /// 조준 모드에서 이 배틀러의 대상 상태. View가 유효 타겟과 선택 불가를 시각으로 구분하는 데 쓴다.
    /// None=조준 중 아님(평상시), Selectable=유효 타겟(아웃라인), Ineligible=선택 불가(어둡게).
    /// </summary>
    public enum TargetState { None, Selectable, Ineligible }

    /// <summary>
    /// 전장에 세우는 배틀러 하나. 대응하는 <see cref="CombatantViewModel"/>의 스프라이트를 월드 공간
    /// SpriteRenderer로 그리고, 자기 상태(HP·행동·생존)를 구독해 스스로 연출한다 — 피격 흔들림·플로팅
    /// 숫자·시전 돌진·사망 숨김. 중앙 지휘 없이 각자 자기 VM만 본다.
    /// 조준 모드에서는 몸통 탭으로 대상 선택 입력을 낸다(Bind의 onTapped).
    /// </summary>
    public class BattlerView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private FloatingText floatingTextPrefab;
        [SerializeField] private SpriteEffectPlayer effectPlayerPrefab;

        // 몸통 탭 판정 영역. 배틀러 루트에 두어 연출(흔들림·돌진)로 움직이지 않게 한다.
        [SerializeField] private BoxCollider2D tapArea;

        // 탭 영역을 스프라이트보다 이만큼 넓힌다(월드 단위). 손가락 여유.
        private const float TapAreaPadding = 0.15f;

        // 선택 불가(Ineligible) 대상 스프라이트에 곱하는 색. 채도는 유지하고 밝기만 낮춰 어둡게 보이게 한다.
        private static readonly Color DimColor = new(0.35f, 0.35f, 0.35f, 1f);

        // 유효 타겟 아웃라인. 적 공격 조준=적군 계열 #D06A61, 아군 힐/버프 조준=아군 계열 녹색 #4E9B7A
        // (아군 HP바와 같은 색 — 힐 대상이 공격 대상처럼 보이지 않게). 두께는 월드 단위 — 스프라이트 PPU가
        // 달라도(아군 315·적 100) 화면상 굵기가 같도록 셰이더에 넘길 때 PPU로 환산한다.
        private static readonly Color SelectableOutline = new(0.816f, 0.416f, 0.380f, 1f);
        private static readonly Color AllyOutline = new(0.306f, 0.608f, 0.478f, 1f);
        private const float SelectableOutlineWorldThickness = 0.03f;

        // Eclipse/SpriteOutlineURP2D의 아웃라인 프로퍼티. MaterialPropertyBlock으로 배틀러마다 따로 덮어쓴다.
        private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");

        private readonly CompositeDisposable _bindings = new();

        // 아웃라인 오버라이드 전달용. 첫 사용 때 만들어 재사용한다(머티리얼 인스턴스 복제를 피한다).
        private MaterialPropertyBlock _mpb;

        // 이 배틀러가 표시 중인 유닛과 몸통 탭 통지처. Bind에서 세우고 Clear에서 지워 바인딩과 수명을 맞춘다.
        private CombatantViewModel _unit;
        private Action<CombatantViewModel> _onTapped;

        private Func<int> _speed = () => 1;
        private Vector3 _home;
        private int _prevHp;
        private bool _facingRight;

        // 이번 턴에 진행 중인 연출. 루프가 WaitForAnimation으로 이걸 기다린 뒤 다음 턴으로 넘어간다.
        // Preserve로 감싸 여러 번 await 가능하게 둔다(매 턴 모든 배틀러의 이 값을 다시 기다리므로).
        private UniTask _animation = UniTask.CompletedTask;

        /// <summary>
        /// 이 배틀러를 한 유닛 VM에 연결한다. 이전 구독을 정리하고 스프라이트를 세팅한 뒤,
        /// HP 변화(피격·힐)·행동(시전)·생존을 구독해 스스로 연출하도록 만든다.
        /// </summary>
        /// <param name="unit">이 배틀러가 표시할 유닛 VM.</param>
        /// <param name="speed">현재 연출 배속(1 또는 2)을 읽는 함수. 트윈 시간을 나눈다.</param>
        /// <param name="onTapped">몸통을 탭했을 때 이 유닛으로 호출된다. null이면 탭이 무시된다.</param>
        public void Bind(CombatantViewModel unit, Func<int> speed, Action<CombatantViewModel> onTapped = null)
        {
            _bindings.Clear();
            gameObject.SetActive(true);
            _unit = unit;
            _onTapped = onTapped;
            _speed = speed ?? (() => 1);
            if (visualRoot == null) visualRoot = transform;
            _home = visualRoot.localPosition;
            _facingRight = unit.IsAlly;
            _prevHp = unit.CurrentHp.CurrentValue;
            if (spriteRenderer != null) spriteRenderer.sprite = unit.BattlerSprite;
            SetTargetState(TargetState.None); // 평상시 밝기로 초기화(재바인딩 시 이전 dim 잔상 제거)
            ResizeTapArea();

            // HP가 줄면 피격(흔들림+숫자), 늘면 힐(숫자). 구독 즉시 오는 첫 값은 변화 0이라 무시된다.
            unit.CurrentHp
                .Subscribe(OnHpChanged)
                .AddTo(_bindings);

            // 이 유닛이 행동하면 시전 돌진 + 시전 이펙트.
            unit.Acted
                .Subscribe(skill => AddAnimation(PlayCastAsync(skill)))
                .AddTo(_bindings);

            // 이 유닛이 스킬 대상이 되면 피격 이펙트(흔들림·숫자는 HP 변화가 따로 처리).
            unit.Hit
                .Subscribe(skill => AddAnimation(SpawnEffect(skill != null ? skill.impactEffect : null)))
                .AddTo(_bindings);

            // 사망 시 렌더러를 끈다.
            unit.IsAlive
                .Subscribe(alive => { if (spriteRenderer != null) spriteRenderer.enabled = alive; })
                .AddTo(_bindings);
        }

        /// <summary> 이번 턴 진행 중인 연출이 끝나면 완료된다. 진행 중인 게 없으면 즉시 완료. </summary>
        public UniTask WaitForAnimation() => _animation;

        /// <summary>대응 유닛이 없는 빈 배틀러를 숨기고 탭 통지를 끊는다.</summary>
        public void Clear()
        {
            _bindings.Clear();
            _unit = null;
            _onTapped = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// EventSystem 클릭 콜백. Collider2D + 카메라의 Physics2DRaycaster로 월드 스프라이트 탭이 전달된다.
        /// 바인딩된 유닛을 그대로 통지처에 넘기며, 조준 중인지·유효 대상인지 판단은 BattleView가 한다.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData) => _onTapped?.Invoke(_unit);

        /// <summary>
        /// 조준 모드에서 이 배틀러의 대상 상태를 시각으로 반영한다. 유효 타겟(Selectable)은 아웃라인을 켜고,
        /// 선택 불가(Ineligible)는 스프라이트를 어둡게 한다. None이면 평상시(밝기 원복·아웃라인 off)로 되돌린다.
        /// </summary>
        /// <param name="state">이 배틀러의 대상 상태(None/Selectable/Ineligible).</param>
        /// <param name="allyTarget">유효 타겟(Selectable) 아웃라인을 아군색(녹색)으로 칠할지. false면 적색.</param>
        public void SetTargetState(TargetState state, bool allyTarget = false)
        {
            if (spriteRenderer == null) return;
            spriteRenderer.color = state == TargetState.Ineligible ? DimColor : Color.white;
            ApplyOutline(state == TargetState.Selectable, allyTarget);
        }

        // 탭 영역을 방금 바인딩한 스프라이트 크기에 맞춘다. 스프라이트는 런타임에 정해지고 유닛마다
        // 크기·PPU가 달라 에디터에서 미리 맞출 수 없다. 렌더러의 월드 바운드를 배틀러 루트 로컬로 환산해 쓴다.
        private void ResizeTapArea()
        {
            if (tapArea == null || spriteRenderer == null || spriteRenderer.sprite == null) return;

            var bounds = spriteRenderer.bounds; // 실제 그려지는 월드 AABB(Visual의 오프셋·스케일이 반영됨)
            var scale = transform.lossyScale;
            var size = new Vector2(
                bounds.size.x / Mathf.Max(Mathf.Abs(scale.x), 1e-4f),
                bounds.size.y / Mathf.Max(Mathf.Abs(scale.y), 1e-4f));

            tapArea.size = size + new Vector2(TapAreaPadding * 2f, TapAreaPadding * 2f);
            tapArea.offset = transform.InverseTransformPoint(bounds.center);
        }

        // 아웃라인을 이 배틀러에만 켠다. 머티리얼이 Eclipse/SpriteOutlineURP2D가 아니면 이 프로퍼티들은 무시된다.
        private void ApplyOutline(bool on, bool allyTarget)
        {
            _mpb ??= new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(OutlineEnabledId, on ? 1f : 0f);
            if (on)
            {
                _mpb.SetColor(OutlineColorId, allyTarget ? AllyOutline : SelectableOutline);
                // 셰이더 두께 단위는 소스 텍셀이므로 월드 두께에 PPU를 곱해 환산한다.
                float ppu = spriteRenderer.sprite != null ? spriteRenderer.sprite.pixelsPerUnit : 100f;
                _mpb.SetFloat(OutlineThicknessId, SelectableOutlineWorldThickness * ppu);
            }
            spriteRenderer.SetPropertyBlock(_mpb);
        }

        private void OnHpChanged(int hp)
        {
            int delta = hp - _prevHp;
            _prevHp = hp;
            if (delta < 0) AddAnimation(PlayHitAsync(-delta));
            else if (delta > 0) SpawnFloatingText(delta, isHeal: true);
        }

        // 이번 턴의 하위 연출을 합류시킨다. 한 배틀러가 같은 턴에 여러 신호(시전·피격·HP변화)를 받아도
        // 마지막 것이 앞선 것을 덮지 않도록 WhenAll로 묶는다. 직전 턴 연출은 루프가 이미 기다려 완료됐으므로
        // 완료 상태면 새로 시작하고, 진행 중(같은 턴)이면 합친다.
        private void AddAnimation(UniTask next)
        {
            _animation = _animation.Status.IsCompleted()
                ? next.Preserve()
                : UniTask.WhenAll(_animation, next).Preserve();
        }

        // 피격: 데미지 숫자를 띄우고 짧게 흔들린다. 반환 태스크는 흔들림이 끝나면 완료된다.
        private UniTask PlayHitAsync(int amount)
        {
            SpawnFloatingText(amount, isHeal: false);
            float dur = 0.3f / _speed();
            return visualRoot
                .DOShakePosition(dur, strength: 0.25f, vibrato: 18, randomness: 90, snapping: false, fadeOut: true)
                .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        // 시전: 대면 방향 돌진 + (있으면) 시전 이펙트를 함께 재생한다. 둘 다 끝나면 완료된다.
        private UniTask PlayCastAsync(SkillSO skill)
        {
            var lunge = PlayLungeAsync();
            var effect = SpawnEffect(skill != null ? skill.castEffect : null);
            return UniTask.WhenAll(lunge, effect);
        }

        // 대면 방향으로 살짝 돌진했다 제자리로. 반환 태스크는 복귀가 끝나면 완료된다.
        private UniTask PlayLungeAsync()
        {
            float dur = 0.25f / _speed();
            float lunge = _facingRight ? 0.5f : -0.5f;
            return DOTween.Sequence()
                .Append(visualRoot.DOLocalMoveX(_home.x + lunge, dur * 0.4f).SetEase(Ease.OutQuad))
                .Append(visualRoot.DOLocalMoveX(_home.x, dur * 0.6f).SetEase(Ease.InQuad))
                .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        // 연출은 배틀러 자신이 아니라 부모 앵커 밑에 스폰한다(사망으로 배틀러가 숨어도 연출은 남아야 하므로).
        // 그 앵커가 파괴되는 중이면 스폰할 수 없다 — 씬 종료·사망 정리 도중 뒤늦게 도착한 신호가 여기로 온다.
        // 스폰 가능한 부모를 주고, 불가능하면 null.
        private Transform SpawnParentOrNull()
        {
            var parent = transform.parent;
            if (parent == null || parent.gameObject.IsDestroying()) return null;
            return gameObject.IsDestroying() ? null : parent;
        }

        // 이펙트 스펙을 이 배틀러 위치에 스폰해 재생한다. 스펙·프리팹이 없으면 즉시 완료.
        private UniTask SpawnEffect(EffectSpec spec)
        {
            if (spec == null || effectPlayerPrefab == null) return UniTask.CompletedTask;
            var parent = SpawnParentOrNull();
            if (parent == null) return UniTask.CompletedTask;
            var player = Instantiate(effectPlayerPrefab, visualRoot.position, Quaternion.identity, parent);
            return player.Play(spec, _speed(), this.GetCancellationTokenOnDestroy());
        }

        private void SpawnFloatingText(int amount, bool isHeal)
        {
            if (floatingTextPrefab == null) return;
            var parent = SpawnParentOrNull();
            if (parent == null) return;
            var ft = Instantiate(floatingTextPrefab, visualRoot.position, Quaternion.identity, parent);
            ft.Show(amount, isHeal, _speed());
        }

        private void OnDestroy() => _bindings.Dispose();
    }
}