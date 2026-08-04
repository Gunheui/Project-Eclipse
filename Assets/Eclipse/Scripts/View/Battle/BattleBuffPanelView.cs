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
    /// 아군은 스킬 효과와 받은 카드를, 적은 거기에 변이와 받는 저주를 더해 보여준다.
    /// 한 화면에 한 유닛만 담는다.
    /// 이 컴포넌트가 붙은 오브젝트는 항상 켜 두고(주입 대상) 표시는 <see cref="body"/>로 여닫는다.
    /// </summary>
    public class BattleBuffPanelView : MonoBehaviour
    {
        [SerializeField] private UIThemeSO theme;

        // 여닫는 단위. 이 오브젝트만 꺼도 배경·본문이 함께 사라진다.
        [SerializeField] private GameObject body;

        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private TMP_Text statLabel;

        // 제목이 붙는 본문 구획 하나의 위젯 묶음. 씬에서 고정 개수로 배치해 참조를 연결한다.
        [Serializable]
        private struct Section
        {
            public GameObject root;
            public TMP_Text title;
            public TMP_Text value;
        }

        // 유닛 종류와 걸린 것에 따라 세울 구획 수가 달라, 채운 만큼만 앞에서부터 쓰고 나머지는 끈다.
        [SerializeField] private Section[] sections;

        // 적은 스킬 효과·변이·저주를 한꺼번에 달 수 있어 이만큼은 배선돼 있어야 한다.
        private const int RequiredSections = 3;

        private const string SkillEffectTitle = "스킬 효과";
        private const string CardEffectTitle = "카드 효과";
        private const string MutationTitle = "변이";
        private const string CurseTitle = "받는 저주";
        private const string EmptyNotice = "없음";

        private ChapterRunSession _session;

        // 지금 패널이 보여주는 유닛. 다른 유닛으로 넘어갈 때 늦게 도착한 닫기가 새 표시를 지우지 못하게 막는다.
        private CombatantViewModel _shown;

        // 열려 있는 동안 표시 내용을 따라가는 구독. 닫을 때 끊는다.
        private IDisposable _binding;

        [Inject]
        public void Construct(ChapterRunSession session) => _session = session;

        private void Start()
        {
            // 배선이 모자라면 SetSection이 조용히 넘어가 패널 전체가 빈 채로 뜬다. 씬에서 바로 잡히게 짚어 준다.
            if (sections.Length < RequiredSections)
                Debug.LogError($"{name}: 구획이 {sections.Length}칸뿐이다. 씬에서 {RequiredSections}칸을 배선해야 한다.", this);
            Close();
        }

        /// <summary>이 유닛의 상세를 펼친다. 이미 같은 유닛을 보여주고 있으면 다시 만들지 않는다.</summary>
        public void Show(CombatantViewModel unit)
        {
            // 터치는 누른 순간과 롱프레스 성립 시점에 표시 요청이 두 번 온다.
            if (_shown == unit) return;
            _shown = unit;

            // 최종 스탯도 지속 효과 목록도 스킬 효과가 붙고 풀릴 때마다 바뀐다.
            // 떠 있는 동안 같은 턴 신호를 타고 다시 그린다.
            _binding?.Dispose();
            _binding = unit.SkillEffects.Subscribe(effects => Render(unit, effects));

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
            _binding?.Dispose();
            _binding = null;
            _shown = null;
            if (body != null) body.SetActive(false);
        }

        private void OnDestroy() => _binding?.Dispose();

        /// <summary>이 유닛의 헤더·스탯 줄·구획을 지금 상태로 다시 채운다.</summary>
        /// <param name="skillEffects">스킬로 걸린 지속 효과만. 런 카드는 아래 카드 구획이 따로 싣는다.</param>
        private void Render(CombatantViewModel unit, IReadOnlyList<ActiveEffect> skillEffects)
        {
            if (headerLabel != null) headerLabel.text = unit.Name;
            if (statLabel != null) statLabel.text = RunTexts.StatLine(unit.EffectiveStats);

            int filled = 0;
            string lines = string.Join("\n", skillEffects.Select(RunTexts.EffectLine));
            if (lines.Length > 0) SetSection(filled++, SkillEffectTitle, lines);

            if (unit.IsAlly)
            {
                var cards = _session.AcquiredCards
                    .Where(c => !c.TargetsEnemies && c.PartySlot == unit.SlotIndex)
                    .Select(c => c.Card)
                    .ToList();
                SetSection(filled++, CardEffectTitle, cards.Count > 0 ? Lines(cards) : EmptyNotice);
            }
            else
            {
                if (unit.Mutation != null)
                    SetSection(filled++, MutationTitle, RunTexts.MutationEffect(unit.Mutation));

                var curses = _session.AcquiredCards.Where(c => c.TargetsEnemies).Select(c => c.Card).ToList();
                if (curses.Count > 0) SetSection(filled++, CurseTitle, Lines(curses));
            }

            // 끈 구획은 씬의 레이아웃 그룹이 흐름에서 빼고 패널 높이를 다시 잡는다.
            // 안의 TMP_Text가 꺼지면서 갱신을 걸어 주므로 여기서 따로 재계산을 호출하지 않는다.
            for (int i = filled; i < sections.Length; i++)
                if (sections[i].root != null) sections[i].root.SetActive(false);
        }

        /// <summary>index번 구획을 켜고 제목·본문을 적는다. 씬에 칸이 모자라면 그 구획은 버려진다.</summary>
        private void SetSection(int index, string titleText, string valueText)
        {
            if (index >= sections.Length) return;
            var section = sections[index];
            if (section.root != null) section.root.SetActive(true);
            if (section.title != null) section.title.text = titleText;
            if (section.value != null) section.value.text = valueText;
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
