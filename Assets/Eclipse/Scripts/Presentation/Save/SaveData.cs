using System;
using System.Collections.Generic;
using Eclipse.Domain;

namespace Eclipse.Presentation
{
    /// <summary> 보유 캐릭터 한 명의 저장 형태. id는 CharacterSO.id. </summary>
    [Serializable]
    public struct OwnedEntry
    {
        public string id;
        public int level;
        public int ascension;

        /// <summary> 액티브 슬롯별 스킬 레벨. 길이·범위 정규화는 복원(BuildPlayerSave) 쪽이 담당한다. </summary>
        public int[] skillLevels;
    }

    /// <summary> 장 하나의 진행도 저장 형태. cleared는 해당 장에서 클리어한 스테이지 수(단조 증가 카운트). </summary>
    [Serializable]
    public struct ChapterEntry
    {
        public string chapterId;
        public int cleared;
    }

    /// <summary>
    /// 디스크 저장 전용 DTO. JsonUtility 제약에 맞춰 public 필드만 쓴다(프로퍼티·Dictionary 불가).
    /// 런타임 홀더(PlayerSave·CurrencyWallet·StageProgress)와 분리되어 있으며, 변환은 SaveService가 담당한다.
    /// 필드 초기값이 곧 신규 계정 기본값이다 — 파일이 없거나 손상이면 new SaveData()로 수렴한다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary> 현재 스키마 버전. v2 = 스킬 레벨·가챠 천장 필드 추가, 시작 보석 0. </summary>
        public const int CurrentVersion = 2;

        /// <summary> 스키마 버전. 값이 다르면 부분 역직렬화된 반쪽 상태를 쓰지 않고 신규 계정으로 취급한다. </summary>
        public int version = CurrentVersion;

        public List<OwnedEntry> owned = new List<OwnedEntry>();

        public int essence = 0;
        public int gold = 1000;
        public int manual = 0;

        public List<ChapterEntry> chapters = new List<ChapterEntry>();

        /// <summary> 파티 4칸. 인덱스 = 편성 칸 위치, 값 = 보유 캐릭터 id, "" = 빈칸. </summary>
        public string[] party = new string[PlayerSave.PartySlotCount];

        /// <summary> 가챠 천장 카운터. [이음새] 가챠 구현 전까지 유휴. </summary>
        public int pityCounter = 0;

        /// <summary> 다음 최고 등급의 픽업 확정 여부. [이음새] 가챠 구현 전까지 유휴. </summary>
        public bool pickupGuaranteed = false;
    }
}
