using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;

namespace Eclipse.Presentation
{
    /// <summary> 에스크로 보류분 = 고른 문 종류 + 그 문 지점의 깊이. 보상 payload는 공개 시점에 롤한다. </summary>
    public readonly struct EscrowedDoor
    {
        public EscrowedDoor(DoorKind kind, int depth)
        {
            Kind = kind;
            Depth = depth;
        }

        public DoorKind Kind { get; }
        public int Depth { get; }
    }

    /// <summary>
    /// 런 1판의 휘발 상태 단일 소유자. 방 인덱스·슬롯별 버프·적 디버프·에스크로·런 시드·수입 장부가
    /// 여기 모여 방을 넘고, 씬 언로드 시 통째로 폐기된다(직렬화 없음 — 앱 종료 = 실패).
    /// 챕터 데이터 정합성도 생성 시 1회 검증한다.
    /// </summary>
    public sealed class ChapterRunSession
    {
        private readonly StatModifierSet[] _buffs;
        private readonly Dictionary<CurrencyType, int> _income = new();
        private EscrowedDoor _escrow;

        /// <exception cref="ArgumentException">방 배치·정산 표·보스 행이 로드 검증을 통과하지 못할 때.</exception>
        public ChapterRunSession(ChapterSO chapter, EncounterTuningSO tuning,
            IReadOnlyList<OwnedCharacter> party, int runSeed)
        {
            Chapter = chapter ?? throw new ArgumentNullException(nameof(chapter));
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            Party = party ?? throw new ArgumentNullException(nameof(party));
            RunSeed = runSeed;
            Validate(chapter, tuning);

            _buffs = new StatModifierSet[party.Count];
            EnemyDebuffs = new StatModifierSet();
        }

        public ChapterSO Chapter { get; }

        /// <summary> 런 중 편성 불변. 인덱스 = 전투 진영 자리, 빈칸은 null. </summary>
        public IReadOnlyList<OwnedCharacter> Party { get; }

        /// <summary> 런 시작 시 1회 확정. 인카운터·문·카드·방별 전투 시드가 전부 여기서 파생된다. </summary>
        public int RunSeed { get; }

        /// <summary> 0-기반 현재 방. 승리에만 전진하므로 값이 곧 넘긴 방 수 = 정산 입력이다. </summary>
        public int RoomIndex { get; private set; }

        /// <summary> 현재 방 배치 행. 런 종료 후 접근은 예외. </summary>
        public RoomLayout CurrentRoom
        {
            get
            {
                if (RoomIndex >= Chapter.rooms.Length)
                    throw new InvalidOperationException("런이 끝났다 — 현재 방이 없다.");
                return Chapter.rooms[RoomIndex];
            }
        }

        /// <summary> 지나온 문 지점 수. 재화 문 공식의 깊이 d(1부터)와 같은 값이다. </summary>
        public int DoorPointsPassed { get; private set; }

        /// <summary> 런 전역 적 디버프 합(저주 카드). </summary>
        public StatModifierSet EnemyDebuffs { get; }

        /// <summary> 보류 중인 문이 있는지. 방1 진입 직후와 공개 직후에는 없다. </summary>
        public bool HasEscrow { get; private set; }

        /// <summary> 이 슬롯 캐릭터가 런에서 받은 버프 합. 빈 슬롯도 호출 가능(항상 비어 있다). </summary>
        public StatModifierSet BuffsOf(int partySlot)
            => _buffs[partySlot] ??= new StatModifierSet();

        /// <summary>
        /// 고른 문을 보류분으로 기록하고 문 지점 수를 1 올린다. 깊이는 그 지점 번호로 함께 확정된다.
        /// </summary>
        /// <exception cref="InvalidOperationException">이미 보류분이 있을 때(공개 전 중복 선택).</exception>
        public void HoldEscrow(DoorKind kind)
        {
            if (HasEscrow)
                throw new InvalidOperationException("이미 보류 중인 문이 있다 — 공개 전에 다시 고를 수 없다.");
            DoorPointsPassed++;
            _escrow = new EscrowedDoor(kind, DoorPointsPassed);
            HasEscrow = true;
        }

        /// <summary> 보류분을 비우면서 꺼낸다. 지급 전에 소진해야 중복 보고가 같은 보상을 두 번 태우지 못한다. </summary>
        /// <exception cref="InvalidOperationException">보류분이 없을 때.</exception>
        public EscrowedDoor ClaimEscrow()
        {
            if (!HasEscrow)
                throw new InvalidOperationException("보류 중인 문이 없다.");
            HasEscrow = false;
            return _escrow;
        }

        /// <summary> 보류분을 지급 없이 몰수한다. 런 종료 커밋의 첫 단계이며 없으면 무동작이다. </summary>
        public void ForfeitEscrow() => HasEscrow = false;

        /// <summary>
        /// 카드를 배정한다. 저주 카드는 슬롯과 무관하게 런 전역 적 디버프로 쌓인다.
        /// </summary>
        /// <param name="partySlot">배정 슬롯. 저주 카드면 무시된다.</param>
        public void AttachCard(BuffCard card, int partySlot)
        {
            if (card.targetsEnemies)
            {
                foreach (var delta in card.deltas)
                    EnemyDebuffs.Add(delta);
                return;
            }
            if (partySlot < 0 || partySlot >= Party.Count || Party[partySlot] == null)
                throw new ArgumentOutOfRangeException(nameof(partySlot), partySlot, "빈 슬롯에는 배정할 수 없다.");
            foreach (var delta in card.deltas)
                BuffsOf(partySlot).Add(delta);
        }

        /// <summary> 방 커서를 전진시킨다. 승리에만 부른다 — 그래서 RoomIndex가 곧 넘긴 방 수다. </summary>
        public void AdvanceRoom()
        {
            if (RoomIndex >= Chapter.rooms.Length)
                throw new InvalidOperationException("이미 마지막 방을 지났다.");
            RoomIndex++;
        }

        /// <summary> 런 중 지급된 재화를 장부에 누적한다(정산 팝업의 "런 중 획득" 블록). </summary>
        public void RecordIncome(IReadOnlyList<RewardEntry> receipts)
        {
            foreach (var entry in receipts)
            {
                _income.TryGetValue(entry.type, out int current);
                _income[entry.type] = current + entry.amount;
            }
        }

        /// <summary> 런 중 획득 재화 누계(재화별 1건). 아무것도 못 얻었으면 빈 목록. </summary>
        public IReadOnlyList<RewardEntry> RunIncome
            => _income.Select(p => new RewardEntry { type = p.Key, amount = p.Value }).ToList();

        /// <summary>
        /// 방 배치·정산 표의 정합성을 런 시작 시 1회 검증한다. 잘못된 데이터는 여기서 예외로 끊는다.
        /// </summary>
        private static void Validate(ChapterSO chapter, EncounterTuningSO tuning)
        {
            var rooms = chapter.rooms;
            if (rooms == null || rooms.Length == 0)
                throw new ArgumentException($"챕터 '{chapter.id}'의 방 배치가 비어 있다.", nameof(chapter));
            if (rooms.Count(r => r.kind == RoomKind.Boss) != 1 || rooms[^1].kind != RoomKind.Boss)
                throw new ArgumentException($"챕터 '{chapter.id}'의 보스 방은 정확히 1개, 마지막 행이어야 한다.", nameof(chapter));
            foreach (var room in rooms)
            {
                if (room.kind == RoomKind.Boss) continue;
                if (tuning.depths == null || tuning.depths.All(d => d.depth != room.depth))
                    throw new ArgumentException(
                        $"챕터 '{chapter.id}'의 방 깊이 {room.depth}가 인카운터 튜닝 깊이 표에 없다.", nameof(chapter));
            }
            if (chapter.settlement == null || chapter.settlement.Length != rooms.Length + 1)
                throw new ArgumentException(
                    $"챕터 '{chapter.id}'의 정산 표 행 수({chapter.settlement?.Length ?? 0})가 방 수 + 1({rooms.Length + 1})이 아니다.",
                    nameof(chapter));
        }
    }
}