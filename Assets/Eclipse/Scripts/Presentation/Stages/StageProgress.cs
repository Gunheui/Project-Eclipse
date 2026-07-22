using System;
using System.Collections.Generic;
using Eclipse.Data;
using Eclipse.Data.Enums;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 장별 스테이지 진행/해금 상태를 보유하는 반응형 서비스(키는 <see cref="ChapterSO.id"/>).
    /// 장마다 클리어 수를 보유하고, 스테이지 3상태(클리어/열림/잠김)는 <see cref="StateOf"/>로 파생한다.
    /// 장은 첫 접근 시 미클리어로 등록되며(lazy), 세이브 복원은 기존 ReactiveProperty의 제자리 갱신이라 구독이 유지된다.
    /// 씬 전환에서 살아남아야 하므로 <see cref="Eclipse.Core.AppLifetimeScope"/>에 싱글톤으로 등록한다.
    /// </summary>
    public sealed class StageProgress : IDisposable
    {
        // 장별 진행 항목: 클리어 수(반응형)와 상한 검사용 총 스테이지 수.
        private sealed class ChapterEntry
        {
            public readonly ReactiveProperty<int> Cleared;
            public readonly int StageCount;

            public ChapterEntry(int cleared, int stageCount)
            {
                Cleared = new ReactiveProperty<int>(cleared);
                StageCount = stageCount;
            }
        }

        // 장 id → 진행 항목. Cleared가 바뀌면 구독한 항목 상태가 갱신된다.
        private readonly Dictionary<string, ChapterEntry> _chapters = new Dictionary<string, ChapterEntry>();

        // 등록 전에 도착한 복원값(장 id → 클리어 수). EntryOf가 그 장을 등록하는 순간 소비·제거하므로
        // _chapters와 키가 겹치지 않는다(등록된 장의 복원은 제자리 갱신으로 간다).
        private readonly Dictionary<string, int> _pending = new Dictionary<string, int>();

        /// <summary>
        /// 스테이지 3상태를 계산하는 순수 함수: 인덱스 &lt; 클리어 수 = Cleared, 같으면 Open, 크면 Locked.
        /// 이 규칙 하나로 순차 해금·보스 잠금이 특수 분기 없이 성립한다.
        /// </summary>
        public static StageState StateOf(int stageIndex, int clearedCount)
        {
            if (stageIndex < clearedCount) return StageState.Cleared;
            if (stageIndex == clearedCount) return StageState.Open;
            return StageState.Locked;
        }

        /// <summary>
        /// 지정한 장의 클리어 수 스트림. 미등록 장이면 이 호출에서 미클리어로 등록된다.
        /// </summary>
        /// <param name="chapter">조회할 장. null이거나 id/stages가 비면 예외.</param>
        public ReadOnlyReactiveProperty<int> ClearedCountOf(ChapterSO chapter)
            => EntryOf(chapter).Cleared;

        /// <summary>
        /// 지정한 스테이지를 클리어 처리한다. 지금 열린(Open) 스테이지일 때만 클리어 수를 1 올리고,
        /// 범위밖·잠김·이미 깬 스테이지는 무시한다(단조·순차 보장).
        /// </summary>
        /// <returns>true면 최초 클리어(보상 지급이 최초 클리어분을 얹는 유일한 근거). 무시되면 false.</returns>
        public bool TryMarkCleared(ChapterSO chapter, int stageIndex)
        {
            var entry = EntryOf(chapter);
            if (stageIndex < 0) return false;
            if (stageIndex >= entry.StageCount) return false;
            if (stageIndex != entry.Cleared.Value) return false;

            entry.Cleared.Value = stageIndex + 1;
            return true;
        }

        /// <summary>
        /// 저장된 클리어 수를 복원한다. 등록된 장이면 제자리 갱신(구독 유지), 미등록 장이면 보류했다가
        /// 첫 접근 때 초기값으로 소비한다. 같은 장에 여러 번 호출되면 마지막 값이 이긴다.
        /// </summary>
        /// <param name="chapterId">복원할 장의 id. null/빈 문자열이면 무시한다.</param>
        /// <param name="cleared">저장된 클리어 수. 음수는 0으로, 총 스테이지 수 초과분은 상한으로 잘린다.</param>
        public void Restore(string chapterId, int cleared)
        {
            if (string.IsNullOrEmpty(chapterId))
                return;

            if (_chapters.TryGetValue(chapterId, out var entry))
                entry.Cleared.Value = Math.Clamp(cleared, 0, entry.StageCount);
            else
                _pending[chapterId] = Math.Max(0, cleared); // 상한 클램프는 등록 시점(스테이지 수 확정 때)에 한다.
        }

        /// <summary>
        /// 저장용 스냅샷(장 id별 클리어 수, 장마다 한 항목). 보류 장(복원 후 미접근)도 포함해
        /// 로드 직후 바로 저장해도 진행도가 유실되지 않는다.
        /// </summary>
        public IEnumerable<(string chapterId, int cleared)> Snapshot()
        {
            foreach (var pair in _chapters)
                yield return (pair.Key, pair.Value.Cleared.Value);
            foreach (var pair in _pending)
                yield return (pair.Key, pair.Value);
        }

        // 장 진행 항목을 찾고, 없으면 미클리어로 만들어 등록한다(lazy). 스테이지 수는 등록 시점 stages 길이로
        // 고정되므로, 같은 id가 다른 스테이지 수로 다시 오면 데이터 불일치로 보고 즉시 드러낸다.
        private ChapterEntry EntryOf(ChapterSO chapter)
        {
            if (chapter == null)
                throw new ArgumentNullException(nameof(chapter));
            if (string.IsNullOrEmpty(chapter.id))
                throw new ArgumentException("장 id가 비어 있다 — 진행도 키로 쓸 수 없다.", nameof(chapter));
            if (chapter.stages == null)
                throw new ArgumentException($"장 '{chapter.id}'의 stages가 null이다 — 스테이지 수를 정할 수 없다.", nameof(chapter));

            if (_chapters.TryGetValue(chapter.id, out var entry))
            {
                if (entry.StageCount != chapter.stages.Length)
                    throw new InvalidOperationException(
                        $"장 '{chapter.id}'의 스테이지 수가 등록 시점과 다르다(등록 {entry.StageCount} ↔ 현재 {chapter.stages.Length}).");
                return entry;
            }

            // 복원이 등록보다 먼저 도착한 장이면 보류값을 초기 클리어 수로 소비한다(총 스테이지 수로 클램프).
            int cleared = 0;
            if (_pending.Remove(chapter.id, out var pendingCleared))
                cleared = Math.Min(pendingCleared, chapter.stages.Length);

            entry = new ChapterEntry(cleared, stageCount: chapter.stages.Length);
            _chapters[chapter.id] = entry;
            return entry;
        }

        /// <summary>보유한 모든 장의 <see cref="ReactiveProperty{T}"/>를 해제한다.</summary>
        public void Dispose()
        {
            foreach (var entry in _chapters.Values)
                entry.Cleared.Dispose();
            _chapters.Clear();
            _pending.Clear();
        }
    }
}
