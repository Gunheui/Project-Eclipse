using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Service;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 파티 편성 화면의 상태. 4개 슬롯을 리액티브로 들고, 슬롯 채움 신호에서 인원수·진입 가능 여부를 파생한다.
    /// 편성 자체는 이 VM이 아니라 <see cref="PlayerSave.Party"/>가 보관한다 — 이 VM은 씬 스코프라 전투를 다녀오면
    /// 새로 만들어지므로, 생성 시 저장된 편성을 읽어 슬롯을 채우고 변경 때마다 되쓴다.
    /// 전투 진입 시 슬롯 위치를 그대로 실어 보내 편성 칸이 전투 진영 배치가 되게 한다.
    /// </summary>
    public sealed class PartyFormationViewModel : ViewModelBase
    {
        /// <summary> 편성 슬롯 수(= 최대 파티 인원). </summary>
        public const int SlotCount = PlayerSave.PartySlotCount;

        private readonly PlayerSave _save;
        private readonly NavigationContext _nav;
        private readonly ISceneFlow _sceneFlow;

        private readonly ReactiveProperty<OwnedCharacter>[] _slots;
        private readonly ReadOnlyReactiveProperty<CharacterSO>[] _slotOccupants;
        private bool _entering;
        private int _pickSlot;

        /// <summary> 4개 편성 슬롯(도메인). 각 값은 채움(OwnedCharacter) 또는 빈칸(null). 편성 로직·검증의 원천. </summary>
        public IReadOnlyList<ReactiveProperty<OwnedCharacter>> Slots => _slots;

        /// <summary>
        /// 슬롯별 점유자 정의(Data 투영). 각 값은 채움 캐릭터의 CharacterSO 또는 빈칸(null). View가 슬롯별로 구독한다.
        /// View 레이어가 도메인(OwnedCharacter)을 보지 않도록 Slots를 정의로만 얇게 투영한 것.
        /// </summary>
        public IReadOnlyList<ReadOnlyReactiveProperty<CharacterSO>> SlotOccupants => _slotOccupants;

        /// <summary> 픽 세션을 연 슬롯 번호(0~3). 픽 화면이 교체 대상 슬롯으로 읽는다. </summary>
        public int PickSlot => _pickSlot;

        /// <summary> 채워진 슬롯 수(0~4). 슬롯 값 변화에서 파생. </summary>
        public ReadOnlyReactiveProperty<int> PartyCount { get; }

        /// <summary> 전투 진입 가능 여부(1명 이상이면 true, 4명 강제 아님). PartyCount에서 파생. </summary>
        public ReadOnlyReactiveProperty<bool> CanEnter { get; }

        /// <summary> 이번 편성이 향하는 스테이지. 진입 직전 StageSelect가 기록한 현재 선택 스테이지. </summary>
        public StageSO SelectedStage => _nav.SelectedStage;

        /// <param name="save">보유 캐릭터 원천이자 편성 보관처. 슬롯 초기값을 여기서 읽고 변경도 여기에 되쓴다.</param>
        /// <param name="nav">씬 경계 선택 보관함. 진입 시 슬롯 위치를 보존한 파티를 SelectedParty에 기록한다.</param>
        /// <param name="sceneFlow">진입 시 전투 씬으로 전환하는 창구.</param>
        public PartyFormationViewModel(PlayerSave save, NavigationContext nav, ISceneFlow sceneFlow)
        {
            _save = save;
            _nav = nav;
            _sceneFlow = sceneFlow;

            _slots = new ReactiveProperty<OwnedCharacter>[SlotCount];
            _slotOccupants = new ReadOnlyReactiveProperty<CharacterSO>[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = new ReactiveProperty<OwnedCharacter>(save.Party[i]);
                _slotOccupants[i] = _slots[i]
                    .Select(owned => owned != null ? owned.Definition : null)
                    .ToReadOnlyReactiveProperty(null);
            }

            PartyCount = Observable.CombineLatest(_slots)
                .Select(values => values.Count(v => v != null))
                .ToReadOnlyReactiveProperty(0);
            CanEnter = PartyCount
                .Select(count => count > 0)
                .ToReadOnlyReactiveProperty(false);
        }

        /// <summary>
        /// 슬롯 탭으로 픽 세션을 연다. 이후 픽 화면이 고른 캐릭터는 이 슬롯에 배치된다.
        /// </summary>
        /// <param name="slot">탭된 슬롯 번호(0~3). 픽 화면이 배치 대상(앵커)으로 읽는다.</param>
        public void BeginPick(int slot)
        {
            _pickSlot = slot;
        }

        /// <summary>
        /// 지정 슬롯에 캐릭터를 배치한다. 그 캐릭터가 다른 슬롯에 이미 있으면 그 슬롯을 비워 중복을 막는다
        /// (= 슬롯 간 이동). 대상 슬롯의 기존 점유자는 교체된다. 슬롯 사이 빈칸은 그대로 유지된다.
        /// 성공하면 편성이 <see cref="PlayerSave.Party"/>에 반영돼 화면을 떠나도 남는다.
        /// </summary>
        /// <param name="slot">배치할 슬롯 번호(0~3). 범위를 벗어나면 거부한다.</param>
        /// <param name="unit">배치할 보유 캐릭터. null이거나 미보유면 거부한다.</param>
        /// <returns>배치했으면 true, 검증에 걸려 거부됐으면 false(이때 슬롯 불변).</returns>
        public bool AssignToSlot(int slot, OwnedCharacter unit)
        {
            if (slot < 0 || slot >= SlotCount)
                return false;
            if (unit == null || !_save.OwnedCharacters.Contains(unit))
                return false;

            for (int i = 0; i < SlotCount; i++)
            {
                if (i != slot && _slots[i].Value == unit)
                    _slots[i].Value = null;
            }
            _slots[slot].Value = unit;
            SyncToSave();
            return true;
        }

        /// <summary>
        /// 지정 슬롯을 빈칸으로 만든다. 다른 슬롯은 당겨지지 않고 중간 빈칸으로 남으며, 편성 저장에도 반영된다.
        /// </summary>
        /// <param name="slot">비울 슬롯 번호(0~3). 범위를 벗어나면 무시한다.</param>
        public void ClearSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
                return;
            _slots[slot].Value = null;
            SyncToSave();
        }

        /// <summary>
        /// 현재 편성으로 전투 씬에 진입한다. 슬롯 위치를 그대로 유지한 4칸 목록(빈칸은 null)을
        /// NavigationContext.SelectedParty에 실어 전투 스코프가 아군을 구성한다 — 편성 칸이 곧 전투 진영 자리라
        /// 앞으로 당겨 압축하지 않는다. 빈 편성이면 아무 일도 하지 않고, 씬 로드 중복 진입도 한 번만 막는다(연타 방어).
        /// </summary>
        public void EnterBattle()
        {
            var party = _slots.Select(s => s.Value).ToList();
            if (party.All(c => c == null))
                return;
            if (_entering)
                return;
            _entering = true;
            _nav.SelectedParty = party;
            EnterBattleAsync().Forget();
        }

        // 전환이 성공하면 이 VM은 씬과 함께 내려가므로 가드를 풀 일이 없지만, 실패하면 화면이 그대로 남는다.
        // 그때 가드를 풀지 않으면 진입 버튼이 영구히 죽으므로 되돌린 뒤 예외를 다시 던져 드러낸다.
        private async UniTaskVoid EnterBattleAsync()
        {
            try
            {
                await _sceneFlow.ToBattleAsync();
            }
            catch
            {
                _entering = false;
                throw;
            }
        }

        // 현재 슬롯 상태를 편성 저장에 그대로 복사한다. 위치가 의미를 가지므로 인덱스를 맞춰 넣는다.
        private void SyncToSave()
        {
            for (int i = 0; i < SlotCount; i++)
                _save.Party[i] = _slots[i].Value;
        }

        /// <summary>보유한 슬롯 스트림과 파생 프로퍼티(PartyCount·CanEnter)를 모두 해제한다.</summary>
        protected override void OnDispose()
        {
            CanEnter.Dispose();
            PartyCount.Dispose();
            foreach (var occupant in _slotOccupants)
                occupant.Dispose();
            foreach (var slot in _slots)
                slot.Dispose();
        }
    }
}
