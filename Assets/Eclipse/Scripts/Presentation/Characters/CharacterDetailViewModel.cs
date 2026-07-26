using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Service;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 캐릭터 상세 화면의 ViewModel. 선택된 캐릭터의 정의·레벨을 읽어
    /// 표시용 값(현재 스탯·스킬·돌파)을 노출한다.
    /// 값이 변하지 않는 표시 전용 화면이므로 스트림이 아닌 고정 값으로 노출한다.
    /// </summary>
    public class CharacterDetailViewModel : ViewModelBase
    {
        private readonly OwnedCharacter _owned;
        private readonly Stats _currentStats;
        private readonly ISpriteProvider _spriteProvider;

        /// <summary> 표시명(정의에서 읽음). </summary>
        public string DisplayName => _owned.Definition.displayName;

        /// <summary> 등급(정의에서 읽음). </summary>
        public Rarity Rarity => _owned.Definition.rarity;

        /// <summary> 역할(정의에서 읽음). </summary>
        public Role Role => _owned.Definition.role;

        /// <summary> 현재 레벨. </summary>
        public int Level => _owned.Level;

        /// <summary> 돌파 단계(0 = 미돌파). </summary>
        public int AscensionTier => _owned.AscensionTier;

        /// <summary> 초상 스프라이트를 로드한다. 로딩 방식은 ISpriteProvider가 감춘다. </summary>
        public UniTask<Sprite> LoadPortraitAsync(CancellationToken ct = default)
            => _spriteProvider.LoadPortraitAsync(_owned.Definition, ct);

        /// <summary>
        /// 현재 레벨·돌파 기준 스탯 6종. 전투 조립과 같은 정본 빌더(<see cref="CharacterStats.BuildAllyStats"/>)를
        /// 태워 표시와 전투가 어긋나지 않는다. 생성 시점에 한 번 계산해 캐싱한다.
        /// </summary>
        public Stats CurrentStats => _currentStats;

        /// <summary> 기본 공격 정의. </summary>
        public SkillSO BasicSkill => _owned.Definition.basicSkill;

        /// <summary> 일반 스킬 정의. </summary>
        public SkillSO NormalSkill => _owned.Definition.normalSkill;

        /// <summary> 궁극기 정의. </summary>
        public SkillSO UltimateSkill => _owned.Definition.ultimateSkill;

        /// <summary>
        /// 내비게이션 보관함에서 선택된 캐릭터를 읽어 상세 표시 값을 구성한다.
        /// 전제: 생성 전에 NavigationContext.Selected가 설정돼 있어야 한다.
        /// </summary>
        public CharacterDetailViewModel(NavigationContext context, ISpriteProvider spriteProvider)
        {
            if (context.Selected == null)
                throw new InvalidOperationException(
                    "NavigationContext.Selected가 비어 있습니다. 상세 화면을 Push하기 전에 선택 캐릭터를 기록해야 합니다.");

            _owned = context.Selected;
            _spriteProvider = spriteProvider;
            _currentStats = CharacterStats.BuildAllyStats(_owned.Definition, _owned.Level, _owned.AscensionTier, null);
        }
    }
}
