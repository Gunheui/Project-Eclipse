using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Eclipse.Presentation
{
    /// <summary>
    /// 플레이어 영속 상태의 저장·복원 서비스. 런타임 홀더 3종(PlayerSave·CurrencyWallet·StageProgress)의
    /// 현재 상태를 <see cref="SaveData"/>로 스냅샷해 JSON 파일 하나로 쓴다.
    /// 복원(<see cref="LoadOrNew"/>·<see cref="BuildPlayerSave"/>·<see cref="ApplyChapters"/>)은 정적 메서드로
    /// 분리해 테스트가 프로덕션 경로를 그대로 태운다. 저장 실패는 로그만 남기고 삼킨다 —
    /// 저장이 게임 진행을 죽여선 안 된다.
    /// </summary>
    public sealed class SaveService
    {
        private readonly PlayerSave _save;
        private readonly CurrencyWallet _wallet;
        private readonly StageProgress _progress;
        private readonly string _filePath;

        /// <summary> 기본 저장 경로. 플랫폼별 영속 디렉터리 밑의 player.json. </summary>
        public static string DefaultFilePath => Path.Combine(Application.persistentDataPath, "player.json");

        /// <param name="save">보유 캐릭터·파티 원천.</param>
        /// <param name="wallet">재화 3종 잔액 원천.</param>
        /// <param name="progress">장별 클리어 카운트 원천.</param>
        /// <param name="filePath">저장 파일 경로. null이면 <see cref="DefaultFilePath"/>. 테스트가 임시 경로를 주입하는 이음새.</param>
        public SaveService(PlayerSave save, CurrencyWallet wallet, StageProgress progress, string filePath = null)
        {
            _save = save;
            _wallet = wallet;
            _progress = progress;
            _filePath = filePath ?? DefaultFilePath;
        }

        /// <summary>
        /// 현재 상태 전체를 스냅샷해 파일에 쓴다. WebGL에선 IndexedDB로 내려보내고, iOS에선 iCloud 백업 제외
        /// 플래그를 재적용한다. 어떤 예외도 밖으로 던지지 않는다 — 실패는 에러 로그만 남기고 게임은 계속된다.
        /// </summary>
        public void Save()
        {
            // ponytail: 원자적 쓰기 없음(쓰다 죽으면 파일 손상 가능) — LoadOrNew가 손상 파일을 신규 계정으로
            // 흡수하는 것이 방어선이다. 무결성이 중요해지면 temp 파일 + File.Replace로 업그레이드.
            try
            {
                File.WriteAllText(_filePath, JsonUtility.ToJson(Snapshot()));
                SyncWebGLFileSystem();
                ApplyIosNoBackupFlag();
            }
            catch (Exception e)
            {
                Debug.LogError($"세이브 저장 실패({_filePath}): {e.Message}");
            }
        }

        // 직렬화 준비(홀더 → DTO)와 파일 쓰기를 나누는 지점. 원격 백엔드가 생기면 이 DTO가 곧 요청 본문이 된다.
        private SaveData Snapshot()
        {
            return new SaveData
            {
                owned = _save.OwnedCharacters
                    .Select(o => new OwnedEntry { id = o.Definition.id, level = o.Level, ascension = o.AscensionTier })
                    .ToList(),
                essence = _wallet.Essence.CurrentValue,
                gold = _wallet.Gold.CurrentValue,
                manual = _wallet.Manual.CurrentValue,
                chapters = _progress.Snapshot()
                    .Select(c => new ChapterEntry { chapterId = c.chapterId, cleared = c.cleared })
                    .ToList(),
                party = _save.Party.Select(o => o != null ? o.Definition.id : "").ToArray(),
            };
        }

        /// <summary>
        /// 파일에서 SaveData를 읽는다. 파일 없음·손상·버전 불일치·그 외 모든 예외는 신규 계정(new SaveData())으로
        /// 수렴한다 — 로드는 어떤 경우에도 던지지 않는다. 어느 경로로 갈렸는지 로그를 남긴다.
        /// </summary>
        /// <param name="filePath">읽을 파일 경로.</param>
        /// <returns>복원된 데이터 또는 신규 계정 기본값. null이 아니다.</returns>
        public static SaveData LoadOrNew(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.Log($"세이브 파일 없음({filePath}) — 신규 계정으로 시작한다.");
                    return new SaveData();
                }

                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(filePath));
                if (data == null || data.version != 1)
                {
                    // 버전이 다르면 부분 역직렬화된 반쪽 상태를 쓰지 않고 신규 계정으로 취급한다.
                    Debug.LogWarning($"세이브 버전 불일치 또는 빈 파일({filePath}) — 신규 계정으로 시작한다.");
                    return new SaveData();
                }

                Debug.Log($"세이브 로드 완료({filePath}).");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"세이브 로드 실패({filePath}) — 신규 계정으로 시작한다: {e.Message}");
                return new SaveData();
            }
        }

        /// <summary>
        /// SaveData의 보유·파티를 PlayerSave로 복원한다. 카탈로그에 없는 캐릭터 id는 경고 후 건너뛴다(던지지 않음 —
        /// 앱 부트스트랩 중 호출되므로 예외는 곧 검은 화면이다). 파티 칸은 방금 복원한 보유 목록의 동일 인스턴스를
        /// 가리킨다 — 편성 검증(<see cref="PartyFormationViewModel.AssignToSlot"/>)이 참조 동등성으로 동작하기 때문이다.
        /// </summary>
        /// <param name="data">복원할 저장 데이터.</param>
        /// <param name="catalog">캐릭터 id → 정의 SO. 여기 없는 id는 복원되지 않는다.</param>
        /// <returns>복원된 PlayerSave. 세이브가 비었으면 보유 목록도 비어 있다.</returns>
        public static PlayerSave BuildPlayerSave(SaveData data, IReadOnlyDictionary<string, CharacterSO> catalog)
        {
            var owned = new List<OwnedCharacter>();
            foreach (var entry in data.owned ?? new List<OwnedEntry>())
            {
                if (string.IsNullOrEmpty(entry.id) || !catalog.TryGetValue(entry.id, out var definition))
                {
                    Debug.LogWarning($"세이브의 캐릭터 id '{entry.id}'가 카탈로그에 없다 — 건너뛴다.");
                    continue;
                }
                owned.Add(new OwnedCharacter(definition, entry.level, entry.ascension));
            }

            var save = new PlayerSave(owned);
            int slots = Math.Min(save.Party.Length, data.party?.Length ?? 0);
            for (int i = 0; i < slots; i++)
            {
                string id = data.party[i];
                if (string.IsNullOrEmpty(id))
                    continue;
                save.Party[i] = owned.FirstOrDefault(o => o.Definition.id == id);
            }
            return save;
        }

        /// <summary>
        /// SaveData의 장별 클리어 카운트를 StageProgress에 복원한다. 아직 등록되지 않은 장이면
        /// StageProgress가 보류했다가 첫 접근 때 적용한다(<see cref="StageProgress.Restore"/>).
        /// </summary>
        /// <param name="data">복원할 저장 데이터.</param>
        /// <param name="progress">복원 대상 진행도.</param>
        public static void ApplyChapters(SaveData data, StageProgress progress)
        {
            foreach (var chapter in data.chapters ?? new List<ChapterEntry>())
                progress.Restore(chapter.chapterId, chapter.cleared);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void EclipseSyncFs();
#endif

        // WebGL의 File.WriteAllText는 브라우저 메모리(IDBFS 캐시)까지만 쓴다 — IndexedDB로 내리려면 명시적 sync가 필요하다.
        private static void SyncWebGLFileSystem()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            EclipseSyncFs();
#endif
        }

        // 세이브를 iCloud 백업 대상에서 제외한다. 파일을 다시 쓰면 풀릴 수 있어 매 저장 후 재적용한다.
        private void ApplyIosNoBackupFlag()
        {
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.SetNoBackupFlag(_filePath);
#endif
        }
    }
}
