using System;
using System.Collections.Generic;
using System.Linq;

namespace Eclipse.Presentation
{
    /// <summary> 캐릭터 목록 정렬 기준. <see cref="CharacterSort.Next"/>가 이 순서로 순환한다. </summary>
    public enum CharacterSortKey { Rarity, Level, Name }

    /// <summary>
    /// 캐릭터 목록 정렬 규칙. 목록 화면과 픽 화면이 같은 순환 순서·같은 정렬 결과·같은 라벨을 쓰도록
    /// 한 곳에 모아 둔다. 동률은 항상 표시명 오름차순으로 갈라 순서를 결정적으로 유지한다.
    /// </summary>
    public static class CharacterSort
    {
        private const int KeyCount = 3;

        /// <summary>정렬 기준을 다음 값으로 넘긴다(마지막이면 처음으로 되돌아온다).</summary>
        public static CharacterSortKey Next(CharacterSortKey key)
            => (CharacterSortKey)(((int)key + 1) % KeyCount);

        /// <summary>정렬 기준의 표시 라벨("등급"/"레벨"/"이름"). 정의되지 않은 값이면 빈 문자열.</summary>
        public static string Label(CharacterSortKey key) => key switch
        {
            CharacterSortKey.Rarity => "등급",
            CharacterSortKey.Level => "레벨",
            CharacterSortKey.Name => "이름",
            _ => "",
        };

        /// <summary>
        /// 정렬 기준에 맞춰 새 리스트를 만들어 반환한다(원본 불변).
        /// </summary>
        /// <param name="character">항목에서 캐릭터 항목 VM을 꺼내는 접근자. 항목이 VM을 직접 담든 감싸든 대응한다.</param>
        public static List<T> Apply<T>(IEnumerable<T> source, Func<T, CharacterItemViewModel> character, CharacterSortKey key)
            => key switch
            {
                CharacterSortKey.Rarity => source
                    .OrderByDescending(x => (int)character(x).Rarity)
                    .ThenBy(x => character(x).DisplayName)
                    .ToList(),
                CharacterSortKey.Level => source
                    .OrderByDescending(x => character(x).Level.CurrentValue)
                    .ThenBy(x => character(x).DisplayName)
                    .ToList(),
                CharacterSortKey.Name => source
                    .OrderBy(x => character(x).DisplayName)
                    .ToList(),
                _ => source.ToList(),
            };
    }
}
