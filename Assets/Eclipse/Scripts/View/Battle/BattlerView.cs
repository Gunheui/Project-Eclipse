using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Presentation;
using Eclipse.View.Theme;
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
    /// 전장에 세우는 배틀러 하나. 유닛 VM의 스프라이트를 월드 SpriteRenderer로 그리고,
    /// 자기 상태(HP·행동·생존)를 구독해 스스로 연출한다(피격 흔들림·플로팅 숫자·시전 모션·사망 숨김).
    /// 조준 모드에서는 몸통 탭으로 대상 선택 입력을 보내고(Bind의 onTapped),
    /// 그 외에는 마우스를 올리거나 꾹 누르는 동안 상세 표시를 요청한다(Bind의 onHovered).
    /// </summary>
    public class BattlerView : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler
    {
        [SerializeField] private UIThemeSO theme;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private FloatingText floatingTextPrefab;
        [SerializeField] private SpriteEffectPlayer effectPlayerPrefab;
        [SerializeField] private VfxPlayer vfxPlayerPrefab;

        // 몸통 탭 판정 영역. 배틀러 루트에 두어 연출(흔들림·돌진)로 움직이지 않게 한다.
        [SerializeField] private BoxCollider2D tapArea;

        // 탭 영역을 스프라이트보다 이만큼 넓힌다(월드 단위). 손가락 여유.
        private const float TapAreaPadding = 0.15f;

        // 탭 판정의 최대 가로 폭(월드 단위). 아군 초상은 정규화 규격이라 망토·무기까지 폭 3에 가까운데
        // 앞뒤 줄 자리 간격은 1.5뿐이라, 그대로 두면 앞줄이 뒷줄의 몸통 탭을 가로챈다.
        // 자리 간격보다 좁게 잘라 서로의 중심을 덮지 않게 한다. 이 값보다 좁은 스프라이트는 그대로 둔다.
        [SerializeField] private float tapAreaMaxWidth = 1.4f;

        // 선택 불가(Ineligible) 대상 스프라이트에 곱하는 색. 채도는 유지하고 밝기만 낮춰 어둡게 보이게 한다.
        private static readonly Color DimColor = new(0.35f, 0.35f, 0.35f, 1f);

        // HP바 앵커 배치값(로컬 단위). 여백은 머리와 바 사이 간격이고,
        // 상한은 장신 적이 바를 화면 위로 밀어올리지 못하게 막는다.
        private const float HeadAnchorMargin = 0.3f;
        private const float HeadAnchorMaxY = 4f;

        // 유효 타겟 아웃라인 두께. 월드 단위로 정의한다 — 스프라이트 PPU가 달라도(아군 315·적 100)
        // 화면상 굵기가 같도록 셰이더에 넘길 때 PPU로 환산한다.
        private const float SelectableOutlineWorldThickness = 0.03f;

        // 한 대상에게 숫자가 연달아 뜰 때의 간격(초). 앞 숫자가 상승하는 도중 다음 숫자가 뜬다.
        private const float NumberInterval = 0.35f;

        // 근접 접근·복귀 대시 시간(초)과 대상 앞에서 멈추는 간격(월드 단위).
        // 자리 조합에 따라 거리가 6~17유닛으로 갈려 시간을 고정하고 감속으로 붙인다.
        private const float ApproachDuration = 0.2f;
        private const float ReturnDuration = 0.15f;
        private const float MeleeGap = 1.2f;

        // Eclipse/SpriteOutlineURP2D의 아웃라인 프로퍼티. MaterialPropertyBlock으로 배틀러마다 따로 덮어쓴다.
        private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");

        // 공용 AnimatorController의 상태. 유닛별 오버라이드는 클립만 갈아끼우므로 이름이 공통이다.
        private static readonly int IdleStateId = Animator.StringToHash("Idle");
        private static readonly int AttackStateId = Animator.StringToHash("Attack");

        private readonly CompositeDisposable _bindings = new();

        // 아웃라인 오버라이드 전달용. 첫 사용 때 만들어 재사용한다(머티리얼 인스턴스 복제를 피한다).
        private MaterialPropertyBlock _mpb;

        // 이 배틀러가 표시 중인 유닛과 탭·호버 통지처. Bind에서 세우고 Clear에서 지워 바인딩과 수명을 맞춘다.
        private CombatantViewModel _unit;
        private Action<CombatantViewModel> _onTapped;
        private Action<CombatantViewModel, bool> _onHovered;

        // 이 배틀러의 평상시 스프라이트 색. 변이가 있으면 그 틴트, 없으면 흰색이다.
        private Color _baseColor = Color.white;

        private Func<int> _speed = () => 1;
        private Func<bool, Bounds> _formationBounds;
        private Func<CombatantViewModel, Vector3?> _battlerPosition;
        private Vector3 _home;

        // 이 유닛의 공격 클립에서 무기가 닿는 시점(초). 배속은 재생할 때 나눈다.
        private float _impactTime;

        // 이번 시전의 타격 알림. 시전마다 새로 만들고 알린 뒤 비운다 —
        // 남겨 두면 다음 턴이 옛 알림을 그대로 물려받는다.
        private UniTaskCompletionSource _impact;

        // 발밑·몸통 앵커의 이 트랜스폼 기준 위치. Bind에서 실루엣 아래끝과 중심으로 잡는다.
        private float _groundLocalY;
        private Vector3 _bodyCenterLocal;

        // HP바가 매달린 HeadAnchor와 씬에 저작된 기본 높이. 슬롯마다 있는 자식이라 첫 사용 때 이름으로 찾는다.
        private Transform _headAnchor;
        private float _anchorBaseY;

        // 이번 턴에 진행 중인 연출. 루프가 WaitForAnimation으로 이걸 기다린 뒤 다음 턴으로 넘어간다.
        // Preserve로 감싸 여러 번 await 가능하게 둔다(매 턴 모든 배틀러의 이 값을 다시 기다리므로).
        private UniTask _animation = UniTask.CompletedTask;

        // 아직 재생하지 못한 표시. 같은 턴에 여러 번 맞거나 틱이 겹쳐도 겹치지 않고 하나씩 나간다.
        private readonly Queue<(EffectDisplay Result, SkillSO Skill, bool Shake)> _displayQueue = new();
        private bool _draining;

        // 이번 바인딩이 소유한 재생 수명. 다시 바인딩하거나 비울 때 끊어, 옛 재생이 새 바인딩의
        // 대기열과 플래그를 함께 건드리지 못하게 한다.
        private CancellationTokenSource _playbackCts;

        // 유지 중인 파티클 재생기와 그것을 띄운 스킬. 그 스킬이 건 효과가 다 풀리면 함께 걷는다.
        private readonly List<(VfxPlayer Player, SkillSO Source)> _heldVfx = new();

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform;
            // 제자리는 씬에 저작된 값 하나뿐이다. 바인딩마다 다시 읽으면 연출 도중 재바인딩됐을 때
            // 어긋난 위치가 제자리로 굳어 이후 흔들림이 그 자리로 돌아간다.
            _home = visualRoot.localPosition;
        }

        // 배속 토글은 View가 들고 있고 알림은 없다. 매 프레임 읽어 애니메이터 전체 속도에 반영한다.
        private void Update()
        {
            if (animator != null) animator.speed = _speed();
        }

        /// <summary>
        /// 이 배틀러를 한 유닛 VM에 연결한다. 이전 구독을 정리하고,
        /// 피격·틱·행동(시전)·생존을 구독해 스스로 연출하게 한다.
        /// </summary>
        /// <param name="speed">현재 연출 배속(1 또는 2)을 읽는 함수. 트윈 시간을 나눈다.</param>
        /// <param name="formationBounds">
        /// 진영(아군이면 true) 배틀러 전체를 두른 범위를 읽는 함수. 진영 앵커 이펙트가 이 중심에 놓인다.
        /// </param>
        /// <param name="battlerPosition">
        /// 유닛이 서 있는 자리를 읽는 함수. 근접 시전자가 접근 목적지로 쓴다. 대응 배틀러가 없으면 null을 돌려준다.
        /// </param>
        /// <param name="onTapped">몸통을 탭했을 때 이 유닛으로 호출된다. null이면 탭이 무시된다.</param>
        /// <param name="onHovered">포인터가 올라오거나(true) 벗어날 때(false) 호출된다. 상세 표시용.</param>
        public void Bind(CombatantViewModel unit, Func<int> speed, Func<bool, Bounds> formationBounds,
            Func<CombatantViewModel, Vector3?> battlerPosition = null,
            Action<CombatantViewModel> onTapped = null, Action<CombatantViewModel, bool> onHovered = null)
        {
            ResetPlayback();
            _bindings.Clear();
            HideDetail();
            // 파괴 토큰과 묶어 씬이 사라질 때도 함께 끊긴다.
            _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            gameObject.SetActive(true);
            _unit = unit;
            _onTapped = onTapped;
            _onHovered = onHovered;
            _speed = speed ?? (() => 1);
            _formationBounds = formationBounds;
            _battlerPosition = battlerPosition;
            _impactTime = unit.BattlerImpactTime;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = unit.BattlerSprite;
                // 애니 원화는 아군이 오른쪽, 적이 왼쪽을 향해 이미 서로 마주 본다. 뒤집으면 등지고 싸운다.
                spriteRenderer.flipX = false;
            }
            BindAnimator(unit.BattlerAnimator);
            _baseColor = unit.Tint;
            SetTargetState(TargetState.None); // 평상시 밝기로 초기화(재바인딩 시 이전 dim 잔상 제거)
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                var body = SilhouetteLocalBounds();
                ResizeTapArea(body);
                PositionHeadAnchor(body);
                _groundLocalY = body.min.y;
                _bodyCenterLocal = body.center;
            }

            unit.Acted
                .Subscribe(cast => AddAnimation(PlayCastAsync(cast.Skill, cast.Targets, _playbackCts)))
                .AddTo(_bindings);

            // 흔들림은 맞은 반응이라 피해에만 붙인다. 버프·실드를 받을 때는 흔들리지 않는다.
            unit.Hit
                .Subscribe(h => AddAnimation(QueueDisplay(h.Result, h.Skill,
                    shake: h.Result.Type == EffectType.Damage)))
                .AddTo(_bindings);

            unit.Ticked
                .Subscribe(tick => AddAnimation(QueueDisplay(tick, null, shake: false)))
                .AddTo(_bindings);

            // 유지 이펙트 정리는 여기 한 곳에서만 한다. 스폰 시점에 대조하면 시전·피격 신호가 상태 갱신보다
            // 먼저 흘러, 갓 띄운 이펙트가 직전 턴 집합에 없다는 이유로 그 자리에서 걷힌다.
            unit.EffectSources
                .Subscribe(CullHeldVfx)
                .AddTo(_bindings);

            unit.TurnStarted
                .Subscribe(_ => FlashHeldVfx())
                .AddTo(_bindings);

            unit.IsAlive
                .Subscribe(SetAlive)
                .AddTo(_bindings);
        }

        /// <summary>
        /// 이번 유닛의 오버라이드 컨트롤러를 애니메이터에 꽂고 대기 모션을 돌리기 시작한다.
        /// </summary>
        /// <param name="controller">유닛별 AnimatorOverrideController. null이면 정지 그림만 남는다.</param>
        private void BindAnimator(RuntimeAnimatorController controller)
        {
            if (animator == null) return;

            animator.runtimeAnimatorController = controller;
            if (controller == null) return;

            // 한 방에 같은 적이 여러 마리 서면 대기 호흡이 한 몸처럼 맞아 떨어진다. 시작 위치를 흩어 어긋나게 둔다.
            animator.Play(IdleStateId, 0, UnityEngine.Random.value);
        }

        /// <summary> 이번 턴 진행 중인 연출이 끝나면 완료된다. 진행 중인 게 없으면 즉시 완료. </summary>
        public UniTask WaitForAnimation() => _animation;

        /// <summary> 이 배틀러가 때리는 순간이 오면 완료된다. 시전 중이 아니면 즉시 완료. </summary>
        public UniTask WaitForImpact() => _impact?.Task ?? UniTask.CompletedTask;

        /// <summary>타격을 기다리는 쪽을 풀어 준다. 알릴 것이 없어도 호출 안전(멱등).</summary>
        /// <param name="impact">
        /// 종결할 알림. 끊긴 옛 재생이 한 박자 늦게 깨어나도 자기 것만 건드려, 그사이 시작된 시전의
        /// 알림을 대신 끝내지 않는다.
        /// </param>
        private void CompleteImpact(UniTaskCompletionSource impact)
        {
            if (impact == null) return;
            if (_impact == impact) _impact = null;
            impact.TrySetResult();
        }

        /// <summary>대응 유닛이 없는 빈 배틀러를 숨기고 탭 통지를 끊는다.</summary>
        public void Clear()
        {
            ResetPlayback();
            _bindings.Clear();
            HideDetail();
            _unit = null;
            _onTapped = null;
            _onHovered = null;
            gameObject.SetActive(false);
        }

        /// <summary>진행 중인 재생을 끊고 배틀러를 평상 상태로 되돌린다. 호출 안전(멱등).</summary>
        private void ResetPlayback()
        {
            // 취소는 콜백을 그 자리에서 실행해 대기 중이던 재생을 깨운다. 소유자를 먼저 비워야
            // 깨어난 옛 재생이 자기 차례가 아님을 보고 물러난다.
            var previous = _playbackCts;
            _playbackCts = null;
            previous?.Cancel();
            previous?.Dispose();

            // 끊긴 재생도 타격은 알리고 끝낸다. 알릴 주체가 사라지면 턴 루프가 그 대기에서 영영 선다.
            CompleteImpact(_impact);

            // 취소된 트윈은 중간 위치에서 멈춘다. 다음 바인딩이 그 자리를 쓰지 않게 제자리로 되돌린다.
            if (visualRoot != null) visualRoot.localPosition = _home;
            // 공격 도중 끊기면 그 포즈에서 멈춘다. 다음 유닛이 옛 중간 포즈를 물려받지 않게 대기로 되돌린다.
            // 숨은 배틀러에 재생을 걸면 경고만 나고 먹지 않는다. 그쪽은 다시 켤 때 Bind가 대기부터 세운다.
            if (animator != null && animator.runtimeAnimatorController != null && animator.gameObject.activeInHierarchy)
                animator.Play(IdleStateId, 0, 0f);
            _displayQueue.Clear();
            _draining = false;
            // 남겨 두면 다음 방의 첫 턴이 옛 대기를 그대로 물려받는다.
            _animation = UniTask.CompletedTask;
            ClearHeldVfx();
        }

        /// <summary>
        /// EventSystem 클릭 콜백. Collider2D + 카메라의 Physics2DRaycaster로 월드 스프라이트 탭이 전달된다.
        /// 같은 경로로 호버·프레스 콜백도 함께 들어온다.
        /// 바인딩된 유닛을 그대로 통지처에 넘기며, 조준 중인지·유효 대상인지 판단은 BattleView가 한다.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData) => _onTapped?.Invoke(_unit);

        /// <summary>포인터가 올라오면 바로 상세를 띄운다(스킬 툴팁과 같은 방식).</summary>
        public void OnPointerEnter(PointerEventData eventData) => ShowDetail();

        public void OnPointerExit(PointerEventData eventData) => HideDetail();

        /// <summary>손을 떼면 내린다. 터치는 뗀 자리에서 포인터가 사라져 나감 통지가 늦을 수 있다.</summary>
        public void OnPointerUp(PointerEventData eventData) => HideDetail();

        private void ShowDetail()
        {
            if (_unit != null) _onHovered?.Invoke(_unit, true);
        }

        /// <summary>상세를 내린다. 켜져 있지 않아도 호출 안전(멱등).</summary>
        private void HideDetail()
        {
            if (_unit != null) _onHovered?.Invoke(_unit, false);
        }

        /// <summary>
        /// 사망한 배틀러를 숨기고 탭 판정도 함께 끊는다. 연출은 부모 앵커 밑에 스폰되므로 그대로 남는다.
        /// </summary>
        private void SetAlive(bool alive)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = alive;
            if (tapArea != null) tapArea.enabled = alive;
            // 탭 판정을 끄면 포인터가 벗어나는 이벤트가 오지 않는다. 상세가 떠 있으면 여기서 직접 내린다.
            if (!alive)
            {
                HideDetail();
                ClearHeldVfx(); // 죽은 유닛에 오라가 남지 않게 한다
            }
        }

        /// <summary>유지 중인 파티클 이펙트를 모두 걷는다. 호출 안전(멱등).</summary>
        private void ClearHeldVfx()
        {
            foreach (var (player, _) in _heldVfx)
                if (player != null) player.StopHold();
            _heldVfx.Clear();
        }

        /// <summary>출처 스킬의 효과가 하나도 남지 않은 유지 이펙트를 걷는다. 유닛의 턴 정산마다 호출된다.</summary>
        /// <param name="liveSources">지금 이 유닛에 걸려 있는 효과들의 출처 스킬.</param>
        private void CullHeldVfx(IReadOnlyCollection<SkillSO> liveSources)
            => StopHeldVfx(source => !liveSources.Contains(source));

        /// <summary>조건에 맞는 유지 이펙트를 걷는다. 이미 파괴된 재생기는 조건과 무관하게 함께 뺀다.</summary>
        private void StopHeldVfx(Func<SkillSO, bool> match)
        {
            for (int i = _heldVfx.Count - 1; i >= 0; i--)
            {
                var (player, source) = _heldVfx[i];
                if (player != null && !match(source)) continue;
                if (player != null) player.StopHold();
                _heldVfx.RemoveAt(i);
            }
        }

        /// <summary>「턴마다」 방식의 유지 이펙트를 한 번씩 다시 터뜨린다. 이 유닛의 턴 시작 정산마다 호출된다.</summary>
        private void FlashHeldVfx()
        {
            foreach (var (player, _) in _heldVfx)
                if (player != null) player.FlashEachTurn();
        }

        /// <summary>
        /// 조준 모드의 대상 상태를 시각으로 반영한다. Selectable=아웃라인, Ineligible=어둡게, None=평상시 원복.
        /// </summary>
        /// <param name="allyTarget">아웃라인을 아군색(녹색)으로 칠할지. false면 적색.</param>
        public void SetTargetState(TargetState state, bool allyTarget = false)
        {
            if (spriteRenderer == null) return;
            // 변이 틴트 위에 dim을 곱한다 — 대입하면 변이색이 사라지고 조준이 끝난 뒤에도 흰색으로 남는다.
            spriteRenderer.color = state == TargetState.Ineligible ? _baseColor * DimColor : _baseColor;
            ApplyOutline(state == TargetState.Selectable, allyTarget);
        }

        /// <summary>
        /// 그림이 실제로 차지하는 범위를 이 트랜스폼 기준 사각형으로 반환한다.
        /// </summary>
        private Bounds SilhouetteLocalBounds()
        {
            // SpriteRenderer.bounds는 투명 여백까지 포함한 원본 칸 전체를 돌려준다. 적 배틀러는 칸을
            // 꽉 채운 규격이라 그 값을 쓰면 탭 판정과 HP바가 그림보다 한참 크게 잡힌다.
            // 타이트 메시의 정점이 여백을 뺀 실제 외곽이다.
            var verts = spriteRenderer.sprite.vertices;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var v in verts)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }

            // flip은 트랜스폼이 아니라 메시를 피벗 기준으로 뒤집으므로 정점 좌표에 직접 반영한다.
            if (spriteRenderer.flipX) (min.x, max.x) = (-max.x, -min.x);
            if (spriteRenderer.flipY) (min.y, max.y) = (-max.y, -min.y);

            var visual = spriteRenderer.transform;
            var a = transform.InverseTransformPoint(visual.TransformPoint(min));
            var b = transform.InverseTransformPoint(visual.TransformPoint(max));
            var bounds = new Bounds();
            bounds.SetMinMax(Vector3.Min(a, b), Vector3.Max(a, b));
            return bounds;
        }

        /// <summary>
        /// 탭 영역을 그림이 차지하는 범위에 맞춘다. 유닛마다 크기가 달라 에디터에서 미리 맞출 수 없다.
        /// </summary>
        /// <param name="body">이 트랜스폼 기준 실루엣 범위.</param>
        private void ResizeTapArea(Bounds body)
        {
            if (tapArea == null) return;

            var size = (Vector2)body.size + new Vector2(TapAreaPadding * 2f, TapAreaPadding * 2f);
            if (tapAreaMaxWidth > 0f) size.x = Mathf.Min(size.x, tapAreaMaxWidth);

            tapArea.size = size;
            tapArea.offset = body.center;
        }

        /// <summary>
        /// HP바 앵커를 머리 위로 올린다. 씬에 저작된 높이가 하한이라 그보다 작은 유닛의 바는 그 높이에 머문다.
        /// </summary>
        /// <param name="body">이 트랜스폼 기준 실루엣 범위.</param>
        private void PositionHeadAnchor(Bounds body)
        {
            if (_headAnchor == null)
            {
                _headAnchor = transform.Find("HeadAnchor");
                if (_headAnchor == null) return;
                _anchorBaseY = _headAnchor.localPosition.y;
            }

            var pos = _headAnchor.localPosition;
            pos.y = Mathf.Clamp(body.max.y + HeadAnchorMargin, _anchorBaseY, HeadAnchorMaxY);
            _headAnchor.localPosition = pos;
        }

        /// <summary>
        /// 아웃라인을 이 배틀러에만 켠다. 머티리얼이 Eclipse/SpriteOutlineURP2D가 아니면 이 프로퍼티들은 무시된다.
        /// </summary>
        private void ApplyOutline(bool on, bool allyTarget)
        {
            _mpb ??= new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(OutlineEnabledId, on ? 1f : 0f);
            if (on)
            {
                _mpb.SetColor(OutlineColorId, allyTarget ? theme.battleAlly : theme.battleEnemy);
                // 셰이더 두께 단위는 소스 텍셀이므로 월드 두께에 PPU를 곱해 환산한다.
                float ppu = spriteRenderer.sprite != null ? spriteRenderer.sprite.pixelsPerUnit : 100f;
                _mpb.SetFloat(OutlineThicknessId, SelectableOutlineWorldThickness * ppu);
            }
            spriteRenderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 이번 턴의 하위 연출을 합류시킨다. 같은 턴에 여러 신호(시전·피격·틱)를 받아도
        /// 마지막 것이 앞선 것을 덮지 않는다.
        /// </summary>
        private void AddAnimation(UniTask next)
        {
            // 완료 상태면 새 턴이므로 새로 시작하고, 진행 중이면 WhenAll로 묶는다.
            _animation = _animation.Status.IsCompleted()
                ? next.Preserve()
                : UniTask.WhenAll(_animation, next).Preserve();
        }

        /// <summary>
        /// 효과 결과 하나를 대기열에 넣는다. 한 턴에 여러 번 맞거나 틱이 겹쳐도 겹치지 않고 하나씩 나가,
        /// 멀티히트 타수와 도트 개수가 화면에 보인다. 한 턴의 신호는 동기로 연달아 들어온다.
        /// </summary>
        /// <param name="skill">이 결과를 낸 스킬. 피격 이펙트를 꺼내고 유지 이펙트의 출처로 쓴다. 틱처럼 없으면 null.</param>
        /// <param name="shake">이 결과에 몸통이 반응할지.</param>
        /// <returns>
        /// 대기열을 비우는 재생. 같은 턴의 두 번째부터는 앞선 재생이 끝까지 비우므로 완료된 값이다.
        /// </returns>
        private UniTask QueueDisplay(EffectDisplay result, SkillSO skill, bool shake)
        {
            _displayQueue.Enqueue((result, skill, shake));
            return _draining ? UniTask.CompletedTask : DrainQueueAsync(_playbackCts);
        }

        /// <summary>대기열이 빌 때까지 하나씩 재생한다. 재바인딩으로 차례를 잃으면 중간에 멈춘다.</summary>
        /// <param name="owner">이 재생을 시작한 바인딩의 취소 소스. 현재 것과 다르면 물러난다.</param>
        private async UniTask DrainQueueAsync(CancellationTokenSource owner)
        {
            var ct = owner.Token; // 폐기된 뒤에는 못 읽으므로 시작할 때 받아 둔다
            _draining = true;
            try
            {
                while (owner == _playbackCts && _displayQueue.Count > 0)
                {
                    var (result, skill, shake) = _displayQueue.Dequeue();
                    await PlayResultAsync(result, skill, shake, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // 재바인딩으로 끊긴 재생. 대기만 풀리면 되므로 알릴 것이 없다.
            }
            finally
            {
                // 새 바인딩이 이미 자기 재생을 돌리고 있으면 그쪽 플래그를 대신 내리면 안 된다.
                if (owner == _playbackCts) _draining = false;
            }
        }

        /// <summary>
        /// 효과 결과 하나를 재생한다. 크기가 0인 결과(버프·도발, 최대 HP에서 걸린 리젠)는
        /// 숫자 없이 이펙트만 나간다.
        /// </summary>
        /// <returns>숫자·흔들림·타격 이펙트가 모두 끝나면 완료된다.</returns>
        private async UniTask PlayResultAsync(EffectDisplay result, SkillSO skill, bool shake, CancellationToken ct)
        {
            // 이펙트와 흔들림은 숫자와 나란히 돈다.
            var reaction = UniTask.WhenAll(
                SpawnEffect(skill != null ? skill.impactEffect : null),
                SpawnVfx(skill != null ? skill.impactVfx : null, skill),
                shake ? PlayShakeAsync(ct) : UniTask.CompletedTask);

            if (result.Amount > 0) await SpawnAmountAsync(result, ct);

            await reaction;
        }

        /// <summary>숫자 하나를 띄우고 다음 숫자를 위한 간격을 둔다.</summary>
        /// <returns>다음 숫자를 띄워도 될 때 완료된다.</returns>
        private UniTask SpawnAmountAsync(EffectDisplay result, CancellationToken ct)
        {
            bool heal = result.Type is EffectType.Heal or EffectType.Regen;
            var ft = SpawnFloatingTextOrNull();
            if (ft != null)
                ft.Show((heal ? "+" : "-") + result.Amount, NumberColor(result), _speed(), result.IsCrit);
            return UniTask.Delay(TimeSpan.FromSeconds(NumberInterval / _speed()), cancellationToken: ct);
        }

        /// <summary>숫자 색. 실드가 막아 낸 피해는 종류와 무관하게 실드색으로 알린다.</summary>
        private Color NumberColor(EffectDisplay result)
        {
            if (result.Shielded) return theme.battleShield;
            return result.Type switch
            {
                EffectType.Heal => theme.battleHeal,
                EffectType.Dot => theme.battleDot,
                EffectType.Regen => theme.battleRegen,
                _ => theme.battleDamage,
            };
        }

        /// <summary>피격 반응으로 짧게 흔들린다.</summary>
        /// <returns>흔들림이 끝나면 완료된다. 취소되면 트윈만 죽고 대기는 예외 없이 완료된다.</returns>
        private UniTask PlayShakeAsync(CancellationToken ct)
        {
            float dur = 0.3f / _speed();
            return visualRoot
                .DOShakePosition(dur, strength: 0.25f, vibrato: 18, randomness: 90, snapping: false, fadeOut: true)
                .ToUniTask(cancellationToken: ct);
        }

        /// <summary>시전: 접근·공격 모션·복귀를 차례로 진행하고 시전 이펙트를 나란히 재생한다.</summary>
        /// <param name="targets">이번 턴에 맞는 대상들. 근접 스킬이면 여기서 접근 목적지를 구한다.</param>
        /// <param name="owner">이 재생을 시작한 바인딩의 취소 소스. 현재 것과 다르면 물러난다.</param>
        /// <returns>이동·모션·이펙트가 모두 끝나면 완료된다.</returns>
        private async UniTask PlayCastAsync(SkillSO skill, IReadOnlyList<CombatantViewModel> targets,
            CancellationTokenSource owner)
        {
            var ct = owner.Token;
            // 첫 대기 전에 세운다. 전장이 시전 신호 바로 뒤에 이 값을 읽는다.
            var impact = _impact = new UniTaskCompletionSource();

            // 이펙트는 이동·모션과 나란히 간다. 순차로 돌리면 유지 오라 등록이 접근 시간만큼 늦는다.
            var effects = UniTask.WhenAll(
                SpawnEffect(skill != null ? skill.castEffect : null),
                SpawnVfx(skill != null ? skill.castVfx : null, skill));

            bool melee = skill != null && skill.melee && targets != null && targets.Count > 0;
            try
            {
                if (melee) await MoveAsync(ApproachDestination(targets), ApproachDuration, ct);
                if (owner == _playbackCts) await PlayAttackAsync(impact, ct);
                if (melee && owner == _playbackCts) await MoveAsync(_home, ReturnDuration, ct);
            }
            catch (OperationCanceledException)
            {
                // 재바인딩으로 끊긴 재생. 여기서 삼키지 않으면 예외가 턴 루프까지 올라가 전투가 멈춘다.
            }
            finally
            {
                CompleteImpact(impact);
                // 취소로 빠져나가도 몸통이 슬롯 사이에 남지 않게 한다.
                if (owner == _playbackCts && visualRoot != null) visualRoot.localPosition = _home;
            }

            await effects;
        }

        /// <summary>대상 앞으로 붙는 자리를 이 배틀러의 지역 좌표로 반환한다.</summary>
        /// <param name="targets">이번 턴에 맞는 대상들. 둘 이상이면 상대 진영 중앙으로 간다.</param>
        private Vector3 ApproachDestination(IReadOnlyList<CombatantViewModel> targets)
        {
            var single = targets.Count == 1 ? _battlerPosition?.Invoke(targets[0]) : null;
            var world = single ?? FormationCenter(!_unit.IsAlly);
            // 아군은 오른쪽, 적은 왼쪽을 보고 선다. 대면 방향으로 간격만큼 앞에서 멈춘다.
            world.x -= _unit.IsAlly ? MeleeGap : -MeleeGap;

            // 트윈은 지역 좌표로 돈다. 슬롯 앵커에 배율이 걸려 있어 월드 거리를 그대로 쓰면 어긋난다.
            var local = visualRoot.parent != null ? visualRoot.parent.InverseTransformPoint(world) : world;
            local.z = _home.z;
            return local;
        }

        /// <summary>몸통을 한 자리로 옮긴다.</summary>
        /// <returns>이동이 끝나면 완료된다. 취소되면 트윈만 죽고 대기는 예외 없이 완료된다.</returns>
        private UniTask MoveAsync(Vector3 local, float duration, CancellationToken ct)
            => visualRoot
                .DOLocalMove(local, duration / _speed())
                .SetEase(Ease.OutQuad)
                .ToUniTask(cancellationToken: ct);

        /// <summary>
        /// 공격 모션을 한 번 재생하고 무기가 닿는 시점에 타격을 알린다. 공격 상태가 없는 배틀러는 대기 없이 끝난다.
        /// </summary>
        /// <param name="impact">무기가 닿는 시점에 종결할 타격 알림.</param>
        /// <returns>모션이 끝나면 완료된다. 취소되면 예외 없이 완료된다.</returns>
        private async UniTask PlayAttackAsync(UniTaskCompletionSource impact, CancellationToken ct)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            animator.Play(AttackStateId, 0, 0f);
            // Play는 다음 갱신에야 반영된다. 0초를 흘려 지금 상태로 만들어야 아래에서 공격 상태를 읽는다.
            animator.Update(0f);

            var state = animator.GetCurrentAnimatorStateInfo(0);
            // 공격 상태가 없는 컨트롤러면 대기 상태가 그대로 잡힌다. 그 길이를 기다리면 엉뚱하게 붙잡는다.
            if (state.shortNameHash != AttackStateId) return;

            // 상태 길이는 클립 원본 길이다. animator.speed가 반영되지 않아 배속으로 나눠야 실제 재생 시간이 된다.
            int speed = Mathf.Max(1, _speed());
            // 굽힌 값이 클립보다 길면 모션 끝에 맞춘다. 클립을 갈아 끼우고 다시 굽기 전 상태가 여기 걸린다.
            float impactSeconds = Mathf.Clamp(_impactTime, 0f, state.length);
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(impactSeconds / speed), cancellationToken: ct);
                CompleteImpact(impact);
                await UniTask.Delay(TimeSpan.FromSeconds((state.length - impactSeconds) / speed),
                    cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                // 재바인딩으로 끊긴 대기. 여기서 삼키지 않으면 예외가 턴 루프까지 올라가 전투가 멈춘다.
            }
        }

        /// <summary>
        /// 연출을 스폰할 부모 앵커를 반환한다. 사망으로 배틀러가 숨어도 연출은 남아야 하므로 부모 앵커 밑에 스폰한다.
        /// </summary>
        /// <returns>씬 종료 등으로 앵커가 파괴 중이면 null. 호출부는 스폰을 건너뛴다.</returns>
        private Transform SpawnParentOrNull()
        {
            var parent = transform.parent;
            if (parent == null || parent.gameObject.IsDestroying()) return null;
            return gameObject.IsDestroying() ? null : parent;
        }

        /// <summary>이펙트 스펙을 이 배틀러 위치에 스폰해 재생한다.</summary>
        /// <returns>스펙·프리팹이 없으면 즉시 완료된다.</returns>
        private UniTask SpawnEffect(EffectSpec spec)
        {
            if (spec == null || effectPlayerPrefab == null) return UniTask.CompletedTask;
            var parent = SpawnParentOrNull();
            if (parent == null) return UniTask.CompletedTask;
            var player = Instantiate(effectPlayerPrefab, visualRoot.position, Quaternion.identity, parent);
            // 재생기는 대기가 끝나야 스스로 파괴된다. 재바인딩으로 끊으면 그 경로를 못 타고 화면에 남으므로
            // 배틀러 재생과 달리 파괴 토큰을 그대로 쓴다.
            return player.Play(spec, _speed(), this.GetCancellationTokenOnDestroy());
        }

        /// <summary>파티클 이펙트 스펙을 스폰해 재생한다. 유지 레이어가 있는 스펙은 보유 목록에 남긴다.</summary>
        /// <param name="source">이 스펙을 띄운 스킬. 유지 이펙트를 걷을 시점의 기준이 된다.</param>
        /// <returns>스펙·프리팹이 없으면 즉시 완료된다. 유지 레이어는 대기에 포함되지 않는다.</returns>
        private UniTask SpawnVfx(VfxSpec spec, SkillSO source)
        {
            if (spec == null || vfxPlayerPrefab == null) return UniTask.CompletedTask;
            var parent = SpawnParentOrNull();
            if (parent == null) return UniTask.CompletedTask;
            var player = Instantiate(vfxPlayerPrefab, visualRoot.position, Quaternion.identity, parent);
            // 재생기는 유지 레이어를 붙들면 스스로 사라지지 않는다. 재바인딩으로 끊기면 안 되므로 파괴 토큰을 쓴다.
            var play = player.Play(spec, _speed(), ResolveAnchor, this.GetCancellationTokenOnDestroy());
            // Play는 첫 대기 전까지 동기로 진행하므로 이 시점에 유지 레이어 등록이 끝나 있다.
            if (!player.HasHold) return play;

            StopHeldVfx(held => held == source); // 같은 스킬을 다시 걸면 앞의 것과 겹치지 않게 먼저 걷는다
            _heldVfx.Add((player, source));
            return play;
        }

        /// <summary>레이어 앵커를 월드 좌표로 변환한다.</summary>
        private Vector3 ResolveAnchor(VfxAnchor anchor)
        {
            // 전투 정리로 바인딩이 풀린 뒤에도 남아 있던 연출이 부를 수 있다. 그때는 제자리를 준다.
            if (_unit == null) return visualRoot.position;
            return anchor switch
            {
                VfxAnchor.Foot => transform.TransformPoint(new Vector3(0f, _groundLocalY, 0f)),
                VfxAnchor.Center => transform.TransformPoint(_bodyCenterLocal),
                VfxAnchor.Overhead => _headAnchor != null ? _headAnchor.position : visualRoot.position,
                // 진영 앵커는 시전자 기준이다. 적이 재생해도 AllAllies는 자기 편을 가리킨다.
                VfxAnchor.AllAllies => FormationCenter(_unit.IsAlly),
                VfxAnchor.AllEnemies => FormationCenter(!_unit.IsAlly),
                _ => visualRoot.position,
            };
        }

        /// <summary>한 진영 전체를 두른 범위의 중심. 주입이 없으면 이 배틀러 자리로 대신한다.</summary>
        /// <param name="ally">화면 기준 아군 진영이면 true.</param>
        private Vector3 FormationCenter(bool ally)
            => _formationBounds != null ? _formationBounds(ally).center : visualRoot.position;

        /// <summary>숫자 하나를 이 배틀러 위치에 만든다.</summary>
        /// <returns>프리팹이 없거나 씬이 파괴 중이면 null. 호출부는 표시를 건너뛴다.</returns>
        private FloatingText SpawnFloatingTextOrNull()
        {
            if (floatingTextPrefab == null) return null;
            var parent = SpawnParentOrNull();
            if (parent == null) return null;
            return Instantiate(floatingTextPrefab, visualRoot.position, Quaternion.identity, parent);
        }

        private void OnDestroy()
        {
            ResetPlayback();
            _bindings.Dispose();
        }
    }
}