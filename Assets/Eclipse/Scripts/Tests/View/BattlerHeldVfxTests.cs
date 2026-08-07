using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.View;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eclipse.Tests.View
{
    /// <summary>
    /// 배틀러가 유지 이펙트의 수명을 지속 효과에 맞추는지 관측한다. 켜지고 꺼지는 시점, 시전 턴의
    /// 신호 순서, 재시전과 사망이 대상이다.
    /// </summary>
    public class BattlerHeldVfxTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private Transform _field;
        private Transform _visual;
        private BattlerView _view;
        private Combatant _model;
        private CombatantViewModel _unit;
        private Subject<Unit> _stateChanged;
        private SkillSO _skill;

        [SetUp]
        public void SetUp()
        {
            _field = Track(new GameObject("Field")).transform;
            _skill = BuildSkillWithAura();

            _model = Combatant.FromEnemy(BuildEnemy(), 0, new Stats { hp = 100, atk = 10, def = 5, spd = 10 });
            _stateChanged = new Subject<Unit>();
            _unit = new CombatantViewModel(_model, _stateChanged, null, null, 0f, null, null, null);

            // 연출은 배틀러의 부모 밑에 스폰되므로 전장 역할을 할 부모가 있어야 한다.
            // 실제 배틀러는 몸통이 자식이고 대시가 그 자식만 옮긴다. 같은 구조로 세워야 변위 계산이
            // 실제 경로를 탄다. Awake가 제자리를 잡기 전에 꽂으려고 비활성으로 만들어 둔다.
            var go = Track(new GameObject("Battler"));
            go.SetActive(false);
            go.transform.SetParent(_field);
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(go.transform, false);
            _view = go.AddComponent<BattlerView>();
            SetField(_view, "visualRoot", _visual);
            SetField(_view, "vfxPlayerPrefab", Track(new GameObject("VfxPlayer")).AddComponent<VfxPlayer>());
            go.SetActive(true);
            _view.Bind(_unit, () => 1, _ => new Bounds());
        }

        [TearDown]
        public void TearDown()
        {
            _unit.Dispose();
            _stateChanged.Dispose();
            foreach (var o in _spawned)
                if (o != null)
                    Object.Destroy(o);
            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator 시전_턴에_뜬_오라는_그_턴에_꺼지지_않는다()
        {
            // 실제 순서: 도메인이 효과를 걸고 → 시전 신호가 먼저 흐르고 → 상태 갱신이 뒤따른다.
            ApplyTaunt(duration: 2);
            _unit.RaiseActed(_skill);
            Assert.AreEqual(1, LitAuras(), "시전과 함께 오라가 뜬다");

            _stateChanged.OnNext(Unit.Default);

            Assert.AreEqual(1, LitAuras(), "스폰 시점에 대조했다면 직전 턴 집합에 없어 그 자리에서 꺼진다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 출처_효과가_풀리면_오라도_꺼진다()
        {
            ApplyTaunt(duration: 2);
            _unit.RaiseActed(_skill);
            _stateChanged.OnNext(Unit.Default);

            PassOwnTurn();
            _stateChanged.OnNext(Unit.Default);
            Assert.AreEqual(1, LitAuras(), "한 턴 남았으면 유지한다");

            PassOwnTurn();
            _stateChanged.OnNext(Unit.Default);

            Assert.AreEqual(0, LitAuras(), "도발이 풀리는 턴에 함께 걷힌다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 흡수량을_다_쓴_실드는_턴이_남아도_오라를_걷는다()
        {
            ((IDamageable)_model).ApplyEffect(StatusEffect.Shield(30, 5, _skill));
            _unit.RaiseActed(_skill);
            _stateChanged.OnNext(Unit.Default);
            Assert.AreEqual(1, LitAuras());

            ((IDamageable)_model).ApplyDamage(30);
            _stateChanged.OnNext(Unit.Default);

            Assert.AreEqual(0, LitAuras(), "다 쓴 실드는 목록에 남아 있어도 살아 있는 출처가 아니다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 같은_스킬을_다시_걸면_오라가_겹치지_않는다()
        {
            ApplyTaunt(duration: 2);
            _unit.RaiseActed(_skill);
            _stateChanged.OnNext(Unit.Default);

            _unit.RaiseActed(_skill);

            Assert.AreEqual(1, LitAuras(), "앞서 뜬 오라를 걷고 새로 띄운다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 죽은_배틀러의_오라는_남지_않는다()
        {
            ApplyTaunt(duration: 2);
            _unit.RaiseActed(_skill);
            _stateChanged.OnNext(Unit.Default);

            ((IDamageable)_model).ApplyDamage(_model.MaxHp);
            _stateChanged.OnNext(Unit.Default);

            Assert.AreEqual(0, LitAuras());
            yield return null;
        }

        [UnityTest]
        public IEnumerator 턴마다_오라는_홀더의_턴_시작마다_다시_터진다()
        {
            var eachTurn = BuildSkillWithAura(VfxHold.EachTurn);
            ((IDamageable)_model).ApplyEffect(StatusEffect.Taunt(3, eachTurn));
            _unit.RaiseActed(eachTurn);
            _stateChanged.OnNext(Unit.Default);

            var aura = _field.GetComponentsInChildren<VfxPlayer>(true).Single(p => p.HasHold);
            Assert.AreEqual(1, aura.transform.childCount, "시전 턴 몫이 한 번 재생된다");

            _unit.RaiseTurnStarted();

            Assert.AreEqual(2, aura.transform.childCount, "자기 턴 시작마다 한 번씩 다시 띄운다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 몸통이_대시하면_오라도_따라간다()
        {
            ApplyTaunt(duration: 2);
            _unit.RaiseActed(_skill);
            var aura = _field.GetComponentsInChildren<VfxPlayer>(true).Single(p => p.HasHold);
            var instance = aura.transform.GetChild(0);

            _visual.localPosition = new Vector3(3f, 0f, 0f);
            yield return null;

            Assert.AreEqual(3f, instance.position.x, 0.001f, "발밑 오라가 대시한 몸통을 따라간다");
        }

        /// <summary>이 배틀러 밑에서 아직 켜져 있는 오라 수. 걷힌 재생기는 유지 목록을 비우고 사라진다.</summary>
        private int LitAuras()
            => _field.GetComponentsInChildren<VfxPlayer>(true).Count(p => p.HasHold);

        private void ApplyTaunt(int duration)
            => ((IDamageable)_model).ApplyEffect(StatusEffect.Taunt(duration, _skill));

        /// <summary>이 유닛의 차례를 한 번 통째로 지나 보내 지속턴을 1 깎는다.</summary>
        private void PassOwnTurn()
        {
            _model.OnTurnStart();
            _model.OnTurnEnd();
        }

        /// <summary>유지 레이어 하나짜리 오라를 시전 이펙트로 물린 스킬.</summary>
        /// <param name="mode">유지 방식. 「턴마다」는 턴 시작 통지를 받을 때마다 다시 스폰된다.</param>
        private SkillSO BuildSkillWithAura(VfxHold mode = VfxHold.Continuous)
        {
            var layerPrefab = Track(new GameObject("AuraLayer"));
            layerPrefab.SetActive(false);

            var spec = Track(ScriptableObject.CreateInstance<VfxSpec>());
            spec.layers.Add(new VfxLayer
            {
                prefab = layerPrefab,
                holdTurns = 1,
                holdMode = mode,
                awaitSeconds = 0f,
            });

            var skill = Track(ScriptableObject.CreateInstance<SkillSO>());
            skill.id = "test_aura";
            skill.effects = new List<SkillEffect>();
            skill.castVfx = spec;
            return skill;
        }

        private EnemySO BuildEnemy()
        {
            var enemy = Track(ScriptableObject.CreateInstance<EnemySO>());
            enemy.id = "test_enemy";
            enemy.displayName = "표적";
            return enemy;
        }

        /// <summary>씬에 저작해야 할 직렬화 필드를 테스트에서 꽂는다.</summary>
        private static void SetField(BattlerView view, string name, object value)
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
