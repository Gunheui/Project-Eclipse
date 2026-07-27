using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 문 지점 팝업. 추첨된 문 3종을 약속 문구·확률 공시와 함께 보여 주고 선택을 결과로 돌려준다.
    /// 닫기 없는 강제 선택이다 — 문을 골라야만 닫힌다.
    /// </summary>
    public class DoorPointPopupView : MonoBehaviour, IPopup<DoorKind>
    {
        [SerializeField] private Button[] doorButtons;
        [SerializeField] private TMP_Text[] doorNames;
        [SerializeField] private TMP_Text[] doorPromises;
        [SerializeField] private TMP_Text[] doorOdds;

        private readonly UniTaskCompletionSource<DoorKind> _choice = new();

        /// <summary> 사용자가 고른 문. 재화 문도 여기서는 종류만 확정된다(지급은 다음 방 클리어 후). </summary>
        public UniTask<DoorKind> Result => _choice.Task;

        [Inject]
        public void Construct(ChapterRunFlow flow)
        {
            var doors = flow.Offer.CurrentValue.Doors;

            for (int i = 0; i < doorButtons.Length; i++)
            {
                if (doors == null || i >= doors.Count)
                {
                    doorButtons[i].gameObject.SetActive(false);
                    continue;
                }

                var option = doors[i];
                if (doorNames != null && i < doorNames.Length)
                    doorNames[i].text = option.DisplayName;
                if (doorPromises != null && i < doorPromises.Length)
                    doorPromises[i].text = option.Promise;
                if (doorOdds != null && i < doorOdds.Length)
                    doorOdds[i].text = $"등장 가중치 {option.Weight}/{option.TotalWeight}";

                doorButtons[i].onClick.AddListener(() => _choice.TrySetResult(option.Kind));
            }
        }

        /// <summary>팝업을 띄운다. 등장 연출이 없어 즉시 완료된다.</summary>
        public UniTask Open() => UniTask.CompletedTask;

        /// <summary>팝업을 닫는다. 파괴는 PopupManager가 한다.</summary>
        public UniTask Close() => UniTask.CompletedTask;
    }
}