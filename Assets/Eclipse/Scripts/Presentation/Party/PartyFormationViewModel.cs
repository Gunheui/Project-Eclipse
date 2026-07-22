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
    /// 파티 편성 화면의 ViewModel. 4개 슬롯을 리액티브로 노출하고 인원수·진입 가능 여부를 파생한다.
    /// 편성 원본은 <see cref="PlayerSave.Party"/>가 보관한다. 이 VM은 씬 스코프라 생성 시 저장된 편성을
    /// 읽어 오고 변경 때마다 되쓴다. 전투 진입 시 슬롯 위치가 그대로 전투 진영 배치가 된다.
    /// </summary>
    public sealed class PartyFormationViewModel : ViewModelBase
    {
        /// <summary> 편성 슬롯 수(= 최대 파티 인원). </summary>
        public const int SlotCount = PlayerSave.PartySlotCount;

        private readonly PlayerSave _save;
        private readonly NavigationContext _nav;
        private readonly ISceneFlow _sceneFlow;

        // 편성 변경 직후 스냅샷을 저장하는 창구. WebGL은 탭 종료 훅이 없어 변경 시점 저장이 유일한 안전망이다.
        // null이면 저장을 건너뛴다(테스트 조립).
        private readonly SaveService _saveService;

        private readonly ReactiveProperty<OwnedCharacter>[] _slots;
        private readonly ReadOnlyReactiveProperty<CharacterSO>[] _slotOccupants;
        private bool _entering;
        private int _pickSlot;

        /// <summary> 4개 편성 슬롯(도메인). 각 값은 채움(OwnedCharacter) 또는 빈칸(null). 편성 로직·검증의 원천. </summary>
        public IReadOnlyList<ReactiveProperty<OwnedCharacter>> Slots => _slots;

        /// <summary>
        /// 슬롯별 점유자의 CharacterSO(빈칸은 null). View가 도메인(OwnedCharacter)을 보지 않도록
        /// Slots를 정의로만 얇게 투영한 것.
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

        public PartyFormationViewModel(PlayerSave save, NavigationContext nav, ISceneFlow sceneFlow,
            SaveService saveService)
        {
            _save = save;
            _nav = nav;
            _sceneFlow = sceneFlow;
            _saveService = saveService;

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

        /// <summary> 슬롯 탭으로 픽 세션을 연다. 이후 픽 화면이 고른 캐릭터는 이 슬롯에 배치된다. </summary>
        public void BeginPick(int slot)
        {
            _pickSlot = slot;
        }

        /// <summary>
        /// 지정 슬롯에 캐릭터를 배치한다. 다른 슬롯에 이미 있으면 그 슬롯을 비워 중복을 막고(슬롯 간 이동),
        /// 성공하면 <see cref="PlayerSave.Party"/>에 반영된다. 범위밖 슬롯·null·미보유 캐릭터는 거부한다.
        /// </summary>
        /// <returns>배치했으면 true, 거부됐으면 false(이때 슬롯 불변).</returns>
        public bool AssignToSlot(int slot, OwnedCharacter unit)
        {
            if (slot < 0 || slot >= SlotCount)
                return false;
            if (unit == null || !_save.OwnedCharacters.Contains(unit))
                return false;
            if (_slots[slot].Value == unit)
                return true; // 같은 캐릭터를 같은 칸에 다시 배치 — 무변화이므로 저장하지 않는다.

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
        /// 지정 슬롯을 빈칸으로 만든다. 다른 슬롯은 당겨지지 않고 중간 빈칸으로 남는다. 범위밖은 무시한다.
        /// </summary>
        public void ClearSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
                return;
            if (_slots[slot].Value == null)
                return; // 이미 빈칸 — 무변화이므로 저장하지 않는다.
            _slots[slot].Value = null;
            SyncToSave();
        }

        /// <summary>
        /// 현재 편성으로 전투 씬에 진입한다. 슬롯 위치를 유지한 4칸 목록(빈칸 null)을 SelectedParty에 실어
        /// 편성 칸이 곧 전투 진영 자리가 된다(압축하지 않는다). 빈 편성은 무시하고, 중복 진입은 가드로 막는다.
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

        // 현재 슬롯 상태를 편성 저장에 그대로 복사하고 즉시 디스크에 저장한다. 위치가 의미를 가지므로
        // 인덱스를 맞춰 넣는다. 성공한 변경(AssignToSlot·ClearSlot)에서만 불린다 — 거부·무변화는 저장하지 않는다.
        private void SyncToSave()
        {
            for (int i = 0; i < SlotCount; i++)
                _save.Party[i] = _slots[i].Value;
            _saveService?.Save();
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
