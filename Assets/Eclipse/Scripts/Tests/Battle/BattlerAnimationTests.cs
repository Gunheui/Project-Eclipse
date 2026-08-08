using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.View;
using NUnit.Framework;
using R3;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eclipse.Tests
{
    /// <summary>
    /// 배틀러가 시전·사망 신호를 클립 길이만큼의 대기로 바꾸는 경로를 관측한다.
    /// 컨트롤러를 메모리에 세워야 해서 에디터 전용 어셈블리에 둔다.
    /// </summary>
    public class BattlerAnimationTests
    {
        private const float AttackSeconds = 1.5f;
        private const float DeadSeconds = 1f;
        // 클립 길이보다 짧아야 타격 알림이 모션 도중에 온다.
        private const float ImpactSeconds = 0.5f;

        private readonly List<Object> _spawned = new List<Object>();

        private BattlerView _view;
        private Animator _animator;
        private Combatant _model;
        private CombatantViewModel _unit;
        private Subject<Unit> _stateChanged;

        [SetUp]
        public void SetUp()
        {
            _model = Combatant.FromEnemy(Track(ScriptableObject.CreateInstance<EnemySO>()), 0,
                new Stats { hp = 100, atk = 10, def = 5, spd = 10 });
            _stateChanged = new Subject<Unit>();
            _unit = new CombatantViewModel(_model, _stateChanged, null, BuildController(), ImpactSeconds, null, null,
                null);

            // 연출은 배틀러의 부모 밑에 스폰되므로 전장 역할을 할 부모가 있어야 한다.
            var field = Track(new GameObject("Field"));
            var go = Track(new GameObject("Battler"));
            go.transform.SetParent(field.transform);
            _view = go.AddComponent<BattlerView>();
            _animator = go.AddComponent<Animator>();
            SetField(_view, "animator", _animator);
        }

        [TearDown]
        public void TearDown()
        {
            _unit.Dispose();
            _stateChanged.Dispose();
            // 에디트 모드에서는 Destroy가 다음 프레임을 기다리지 못해 에러를 낸다.
            foreach (var o in _spawned)
                if (o != null)
                    Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator 시전은_공격_클립_길이만큼_턴을_붙잡는다()
        {
            Bind();
            _unit.RaiseActed(null);

            Assert.IsFalse(_view.WaitForAnimation().Status.IsCompleted(),
                "대기가 바로 끝나면 모션이 끝나기 전에 다음 턴이 넘어간다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 클립_대기_중_재바인딩은_옛_대기를_예외_없이_끊는다()
        {
            Bind();
            _unit.RaiseActed(null);
            var pending = _view.WaitForAnimation();

            Bind();

            Assert.IsTrue(_view.WaitForAnimation().Status.IsCompleted(),
                "새 바인딩이 앞 방의 대기를 물려받으면 첫 턴이 그만큼 늦게 시작한다");

            yield return null; // 취소는 플레이어 루프에서 풀린다

            Assert.AreEqual(UniTaskStatus.Succeeded, pending.Status,
                "취소가 예외로 새면 그 예외가 턴 루프까지 올라가 전투가 멈춘다");
        }

        [UnityTest]
        public IEnumerator 타격_알림은_모션_도중에_온다()
        {
            Bind();
            _unit.RaiseActed(null);

            Assert.IsFalse(_view.WaitForImpact().Status.IsCompleted(),
                "시전과 함께 알리면 칼을 뽑기도 전에 상대가 피를 흘린다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 모션_대기_중_재바인딩은_타격을_알리고_끝낸다()
        {
            Bind();
            _unit.RaiseActed(null);
            var impact = _view.WaitForImpact();

            Bind();

            Assert.AreEqual(UniTaskStatus.Succeeded, impact.Status,
                "알릴 주체가 사라지면 턴 루프가 타격 대기에서 영영 선다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 사망은_사망_클립_길이만큼_턴을_붙잡는다()
        {
            Bind();
            _model.ApplyDamage(999);
            _stateChanged.OnNext(Unit.Default);

            Assert.IsFalse(_view.WaitForAnimation().Status.IsCompleted(),
                "대기가 바로 끝나면 쓰러지는 도중에 다음 턴이 넘어간다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 이미_죽은_유닛을_다시_바인딩해도_사망_연출은_돌지_않는다()
        {
            _model.ApplyDamage(999);

            Bind();

            Assert.IsTrue(_view.WaitForAnimation().Status.IsCompleted(),
                "배치 상태를 사망 전이로 읽으면 방을 열 때마다 시체가 다시 쓰러진다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 사망_뒤에_빠져나온_피격은_사망_모션을_덮지_않는다()
        {
            Bind();
            _model.ApplyDamage(999);
            _stateChanged.OnNext(Unit.Default);

            // 피격 표시는 대기열을 거쳐 사망보다 늦게 나갈 수 있다. 그 순서를 그대로 만든다.
            _unit.RaiseHit(null, new EffectResult(EffectType.Damage, _model, 10));

            // 게임 중에는 플레이어 루프가 애니메이터를 돌려 재생 지시가 반영되지만 에디트 모드에서는
            // 아무도 돌리지 않는다. 상태를 읽으려면 테스트가 직접 한 번 평가시켜야 한다.
            _animator.Update(0f);

            Assert.AreEqual(Animator.StringToHash("Dead"), _animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                "늦은 피격이 사망 모션을 덮으면 배틀러가 죽다 만 자세로 사라진다");
            yield return null;
        }

        private void Bind() => _view.Bind(_unit, () => 1, _ => new Bounds());

        /// <summary>
        /// 프로덕션 컨트롤러와 같은 모양의 오버라이드 컨트롤러. 상태 이름과 클립 접미가 조회 규칙이라 그대로 맞춘다.
        /// </summary>
        private AnimatorOverrideController BuildController()
        {
            var controller = Track(new AnimatorController());
            controller.AddLayer("Base Layer");

            var idle = Track(new AnimationClip { name = "Test_Idle" });
            var attack = Track(new AnimationClip { name = "Test_Attack" });
            var hit = Track(new AnimationClip { name = "Test_Hit" });
            var dead = Track(new AnimationClip { name = "Test_Dead" });
            // 길이가 있어야 시전·사망 대기가 실제로 열린다. 어떤 값을 키잉하는지는 이 테스트가 보지 않는다.
            attack.SetCurve(string.Empty, typeof(SpriteRenderer), "m_Color.a",
                AnimationCurve.Linear(0f, 1f, AttackSeconds, 1f));
            dead.SetCurve(string.Empty, typeof(SpriteRenderer), "m_Color.a",
                AnimationCurve.Linear(0f, 1f, DeadSeconds, 1f));

            var machine = controller.layers[0].stateMachine;
            machine.AddState("Idle").motion = idle;
            machine.AddState("Attack").motion = attack;
            machine.AddState("Hit").motion = hit;
            machine.AddState("Dead").motion = dead;

            return Track(new AnimatorOverrideController(controller));
        }

        /// <summary>씬에 저작해야 할 참조를 테스트에서 꽂는다.</summary>
        private static void SetField(BattlerView view, string name, Object value)
            => typeof(BattlerView)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(view, value);

        private T Track<T>(T o) where T : Object
        {
            _spawned.Add(o);
            return o;
        }
    }
}
