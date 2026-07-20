using System.Collections.Generic;

namespace Eclipse.Domain
{
    /// <summary>
    /// 플레이어 계정의 진행 상태(보유 캐릭터 등)를 담는 런타임 모델.
    /// 영속성(저장/로드)은 별도 서비스가 담당하며, 이 객체는 상태만 보유한다.
    /// </summary>
    public class PlayerSave
    {
        /// <summary> 파티 편성 칸 수. 편성 화면 슬롯 수이자 전투 아군 진영의 자리 수와 같다. </summary>
        public const int PartySlotCount = 4;

        /// <summary> 보유 캐릭터 목록. </summary>
        public List<OwnedCharacter> OwnedCharacters { get; }

        /// <summary>
        /// 파티 편성. 길이는 항상 <see cref="PartySlotCount"/>이며, 인덱스가 곧 편성 칸 위치다(빈 칸은 null).
        /// 중간 빈 칸을 허용하므로 앞으로 당겨 압축하지 않는다 — 이 위치가 전투 진영 배치로 그대로 이어진다.
        /// </summary>
        public OwnedCharacter[] Party { get; }

        public PlayerSave(List<OwnedCharacter> ownedCharacters)
        {
            OwnedCharacters = ownedCharacters;
            Party = new OwnedCharacter[PartySlotCount];
        }
    }
}
