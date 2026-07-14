using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Eclipse.Data;
using Eclipse.Presentation;
using R3;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// 전장에 세우는 배틀러 하나. 대응하는 <see cref="CombatantViewModel"/>의 스프라이트를 월드 공간
    /// SpriteRenderer로 그리고, 자기 상태(HP·행동·생존)를 구독해 스스로 연출한다 — 피격 흔들림·플로팅
    /// 숫자·시전 돌진·사망 숨김. 중앙 지휘 없이 각자 자기 VM만 본다.
    /// </summary>
    public class BattlerView : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private FloatingText floatingTextPrefab;
        [SerializeField] private SpriteEffectPlayer effectPlayerPrefab;

        private readonly CompositeDisposable _bindings = new();

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
        public void Bind(CombatantViewModel unit, Func<int> speed)
        {
            _bindings.Clear();
            gameObject.SetActive(true);
            _speed = speed ?? (() => 1);
            if (visualRoot == null) visualRoot = transform;
            _home = visualRoot.localPosition;
            _facingRight = unit.IsAlly;
            _prevHp = unit.CurrentHp.CurrentValue;
            if (spriteRenderer != null) spriteRenderer.sprite = unit.BattlerSprite;

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

        /// <summary>대응 유닛이 없는 빈 배틀러를 숨긴다.</summary>
        public void Clear()
        {
            _bindings.Clear();
            gameObject.SetActive(false);
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

        // 이펙트 스펙을 이 배틀러 위치에 스폰해 재생한다. 스펙·프리팹이 없으면 즉시 완료.
        private UniTask SpawnEffect(EffectSpec spec)
        {
            if (spec == null || effectPlayerPrefab == null) return UniTask.CompletedTask;
            var player = Instantiate(effectPlayerPrefab, visualRoot.position, Quaternion.identity, transform.parent);
            return player.Play(spec, _speed(), this.GetCancellationTokenOnDestroy());
        }

        private void SpawnFloatingText(int amount, bool isHeal)
        {
            if (floatingTextPrefab == null) return;
            var ft = Instantiate(floatingTextPrefab, visualRoot.position, Quaternion.identity, transform.parent);
            ft.Show(amount, isHeal, _speed());
        }

        private void OnDestroy() => _bindings.Dispose();
    }
}