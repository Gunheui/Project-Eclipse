using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.View;
using Eclipse.View.Theme;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eclipse.Tests.View
{
    /// <summary>
    /// 전투 씬은 상주라 방마다 같은 배틀러를 다시 바인딩한다. 재바인딩이 앞 방의 재생을 확실히
    /// 끊고 평상 상태로 되돌리는지 관측한다. 대기가 끝나야 드러나는 경합은 여기서 보지 못한다.
    /// </summary>
    public class BattlerRebindTests
    {
        // 배틀러를 원점이 아닌 자리에 세운다. 제자리를 바인딩마다 다시 읽으면 이 값이 어긋난 채 굳는다.
        private static readonly Vector3 SlotPosition = new Vector3(2f, 0f, 0f);

        private readonly List<Object> _spawned = new List<Object>();

        private Transform _field;
        private BattlerView _view;
        private Combatant _model;
        private CombatantViewModel _unit;
        private Subject<Unit> _stateChanged;

        [SetUp]
        public void SetUp()
        {
            _field = Track(new GameObject("Field")).transform;

            _model = Combatant.FromEnemy(BuildEnemy(), 0, new Stats { hp = 100, atk = 10, def = 5, spd = 10 });
            _stateChanged = new Subject<Unit>();
            _unit = new CombatantViewModel(_model, _stateChanged, null, null, null, null);

            // 연출은 배틀러의 부모 밑에 스폰되므로 전장 역할을 할 부모가 있어야 한다.
            var go = Track(new GameObject("Battler"));
            go.transform.SetParent(_field);
            go.transform.localPosition = SlotPosition;
            _view = go.AddComponent<BattlerView>();
            SetField(_view, "theme", Track(ScriptableObject.CreateInstance<UIThemeSO>()));
            SetField(_view, "floatingTextPrefab", BuildFloatingText());
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
        public IEnumerator 재바인딩한_뒤에도_새_피격이_숫자를_띄운다()
        {
            Bind();
            _unit.RaiseHit(null, Damage(10));
            Assert.AreEqual(1, Numbers(), "첫 표시는 대기 없이 바로 뜬다");

            // 앞 재생이 간격 대기에 머문 채로 다시 바인딩한다.
            Bind();
            _unit.RaiseHit(null, Damage(10));

            Assert.AreEqual(2, Numbers(),
                "재생중 표시가 켜진 채 남으면 이 배틀러는 그 뒤로 숫자를 영영 띄우지 않는다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 재바인딩은_줄에_남은_표시를_버린다()
        {
            Bind();
            _unit.RaiseHit(null, Damage(10));
            _unit.RaiseHit(null, Damage(20));
            _unit.RaiseHit(null, Damage(30));
            Assert.AreEqual(1, Numbers(), "한 번에 하나씩 나가므로 나머지 둘은 줄에 남는다");

            Bind();
            _unit.RaiseHit(null, Damage(40));

            Assert.AreEqual(2, Numbers(), "앞 방의 줄이 새 바인딩에서 이어 나오면 안 된다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 재바인딩은_연출로_어긋난_제자리를_되돌린다()
        {
            Bind();

            // 흔들림·돌진이 배틀러를 옮겨 놓은 상태. 취소된 트윈은 중간 위치에서 그대로 멈춘다.
            _view.transform.localPosition = SlotPosition + new Vector3(0.4f, 0.2f, 0f);

            Bind();

            Assert.AreEqual(SlotPosition, _view.transform.localPosition,
                "어긋난 자리를 제자리로 삼으면 이후 돌진이 매번 그 자리로 돌아간다");
            yield return null;
        }

        private void Bind() => _view.Bind(_unit, () => 1, _ => new Bounds());

        /// <summary>이 배틀러가 지금까지 띄운 숫자 수. 상승 연출이 끝나야 사라지므로 그대로 쌓인다.</summary>
        private int Numbers() => _field.GetComponentsInChildren<FloatingText>(true).Length;

        private EffectResult Damage(int amount) => new EffectResult(EffectType.Damage, _model, amount);

        /// <summary>라벨 없는 빈 껍데기로 충분하다. 이 테스트가 보는 건 스폰 횟수뿐이다.</summary>
        private FloatingText BuildFloatingText()
        {
            var go = Track(new GameObject("FloatingText"));
            go.SetActive(false);
            return go.AddComponent<FloatingText>();
        }

        private EnemySO BuildEnemy()
        {
            var enemy = Track(ScriptableObject.CreateInstance<EnemySO>());
            enemy.id = "test_enemy";
            enemy.displayName = "표적";
            return enemy;
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
