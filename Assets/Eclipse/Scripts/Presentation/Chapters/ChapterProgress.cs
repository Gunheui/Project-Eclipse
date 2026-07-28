using System;
using System.Collections.Generic;
using Eclipse.Data;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 챕터별 클리어 여부를 보유하는 앱-스코프 서비스(키는 <see cref="ChapterSO.id"/>).
    /// 저장 형태는 구 StageProgress와 같은 (chapterId, int) 쌍이고, 값 도메인만 0/1로 좁아졌다.
    /// 씬 전환에서 살아남아야 하므로 <see cref="Eclipse.Core.AppLifetimeScope"/>에 싱글톤으로 등록한다.
    /// </summary>
    public sealed class ChapterProgress
    {
        // 장 id → 클리어 여부(0/1). 직렬화 키·형태 보존을 위해 int로 든다.
        private readonly Dictionary<string, int> _chapters = new Dictionary<string, int>();

        /// <summary> 해당 챕터를 클리어했는지 알려 준다. </summary>
        public bool IsCleared(ChapterSO chapter)
            => _chapters.TryGetValue(KeyOf(chapter), out var cleared) && cleared > 0;

        /// <summary> 해당 챕터를 클리어로 기록한다. 이미 클리어면 무동작(멱등). </summary>
        public void MarkCleared(ChapterSO chapter)
            => _chapters[KeyOf(chapter)] = 1;

        /// <summary>
        /// 저장된 클리어 값을 복원한다. 음수는 0으로, 1 초과는 1로 좁힌다(구 세이브의 클리어 수 흡수).
        /// </summary>
        /// <param name="chapterId">복원할 장의 id. null/빈 문자열이면 무시한다.</param>
        public void Restore(string chapterId, int cleared)
        {
            if (string.IsNullOrEmpty(chapterId))
                return;
            _chapters[chapterId] = Math.Clamp(cleared, 0, 1);
        }

        /// <summary> 저장용 스냅샷(장 id별 클리어 값, 장마다 한 항목). </summary>
        public IEnumerable<(string chapterId, int cleared)> Snapshot()
        {
            foreach (var pair in _chapters)
                yield return (pair.Key, pair.Value);
        }

        /// <summary>
        /// 진행도 키를 검증해 꺼낸다. id 없는 챕터는 데이터 오류이므로 즉시 드러낸다.
        /// </summary>
        private static string KeyOf(ChapterSO chapter)
        {
            if (chapter == null)
                throw new ArgumentNullException(nameof(chapter));
            if (string.IsNullOrEmpty(chapter.id))
                throw new ArgumentException("장 id가 비어 있다 — 진행도 키로 쓸 수 없다.", nameof(chapter));
            return chapter.id;
        }
    }
}
