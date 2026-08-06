using System;
using System.Collections.Generic;
using Eclipse.Presentation;
using Eclipse.View.Theme;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 전투 HUD 좌상단 턴 순서 표시줄. 스케줄러가 계산한 다가올 행동 순서를 슬롯에 얼굴 아이콘으로 채운다.
    /// 왼쪽(0번)이 다음 차례이고, 아군/적은 프레임 색으로 구분한다. 전투 시작 시 Bind로 한 번 연결한다.
    /// </summary>
    public class TurnTimelineView : MonoBehaviour
    {
        // 한 칸의 참조 묶음. root는 대응 순서가 없을 때 숨기고, frame은 아군/적 틴트, portrait는 얼굴 아이콘.
        [Serializable]
        private struct Slot
        {
            public GameObject root;
            public Image frame;
            public Image portrait;
        }

        [SerializeField] private UIThemeSO theme;
        [SerializeField] private Slot[] slots;

        private readonly CompositeDisposable _bindings = new();

        /// <summary>이 표시줄을 전투 뷰모델의 다가올 순서에 연결한다. 이전 구독은 정리한다.</summary>
        public void Bind(BattleViewModel viewModel)
        {
            _bindings.Clear();

            // 칸 수가 뷰모델의 예보 길이와 다르면 남는 칸이 영영 비거나(더 많을 때) 예보가 잘린다(더 적을 때).
            // 둘 다 에러 없이 조용히 달라지므로 여기서 드러낸다.
            if (slots.Length != BattleViewModel.TimelineSlots)
                Debug.LogWarning($"{name}: 슬롯 {slots.Length}칸이 예보 길이 {BattleViewModel.TimelineSlots}과 다르다.", this);

            viewModel.UpcomingTurns
                .Subscribe(Render)
                .AddTo(_bindings);
        }

        /// <summary>다가올 순서를 왼쪽부터 칸에 채우고, 순서보다 많은 칸은 숨긴다.</summary>
        private void Render(IReadOnlyList<CombatantViewModel> order)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                bool has = order != null && i < order.Count;
                if (slot.root != null) slot.root.SetActive(has);
                if (!has) continue;

                var unit = order[i];
                if (slot.portrait != null)
                {
                    slot.portrait.sprite = unit.TimelineIcon;
                    slot.portrait.enabled = unit.TimelineIcon != null; // 스프라이트 없는 Image는 흰 박스로 그려진다
                }
                if (slot.frame != null) slot.frame.color = unit.IsAlly ? theme.battleAlly : theme.battleEnemy;
            }
        }

        private void OnDestroy() => _bindings.Dispose();
    }
}
