using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Presentation;
using Eclipse.View.Theme;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 전투 중 유닛 상세 패널. 머리 위 아이콘 행이 아이콘만 보여주므로, 유닛에 포인터를 올리거나
    /// 꾹 누르면 그 유닛에 걸린 것을 이름·효과·등급까지 펼친다. 손을 떼면 닫힌다.
    /// 아군은 받은 버프 카드를, 적은 변이와 받는 저주를 보여준다 — 한 화면에 한 유닛만 담는다.
    /// 이 컴포넌트가 붙은 오브젝트는 항상 켜 두고(주입 대상) 표시는 <see cref="body"/>로 여닫는다.
    /// </summary>
    public class BattleBuffPanelView : MonoBehaviour
    {
        [SerializeField] private UIThemeSO theme;

        // 여닫는 단위. 이 오브젝트만 꺼도 배경·본문이 함께 사라진다.
        [SerializeField] private GameObject body;

        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private TMP_Text statLabel;

        // 제목이 붙는 본문 구획 둘. 아군은 A만(받은 버프), 적은 A(변이)와 B(받는 저주)를 쓴다.
        [SerializeField] private GameObject sectionA;
        [SerializeField] private TMP_Text sectionATitle;
        [SerializeField] private TMP_Text sectionAValue;
        [SerializeField] private GameObject sectionB;
        [SerializeField] private TMP_Text sectionBTitle;
        [SerializeField] private TMP_Text sectionBValue;

        private const string BuffTitle = "받은 버프";
        private const string MutationTitle = "변이";
        private const string CurseTitle = "받는 저주";
        private const string EmptyNotice = "없음";

        private ChapterRunSession _session;

        // 지금 패널이 보여주는 유닛. 다른 유닛으로 넘어갈 때 늦게 도착한 닫기가 새 표시를 지우지 못하게 막는다.
        private CombatantViewModel _shown;

        // 열려 있는 동안 스탯 줄을 따라가는 구독. 닫을 때 끊는다.
        private IDisposable _statBinding;

        [Inject]
        public void Construct(ChapterRunSession session) => _session = session;

        private void Start() => Close();

        /// <summary>이 유닛의 상세를 펼친다. 이미 같은 유닛을 보여주고 있으면 다시 만들지 않는다.</summary>
        public void Show(CombatantViewModel unit)
        {
            // 터치는 누른 순간과 롱프레스 성립 시점에 표시 요청이 두 번 온다.
            if (_shown == unit) return;
            _shown = unit;
            if (headerLabel != null) headerLabel.text = unit.Name;

            // 최종 스탯은 스킬 버프가 붙고 풀릴 때마다 바뀐다. 떠 있는 동안 같은 턴 신호로 다시 그린다.
            _statBinding?.Dispose();
            _statBinding = unit.ActiveEffects.Subscribe(_ =>
            {
                if (statLabel != null) statLabel.text = RunTexts.StatLine(unit.EffectiveStats);
            });

            if (unit.IsAlly) FillForAlly(unit.SlotIndex);
            else FillForEnemy(unit);

            if (body != null) body.SetActive(true);
        }

        /// <summary>이 유닛을 보여주고 있을 때만 닫는다. 다른 유닛으로 이미 넘어갔으면 무시한다.</summary>
        public void Close(CombatantViewModel unit)
        {
            if (_shown == unit) Close();
        }

        /// <summary>패널을 닫는다. 열려 있지 않아도 호출 안전(멱등).</summary>
        public void Close()
        {
            // 전투 뷰모델보다 먼저 끊어야 한다 — 방이 바뀔 때 이미 버려진 프로퍼티를 붙들고 있지 않게.
            _statBinding?.Dispose();
            _statBinding = null;
            _shown = null;
            if (body != null) body.SetActive(false);
        }

        private void OnDestroy() => _statBinding?.Dispose();

        /// <summary>아군: 이 자리에 귀속된 카드만 세운다. 적 전체 저주는 아군에게 걸리지 않아 넣지 않는다.</summary>
        private void FillForAlly(int partySlot)
        {
            var cards = _session.AcquiredCards
                .Where(c => !c.TargetsEnemies && c.PartySlot == partySlot)
                .Select(c => c.Card)
                .ToList();

            SetSection(sectionA, sectionATitle, sectionAValue, BuffTitle,
                cards.Count > 0 ? Lines(cards) : EmptyNotice);
            if (sectionB != null) sectionB.SetActive(false);
        }

        /// <summary>적: 변이 배수와 런 전역 저주를 세운다. 없는 구획은 통째로 숨긴다.</summary>
        private void FillForEnemy(CombatantViewModel unit)
        {
            if (unit.Mutation != null)
                SetSection(sectionA, sectionATitle, sectionAValue, MutationTitle,
                    RunTexts.MutationEffect(unit.Mutation));
            else if (sectionA != null) sectionA.SetActive(false);

            var curses = _session.AcquiredCards.Where(c => c.TargetsEnemies).Select(c => c.Card).ToList();
            if (curses.Count > 0)
                SetSection(sectionB, sectionBTitle, sectionBValue, CurseTitle, Lines(curses));
            else if (sectionB != null) sectionB.SetActive(false);
        }

        private static void SetSection(GameObject root, TMP_Text title, TMP_Text value, string titleText, string body)
        {
            if (root != null) root.SetActive(true);
            if (title != null) title.text = titleText;
            if (value != null) value.text = body;
        }

        /// <summary>카드 하나가 한 줄이다. 등급은 색과 문구 두 채널로 함께 적는다.</summary>
        private string Lines(IEnumerable<BuffCard> cards)
            => string.Join("\n", cards.Select(c =>
                $"<color=#{ColorUtility.ToHtmlStringRGB(GradeColorOf(c.grade))}>" +
                $"{RunTexts.GradeLabel(c.grade)}</color>  {c.displayName}  {RunTexts.FormatCard(c)}"));

        /// <summary>등급명 텍스트색. 밝은 패널 표면 위에서 대비를 맞춘 어두운 변형이다.</summary>
        private Color GradeColorOf(CardGrade grade) => grade switch
        {
            CardGrade.Rare => theme.onCardGradeRare,
            CardGrade.Epic => theme.onCardGradeEpic,
            CardGrade.Unique => theme.onCardGradeUnique,
            _ => theme.onCardGradeCommon,
        };
    }
}
