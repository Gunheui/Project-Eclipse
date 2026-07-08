using System.Collections.Generic;

namespace Eclipse.Domain
{
    /// <summary>
    /// 플레이어 계정의 진행 상태(보유 캐릭터 등)를 담는 런타임 모델.
    /// 영속성(저장/로드)은 별도 서비스가 담당하며, 이 객체는 상태만 보유한다.
    /// </summary>
    public class PlayerSave
    {
        /// <summary> 보유 캐릭터 목록. </summary>
        public List<OwnedCharacter> OwnedCharacters { get; }

        public PlayerSave(List<OwnedCharacter> ownedCharacters)
        {
            OwnedCharacters = ownedCharacters;
        }
    }
}
