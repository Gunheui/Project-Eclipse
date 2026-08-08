using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eclipse.View;
using Eclipse.View.Theme;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Eclipse.EditorTools
{
    /// <summary>
    /// 프리팹과 열린 씬에 박혀 있는 색을 토큰과 대조해 <see cref="ThemedGraphic"/>을 붙인다.
    /// 스캔은 아무것도 바꾸지 않으므로 부착 뒤에도 회귀 감지용으로 다시 돌린다.
    /// </summary>
    public static class ThemeMigrationTools
    {
        private const string PrefabRoot = "Assets/Eclipse/Prefabs";

        // 화면이 실제로 참조하는 테마 정본. Data/UI/Tone의 톤 시안도 같은 타입이라 타입 검색으로는 못 가린다.
        private const string ThemePath = "Assets/Eclipse/Data/UI/PeriwinkleTheme.asset";

        private static readonly UIThemeToken[] AllTokens =
            (UIThemeToken[])Enum.GetValues(typeof(UIThemeToken));

        // 사람이 판정한 색. 자동 매칭보다 먼저 본다.
        // #5A6180은 캡션 텍스트 자리인데 onCardGradeCommon과 값이 같아 기계가 가릴 수 없다.
        // 그 토큰은 버프 카드 등급명 전용이고 CardPickPopupView가 코드로 칠하므로 프리팹에 박힐 자리가 아니다.
        // 나머지 넷은 톤을 Lumiel로 바꾸기 전의 primary 계열이다. 지금 팔레트와 값이 어긋나 자동으로는 못 잇는다.
        private static readonly (string Hex, UIThemeToken Token, bool TranslucentOnly)[] ManualMatches =
        {
            ("5A6180", UIThemeToken.TextMedium, false),

            // 톤을 Lumiel로 바꾸기 전의 팔레트. 지금 값과 어긋나 자동으로는 못 잇는다.
            ("6E7BF2", UIThemeToken.Primary, false),
            ("5C68DE", UIThemeToken.PrimaryHover, false),
            ("4C57C4", UIThemeToken.PrimaryPressed, false),
            ("C7CAE8", UIThemeToken.PrimaryDisabled, false),
            ("E7E9FC", UIThemeToken.PrimarySubtle, false),
            ("FAFBFE", UIThemeToken.Surface2, false),
            ("DBDEF0", UIThemeToken.BorderDefault, false),
            ("A7ACC4", UIThemeToken.TextDisabled, false),

            // 새로 판 토큰이 가져갈 색.
            ("F3F4FB", UIThemeToken.Surface1, false),
            ("EAECF7", UIThemeToken.Surface1, false),      // 목록 프레임 배경. 한 계단 차이라 같은 표면으로 본다
            ("C4C9E4", UIThemeToken.BorderStrong, false),
            ("70789E", UIThemeToken.BorderStrong, false),  // 어두운 카드 위 테두리
            ("171A24", UIThemeToken.SurfaceDark, false),
            ("1A1F2B", UIThemeToken.SurfaceDark, false),
            ("20283B", UIThemeToken.SurfaceDark, false),   // 체력 바 트랙
            ("000000", UIThemeToken.Scrim, false),
            ("1A1C2B", UIThemeToken.Scrim, false),
            ("0F121A", UIThemeToken.Scrim, false),
            ("0A0F1C", UIThemeToken.Scrim, false),
            ("0A0A12", UIThemeToken.Scrim, false),         // 화면 전환 페이드

            // 옛 textHigh와 값이 같다. 반투명일 때만 스크림으로 본다.
            ("23273D", UIThemeToken.Scrim, true),

            // 기존 토큰이 흡수하는 색.
            ("ECECEC", UIThemeToken.OnPrimary, false),     // 스크림 위 흰 글씨
            ("E8A83D", UIThemeToken.RaritySSR, false),     // 돌파 최대 라벨
            ("E0A32E", UIThemeToken.RaritySSR, false),     // 등급 텍스트
            ("DDE0EE", UIThemeToken.BorderDefault, false), // 스킬 버튼 비활성. 기본 테두리와 두 눈금 차이도 안 난다
            ("A8A8A8", UIThemeToken.TextDisabled, false),  // 카드 비활성 덮개. 무채색이지만 뜻이 같아 함께 움직인다
            ("C8C8C8", UIThemeToken.TextDisabled, false),  // 위와 같은 자리. 저작하다 만 회색이다
            ("D06A61", UIThemeToken.BattleEnemy, false),   // 정예 배지. 해로운 효과가 아니라 적을 가리킨다

            // 버튼 호버·누름 단계로 찍힌 색. 토큰에서 몇 눈금씩 어긋나 있어 가장 가까운 단계로 모은다.
            ("F5F5FE", UIThemeToken.Surface1, false),
            ("ECEDFD", UIThemeToken.PrimarySubtle, false),
            ("D6D8EA", UIThemeToken.BorderDefault, false),
            ("B0B4CF", UIThemeToken.TextDisabled, false),
        };

        [MenuItem("Eclipse/Theme/하드코딩 색 스캔")]
        private static void Scan() => Run(dryRun: true);

        [MenuItem("Eclipse/Theme/이름표 부착")]
        private static void Attach()
        {
            bool ok = EditorUtility.DisplayDialog(
                "테마 이름표 부착",
                $"{PrefabRoot}의 프리팹과 현재 열린 씬에 ThemedGraphic을 부착한다.\n" +
                "프리팹은 즉시 저장되고 씬은 dirty 표시만 남는다.\n\n" +
                "깨끗한 커밋 상태에서 실행할 것.",
                "실행", "취소");
            if (ok) Run(dryRun: false);
        }

        private static void Run(bool dryRun)
        {
            var theme = LoadTheme();
            if (theme == null) return;

            var report = new Report();
            var palette = AllTokens.ToDictionary(t => t, t => Rgba32(theme.Resolve(t)));

            foreach (string path in PrefabPaths())
            {
                // 프리팹 에셋은 직접 못 고친다. 사본을 열어 고치고 되쓴 뒤 반드시 내려놓는다.
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int attached = ProcessRoot(root, theme, palette, dryRun, path, report);
                    if (!dryRun && attached > 0)
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                catch (Exception e)
                {
                    // 대량 저장은 Undo로 되돌릴 수단이 못 된다. 어디까지 반영됐는지 알리고 멈춘다.
                    Debug.LogError($"[테마 마이그레이션] {path} 처리 중 중단. 이전 프리팹까지는 반영됐다.\n{e}");
                    return;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                int attached = scene.GetRootGameObjects()
                    .Sum(go => ProcessRoot(go, theme, palette, dryRun, scene.name, report));

                // 저장은 사용자가 결정한다. 씬에 다른 편집이 섞여 있을 수 있다.
                if (!dryRun && attached > 0)
                    EditorSceneManager.MarkSceneDirty(scene);
            }

            report.LogNoTokens();
            Debug.Log(report.Compose(dryRun));
        }

        private static UIThemeSO LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(ThemePath);
            if (theme == null)
                Debug.LogError($"[테마 마이그레이션] 테마 정본을 찾지 못했다: {ThemePath}");

            return theme;
        }

        private static IEnumerable<string> PrefabPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal);

        /// <summary>계층 하나의 그래픽을 전부 판정한다.</summary>
        /// <param name="owner">로그에 찍을 소속 이름(프리팹 경로 또는 씬 이름).</param>
        /// <returns>실제로 부착한 개수. 0이면 저장할 이유가 없다.</returns>
        private static int ProcessRoot(
            GameObject root,
            UIThemeSO theme,
            Dictionary<UIThemeToken, int> palette,
            bool dryRun,
            string owner,
            Report report)
        {
            var managed = CollectManaged(root);
            int attached = ProcessSelectables(root, theme, palette, dryRun, owner, report);

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null) continue;

                if (graphic.GetComponent<ThemedGraphic>() != null) { report.AlreadyAttached++; continue; }

                // 중첩 인스턴스는 원본 프리팹을 처리할 때 붙는다. 여기서 건드리면 오버라이드만 쌓인다.
                if (PrefabUtility.IsPartOfPrefabInstance(graphic.gameObject)) { report.Nested++; continue; }

                if (managed.Contains(graphic)) { report.CodeOwned++; continue; }

                int rgba = Rgba32(graphic.color);

                // 순백은 스프라이트 원색을 그대로 내보내는 자리라 on-primary와 구분되지 않는다. 통째로 둔다.
                if (rgba == PureWhite) { report.PureWhite++; continue; }

                // 완전 투명은 탭 판정용 빈 영역이다. 무슨 색을 넣어도 보이지 않는다.
                if ((rgba & 0xFF) == 0) { report.FullyTransparent++; continue; }

                // 알파가 다르면 같은 토큰을 옅게 깐 자리로 보고 RGB만 맞춰 본다.
                bool keepAlpha = (rgba & 0xFF) != 0xFF;
                int lookup = keepAlpha ? (rgba | 0xFF) : rgba;

                var matches = ManualToken(lookup, keepAlpha, out var manual)
                    ? new List<UIThemeToken> { manual }
                    : palette.Where(kv => kv.Value == lookup).Select(kv => kv.Key).ToList();

                if (matches.Count == 0)
                {
                    report.NoToken(rgba, $"{owner} : {Path(graphic.transform)}");
                    continue;
                }
                if (matches.Count > 1)
                {
                    // 같은 색을 쓰는 토큰이 둘 이상이면 의미로 갈라야 한다. 기계가 고르지 않고 사람에게 넘긴다.
                    // 두 번째 인자를 주면 콘솔 줄을 눌러 해당 오브젝트를 바로 선택할 수 있다.
                    report.Ambiguous++;
                    Debug.Log(
                        $"[테마 마이그레이션] 토큰 후보 {string.Join(" / ", matches)} — {owner} : {Path(graphic.transform)}",
                        graphic);
                    continue;
                }

                report.Attachable(matches[0]);
                if (dryRun) continue;

                Bind(graphic.gameObject.AddComponent<ThemedGraphic>(), theme, matches[0], keepAlpha);
                attached++;
            }

            return attached;
        }

        /// <summary>ColorTint 버튼의 상태별 색을 판정한다. 그래픽 색과 별개 축이라 따로 훑는다.</summary>
        /// <returns>실제로 부착한 개수.</returns>
        private static int ProcessSelectables(
            GameObject root,
            UIThemeSO theme,
            Dictionary<UIThemeToken, int> palette,
            bool dryRun,
            string owner,
            Report report)
        {
            int attached = 0;

            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable == null) continue;
                if (selectable.transition != Selectable.Transition.ColorTint) continue;
                if (selectable.GetComponent<ThemedSelectable>() != null) { report.SelectableAttached++; continue; }
                if (PrefabUtility.IsPartOfPrefabInstance(selectable.gameObject)) { report.Nested++; continue; }

                var colors = selectable.colors;
                var slots = new[]
                {
                    colors.normalColor, colors.highlightedColor, colors.pressedColor,
                    colors.selectedColor, colors.disabledColor,
                };

                // Unity 기본 ColorBlock 그대로면 상태색을 저작한 적이 없는 버튼이다.
                // 스프라이트 원색에 기본 틴트만 얹혀 있으므로 토큰이 낄 자리가 아니다.
                if (IsDefaultColorBlock(colors)) { report.PureWhite++; continue; }

                // 어느 상태에서도 색이 안 실리면 스프라이트 원색만 쓰는 버튼이다.
                if (slots.All(c => Rgba32(c) == PureWhite || (Rgba32(c) & 0xFF) == 0)) { report.PureWhite++; continue; }

                // 상태마다 투명도가 다른 버튼이 있다. RGB만 토큰에서 받고 알파는 저작값을 지킨다.
                bool keepAlpha = slots.Any(c => (Rgba32(c) & 0xFF) != 0xFF);
                var tokens = slots.Select(c => Match(c, palette)).ToArray();

                if (tokens.Any(t => t == null))
                {
                    report.SelectableSkipped++;
                    Debug.Log(
                        $"[테마 마이그레이션] 상태색 일부가 토큰 밖 — {owner} : {Path(selectable.transform)} " +
                        $"[{string.Join(" ", slots.Select(c => Hex(Rgba32(c))))}]",
                        selectable);
                    continue;
                }

                report.SelectableAttachable();
                if (dryRun) continue;

                Bind(selectable.gameObject.AddComponent<ThemedSelectable>(), theme, tokens, keepAlpha);
                attached++;
            }

            return attached;
        }

        private static bool IsDefaultColorBlock(ColorBlock colors)
        {
            var d = ColorBlock.defaultColorBlock;
            return Rgba32(colors.normalColor) == Rgba32(d.normalColor)
                && Rgba32(colors.highlightedColor) == Rgba32(d.highlightedColor)
                && Rgba32(colors.pressedColor) == Rgba32(d.pressedColor)
                && Rgba32(colors.selectedColor) == Rgba32(d.selectedColor)
                && Rgba32(colors.disabledColor) == Rgba32(d.disabledColor);
        }

        /// <summary>색 하나에 대응하는 토큰. 투명도는 따로 지키므로 RGB만 본다.</summary>
        /// <returns>후보가 정확히 하나일 때만 값을 돌려준다.</returns>
        private static UIThemeToken? Match(Color color, Dictionary<UIThemeToken, int> palette)
        {
            int rgba = Rgba32(color);
            bool translucent = (rgba & 0xFF) != 0xFF;
            int lookup = rgba | 0xFF;

            if (ManualToken(lookup, translucent, out var manual)) return manual;

            var matches = palette.Where(kv => kv.Value == lookup).Select(kv => kv.Key).ToList();
            return matches.Count == 1 ? matches[0] : (UIThemeToken?)null;
        }

        private static void Bind(ThemedSelectable target, UIThemeSO theme, UIThemeToken?[] tokens, bool keepAlpha)
        {
            var so = new SerializedObject(target);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("normal").intValue = (int)tokens[0].Value;
            so.FindProperty("highlighted").intValue = (int)tokens[1].Value;
            so.FindProperty("pressed").intValue = (int)tokens[2].Value;
            so.FindProperty("selected").intValue = (int)tokens[3].Value;
            so.FindProperty("disabled").intValue = (int)tokens[4].Value;
            so.FindProperty("keepAlpha").boolValue = keepAlpha;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Bind(ThemedGraphic target, UIThemeSO theme, UIThemeToken token, bool keepAlpha)
        {
            var so = new SerializedObject(target);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("token").intValue = (int)token;
            so.FindProperty("keepAlpha").boolValue = keepAlpha;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>코드가 색을 칠하는 그래픽을 모은다. 두 주인이 한 그래픽을 칠하는 걸 막는다.</summary>
        private static HashSet<Graphic> CollectManaged(GameObject root)
        {
            var managed = new HashSet<Graphic>();

            // 테마를 안 읽으면서 색을 직접 칠하는 셋. 아래 참조 검사로는 안 걸려 계층째 뺀다.
            ExcludeSubtree<RoleFilterBar>(root, managed);   // 자식을 이름으로 찾아 칠한다
            ExcludeSubtree<ActingMarkerFx>(root, managed);  // 알파를 흔들어 발광을 만든다
            ExcludeSubtree<FloatingText>(root, managed);    // 데미지·힐 색을 호출자에게서 받는다

            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour is ThemedGraphic) continue;

                var held = new List<Graphic>();
                bool readsTheme = false;

                // 배열·구조체 안쪽까지 훑어야 슬롯 묶음으로 물려 있는 그래픽도 잡힌다.
                // Next 대신 NextVisible이라야 인스펙터에 뜨는 필드만 본다. 숨은 m_GameObject까지 훑으면
                // 모든 컴포넌트가 자기 오브젝트를 참조하는 꼴이 돼 그래픽 전체가 제외된다.
                var iterator = new SerializedObject(behaviour).GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;

                    var value = iterator.objectReferenceValue;
                    if (value is UIThemeSO)
                    {
                        readsTheme = true;
                        continue;
                    }

                    var referenced = value switch
                    {
                        GameObject go => go,
                        Component component => component.gameObject,
                        _ => null,
                    };
                    if (referenced == null) continue;

                    held.AddRange(referenced.GetComponents<Graphic>());
                }

                // 테마를 안 읽는 컴포넌트가 붙든 그래픽은 스프라이트나 크기만 다루는 자리다. 색은 비어 있다.
                if (readsTheme)
                    foreach (var graphic in held)
                        managed.Add(graphic);
            }

            return managed;
        }

        private static void ExcludeSubtree<T>(GameObject root, HashSet<Graphic> managed) where T : Component
        {
            foreach (var owner in root.GetComponentsInChildren<T>(true))
                foreach (var graphic in owner.GetComponentsInChildren<Graphic>(true))
                    managed.Add(graphic);
        }

        /// <summary>사람이 판정한 색인지 본다.</summary>
        /// <param name="rgba">알파를 뗀 조회용 값.</param>
        /// <param name="translucent">원래 색이 반투명이었는지. 같은 값을 쓰는 두 역할을 알파로 가른다.</param>
        private static bool ManualToken(int rgba, bool translucent, out UIThemeToken token)
        {
            foreach (var (hex, value, translucentOnly) in ManualMatches)
            {
                if (translucentOnly && !translucent) continue;
                if (!string.Equals(Hex(rgba), "#" + hex, StringComparison.OrdinalIgnoreCase)) continue;

                token = value;
                return true;
            }

            token = default;
            return false;
        }

        private const int PureWhite = unchecked((int)0xFFFFFFFF);

        private static int Rgba32(Color c) =>
            (To8(c.r) << 24) | (To8(c.g) << 16) | (To8(c.b) << 8) | To8(c.a);

        // 반올림해야 hex와 일치한다. Color32 캐스팅처럼 절삭하면 0.9490이 242가 아니라 241로 떨어져 매칭을 놓친다.
        private static int To8(float v) => Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

        private static string Hex(int rgba) =>
            $"#{(rgba >> 24) & 0xFF:X2}{(rgba >> 16) & 0xFF:X2}{(rgba >> 8) & 0xFF:X2}" +
            ((rgba & 0xFF) == 0xFF ? string.Empty : $"({rgba & 0xFF:X2})");

        private static string Path(Transform t)
        {
            var names = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent)
                names.Add(cur.name);
            names.Reverse();
            return string.Join("/", names);
        }

        /// <summary>판정 결과 집계. 부착 대상과 토큰 없는 색은 각각 무엇이 몇 건인지까지 센다.</summary>
        private class Report
        {
            public int AlreadyAttached;
            public int Nested;
            public int CodeOwned;
            public int PureWhite;
            public int Ambiguous;
            public int FullyTransparent;
            public int SelectableAttached;
            public int SelectableSkipped;

            private int _selectableAttachable;
            private readonly Dictionary<UIThemeToken, int> _attachable = new();
            private readonly Dictionary<int, int> _noToken = new();
            private readonly Dictionary<int, List<string>> _noTokenPlaces = new();

            public void SelectableAttachable() => _selectableAttachable++;

            public void Attachable(UIThemeToken token) =>
                _attachable[token] = _attachable.GetValueOrDefault(token) + 1;

            public void NoToken(int rgba, string place)
            {
                _noToken[rgba] = _noToken.GetValueOrDefault(rgba) + 1;

                // 색만으로는 승격 여부를 못 정한다. 어디에 쓰였는지 표본을 남긴다.
                if (!_noTokenPlaces.TryGetValue(rgba, out var places))
                    _noTokenPlaces[rgba] = places = new List<string>();
                if (places.Count < 3) places.Add(place);
            }

            public string Compose(bool dryRun)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[테마 마이그레이션] {(dryRun ? "스캔(드라이런)" : "부착 완료")}");
                sb.AppendLine(
                    $"부착 {(dryRun ? "가능" : "함")} {_attachable.Values.Sum()} / 애매 {Ambiguous} / " +
                    $"이미 부착 {AlreadyAttached} / 코드 관리 {CodeOwned} / 중첩 인스턴스 {Nested} / " +
                    $"순백 {PureWhite} / 완전 투명 {FullyTransparent} / 토큰 없음 {_noToken.Values.Sum()}");

                sb.AppendLine(
                    $"[버튼 상태색] 부착 {(dryRun ? "가능" : "함")} {_selectableAttachable} / " +
                    $"이미 부착 {SelectableAttached} / 사람 판정 대기 {SelectableSkipped}");

                sb.AppendLine("토큰별:");
                foreach (var (token, count) in _attachable.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
                    sb.AppendLine($"  {count,4}  {token}");

                sb.AppendLine($"토큰 없는 색 {_noToken.Count}종 — 색별 위치는 따로 찍는다");

                return sb.ToString();
            }

            /// <summary>토큰에 없는 색을 색마다 한 줄씩 찍는다. 한 덩어리로 묶으면 콘솔에서 잘린다.</summary>
            public void LogNoTokens()
            {
                foreach (var (rgba, count) in _noToken.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
                    Debug.Log($"[토큰없음] {Hex(rgba)} × {count} — {string.Join(" | ", _noTokenPlaces[rgba])}");
            }
        }
    }
}
