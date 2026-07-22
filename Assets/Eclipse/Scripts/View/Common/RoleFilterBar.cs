using System;
using Eclipse.Data.Enums;
using Eclipse.View.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 역할 필터 버튼 줄(전체/탱커/딜러/힐러/지원). 캐릭터 목록 화면과 파티 픽 화면이 같은 프리팹을 쓴다.
    /// 필터 상태는 보유하지 않고 탭을 <see cref="Selected"/>로만 흘려보낸다. 어떤 필터가 켜졌는지는
    /// 각 화면의 ViewModel이 보관하고, 선택 표시는 화면이 <see cref="SetSelected"/>로 되돌려 준다.
    /// </summary>
    public class RoleFilterBar : MonoBehaviour
    {
        [SerializeField] private UIThemeSO theme;

        [SerializeField] private Button allButton;
        [SerializeField] private Button tankerButton;
        [SerializeField] private Button dealerButton;
        [SerializeField] private Button healerButton;
        [SerializeField] private Button supporterButton;

        /// <summary> 버튼을 탭할 때마다 선택된 역할이 흐른다. null = 전체(필터 해제). </summary>
        public event Action<Role?> Selected;

        private (Button Button, Role? Role)[] _entries;

        private (Button Button, Role? Role)[] Entries => _entries ??= new[]
        {
            (allButton, (Role?)null),
            (tankerButton, Role.Tanker),
            (dealerButton, Role.Dealer),
            (healerButton, Role.Healer),
            (supporterButton, Role.Supporter),
        };

        /// <summary>
        /// 어떤 버튼을 선택 상태로 보일지 정한다. 색만 바꿀 뿐 <see cref="Selected"/>를 발행하지 않으므로
        /// ViewModel의 필터 값을 그대로 흘려보내도 순환하지 않는다.
        /// </summary>
        /// <param name="role">선택으로 표시할 역할. null이면 "전체" 버튼이 선택 상태가 된다.</param>
        public void SetSelected(Role? role)
        {
            if (theme == null)
                return;
            foreach (var entry in Entries)
            {
                if (entry.Button == null)
                    continue;
                Paint(entry.Button.transform, entry.Role == role);
            }
        }

        private void OnEnable()
        {
            foreach (var entry in Entries)
            {
                if (entry.Button == null)
                    continue;
                var role = entry.Role;
                entry.Button.onClick.AddListener(() => Selected?.Invoke(role));
            }
        }

        private void OnDisable()
        {
            foreach (var entry in Entries)
                entry.Button?.onClick.RemoveAllListeners();
        }

        // 버튼 한 개의 배경·테두리·아이콘·라벨 색을 선택 상태에 맞춰 칠한다. 색은 전부 테마 토큰에서 읽는다.
        private void Paint(Transform button, bool selected)
        {
            button.GetComponent<Image>().color = selected ? theme.primarySubtle : theme.surface2;
            button.Find("Border").GetComponent<Image>().color = selected ? theme.primary : theme.borderDefault;
            button.Find("Icon").GetComponent<Image>().color = selected ? theme.primary : theme.textMedium;
            button.Find("Label").GetComponent<TMP_Text>().color = selected ? theme.primary : theme.textHigh;
        }
    }
}
