using System;
using System.Collections.Generic;
using Eclipse.Data;
using Eclipse.Data.Enums;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 장별 스테이지 진행/해금 상태를 보관하는 반응형 서비스. 장마다 "클리어한 스테이지 수"와 총 스테이지 수를 들고,
    /// 각 스테이지의 3상태(클리어/열림/잠김)는 <see cref="StateOf"/>로 클리어 수에서 파생한다.
    /// 장은 사전 시드 없이 <see cref="ChapterSO"/>로 첫 접근할 때 미클리어 상태로 등록되며(lazy), 스테이지 수는
    /// 그 시점의 <see cref="ChapterSO.stages"/> 길이를 상한으로 삼는다. 내부 키는 <see cref="ChapterSO.id"/> —
    /// 세이브 로딩은 항목 교체가 아니라 기존 ReactiveProperty 값의 제자리 갱신으로 들어와야 구독이 유지된다.
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

        /// <summary>
        /// 스테이지 인덱스와 장의 클리어 수로 3상태를 계산하는 순수 함수.
        /// 인덱스가 클리어 수보다 작으면 이미 깬 것(Cleared), 같으면 지금 열린 것(Open), 크면 잠김(Locked).
        /// 1스테이지(index 0)는 클리어 수 0에서 자동으로 Open, 보스(마지막 index)는 그 앞이 모두 깨져야 Open이 되어
        /// 순차 해금·보스 규칙이 특수 분기 없이 성립한다.
        /// </summary>
        /// <param name="stageIndex">장 안에서의 0-기반 스테이지 인덱스.</param>
        /// <param name="clearedCount">해당 장에서 클리어한 스테이지 수.</param>
        /// <returns>해당 스테이지의 표시 상태.</returns>
        public static StageState StateOf(int stageIndex, int clearedCount)
        {
            if (stageIndex < clearedCount) return StageState.Cleared;
            if (stageIndex == clearedCount) return StageState.Open;
            return StageState.Locked;
        }

        /// <summary>
        /// 지정한 장의 클리어 수를 읽기 전용으로 노출한다. 항목 ViewModel이 이를 구독해 상태를 파생한다.
        /// 미등록 장이면 이 호출에서 미클리어 상태로 등록된다.
        /// </summary>
        /// <param name="chapter">조회할 장. null이거나 id/stages가 비면 예외.</param>
        /// <returns>해당 장의 클리어 수 스트림.</returns>
        public ReadOnlyReactiveProperty<int> ClearedCountOf(ChapterSO chapter)
            => EntryOf(chapter).Cleared;

        /// <summary>
        /// 지정한 스테이지를 클리어 처리한다. 지금 열려 있는(Open) 스테이지일 때만 클리어 수를 1 올린다.
        /// 미등록 장이면 이 호출에서 미클리어 상태로 등록된다. 음수 인덱스·장의 스테이지 수 이상(범위밖)·
        /// 아직 잠겼거나 이미 깬 스테이지는 무시한다(단조·순차 보장).
        /// 반환값은 곧 "이번이 최초 클리어인가"이며, 보상 지급이 최초 클리어분을 얹을지 판단하는 유일한 근거다.
        /// </summary>
        /// <param name="chapter">클리어한 스테이지가 속한 장. null이거나 id/stages가 비면 예외.</param>
        /// <param name="stageIndex">클리어한 스테이지의 0-기반 인덱스. [0, 장의 스테이지 수) 범위여야 한다.</param>
        /// <returns>클리어 수가 반영되면 true, 조건에 맞지 않아 무시되면 false.</returns>
        public bool TryMarkCleared(ChapterSO chapter, int stageIndex)
        {
            var entry = EntryOf(chapter);
            if (stageIndex < 0) return false;
            if (stageIndex >= entry.StageCount) return false;
            if (stageIndex != entry.Cleared.Value) return false;

            entry.Cleared.Value = stageIndex + 1;
            return true;
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

            entry = new ChapterEntry(cleared: 0, stageCount: chapter.stages.Length);
            _chapters[chapter.id] = entry;
            return entry;
        }

        /// <summary>보유한 모든 장의 <see cref="ReactiveProperty{T}"/>를 해제한다.</summary>
        public void Dispose()
        {
            foreach (var entry in _chapters.Values)
                entry.Cleared.Dispose();
            _chapters.Clear();
        }
    }
}
