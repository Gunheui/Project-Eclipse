using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using UnityEditor;
using UnityEngine;

namespace Eclipse.EditorTools
{
    /// <summary>
    /// 스킬에 배선된 이펙트를 전투를 거치지 않고 확인하는 창. 배틀러 한 명 크기의 기준 쿼드 둘을 세워 두고,
    /// 고른 스킬의 시전측·피격측을 원하는 시점에서 얼려 보여 준다. 배선 전체를 훑는 검사도 여기서 돌린다.
    /// </summary>
    /// <remarks>
    /// VfxPlayer.Play는 쓰지 않는다. 레이어 시작 지연을 타이머로 재기 때문에 시간이 흐르지 않는 화면에서는
    /// 지연이 걸린 레이어가 영영 뜨지 않는다. 대신 시점마다 레이어를 직접 세운다.
    /// </remarks>
    public sealed class SkillVfxViewerWindow : EditorWindow
    {
        // 전투 카메라와 같은 화각. 여기서 보이는 비율이 게임에서 보이는 비율이다.
        const float OrthoSize = 5.4f;

        // 기준 쿼드 크기 = 배틀러 한 명.
        const float BattlerWidth = 3f;
        const float BattlerHeight = 4f;

        // 검사에서 「화면을 덮는다」로 볼 폭.
        const float ScreenWidth = 22f;
        const float MaxTime = 4f;

        // 검사에서 크기를 재는 시점. 유지 오라는 1.5초는 지나야 입자가 생겨 이른 시점에서는 0으로 잡힌다.
        const float BurstSampleTime = 0.6f;
        const float HoldSampleTime = 2f;

        // 1대1 구도의 두 자리. 진영 앵커도 이 좌표로 대신한다.
        static readonly Vector2 CasterSlot = new(-2.5f, 0f);
        static readonly Vector2 TargetSlot = new(2.5f, 0f);

        // 시점 버튼. 터지고 사라지는 이펙트는 앞의 셋, 늦게 나타나는 유지 오라는 뒤의 셋으로 본다.
        static readonly float[] BurstTimes = { 0.1f, 0.3f, 0.6f };
        static readonly float[] HoldTimes = { 0.8f, 2f, 3.5f };

        sealed class Entry
        {
            public SkillSO Skill;
            public string Unit;
            public string Name;
        }

        readonly List<Entry> _entries = new();
        readonly List<string> _issues = new();
        string _search = string.Empty;
        Vector2 _listScroll;
        Vector2 _issueScroll;
        Entry _selected;
        bool _impactSide;
        float _time = 0.3f;
        bool _autoPlay;
        float _autoSpeed = 0.5f;
        double _lastTick;

        PreviewRenderUtility _preview;
        GameObject _stage;
        GameObject _root;

        [MenuItem("Eclipse/스킬 이펙트 뷰어")]
        static void Open() => GetWindow<SkillVfxViewerWindow>("스킬 이펙트 뷰어").minSize = new Vector2(860, 560);

        void OnEnable()
        {
            Rescan();
            _lastTick = EditorApplication.timeSinceStartup;
        }

        void OnDisable() => ReleaseStage();

        // 편집 모드에는 프레임이 흐르지 않는다. 자동 진행은 창이 스스로 시간을 재서 매번 다시 세운다.
        void Update()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = Mathf.Clamp((float)(now - _lastTick), 0f, 0.1f);
            _lastTick = now;

            if (!_autoPlay || _selected == null) return;
            _time += delta * _autoSpeed;
            if (_time > MaxTime) _time = 0f;
            Rebuild();
            Repaint();
        }

        /// <summary>배선된 스킬만 목록에 올린다.</summary>
        void Rescan()
        {
            _entries.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Eclipse.Data.SkillSO"))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill == null || (skill.castVfx == null && skill.impactVfx == null)) continue;

                var name = skill.name;
                var cut = name.IndexOf('_');
                _entries.Add(new Entry
                {
                    Skill = skill,
                    Name = name,
                    Unit = cut > 0 ? name[..cut] : name,
                });
            }

            _entries.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            if (_selected != null && !_entries.Contains(_selected)) Select(null);
        }

        VfxSpec CurrentSpec()
        {
            if (_selected == null) return null;
            return _impactSide ? _selected.Skill.impactVfx : _selected.Skill.castVfx;
        }

        void Select(Entry entry)
        {
            _selected = entry;
            Rebuild();
        }

        // ---- 프리뷰 씬 ----

        void EnsureStage()
        {
            if (_preview != null) return;

            _preview = new PreviewRenderUtility();
            var cam = _preview.camera;
            cam.cameraType = CameraType.Preview;
            cam.orthographic = true;
            cam.orthographicSize = OrthoSize;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            _preview.lights[0].intensity = 1.2f;
            _preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            _preview.lights[1].intensity = 0.8f;

            _stage = new GameObject("stage") { hideFlags = HideFlags.HideAndDontSave };
            _preview.AddSingleGO(_stage);
            MakeSlotQuad(CasterSlot).transform.SetParent(_stage.transform, true);
            MakeSlotQuad(TargetSlot).transform.SetParent(_stage.transform, true);
            _root = new GameObject("layers") { hideFlags = HideFlags.HideAndDontSave };
            _root.transform.SetParent(_stage.transform, false);
        }

        /// <summary>배틀러 한 명 크기의 기준 쿼드. 이펙트가 이것보다 얼마나 큰지로 배율을 판단한다.</summary>
        static GameObject MakeSlotQuad(Vector2 center)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "slot";
            go.hideFlags = HideFlags.HideAndDontSave;
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = new Vector3(center.x, center.y, 1f);
            go.transform.localScale = new Vector3(BattlerWidth, BattlerHeight, 1f);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                color = new Color(0.72f, 0.72f, 0.78f, 0.22f),
                hideFlags = HideFlags.HideAndDontSave,
            };
            renderer.sortingOrder = -100;
            return go;
        }

        void ReleaseStage()
        {
            if (_stage != null) DestroyImmediate(_stage);
            _stage = null;
            _root = null;
            _preview?.Cleanup();
            _preview = null;
        }

        // ---- 합성 ----

        /// <summary>지금 시점에 화면에 있어야 할 레이어를 전부 새로 세운다.</summary>
        void Rebuild()
        {
            EnsureStage();
            ClearLayers();

            var spec = CurrentSpec();
            if (spec?.layers == null) return;

            foreach (var layer in spec.layers)
            {
                if (layer.prefab == null) continue;

                // 유지 레이어는 한 번 떠서 계속 남으므로 반복 회차를 세지 않는다.
                if (layer.holdTurns > 0)
                {
                    if (_time >= layer.startDelay) Spawn(layer, layer.startDelay);
                    continue;
                }

                var repeats = Mathf.Max(1, layer.repeatCount);
                for (var i = 0; i < repeats; i++)
                {
                    var birth = layer.startDelay + i * layer.repeatInterval;
                    if (_time >= birth) Spawn(layer, birth);
                }
            }
        }

        void ClearLayers()
        {
            if (_root == null) return;
            for (var i = _root.transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(_root.transform.GetChild(i).gameObject);
        }

        /// <summary>레이어 한 벌을 세우고 태어난 뒤 흐른 만큼 굴린다.</summary>
        /// <param name="birth">이 인스턴스가 태어나는 시각(초). 반복 레이어는 회차마다 다르다.</param>
        /// <remarks>
        /// 위치·회전·배율·색·정렬을 입히는 순서는 VfxPlayer.Spawn과 같다. 에디터는 다른 어셈블리라 그쪽
        /// private 메서드에 닿지 못해 옮겨 적었다. 배선 규칙을 고치면 두 곳을 같이 고쳐야 한다.
        /// </remarks>
        GameObject Spawn(VfxLayer layer, float birth)
        {
            var go = Instantiate(layer.prefab, _root.transform);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(true); // 비활성으로 저장된 프리팹은 켜지 않으면 Simulate가 먹지 않는다.

            var tr = go.transform;
            tr.position = AnchorPosition(layer);
            tr.rotation = Quaternion.Euler(layer.rotation);
            tr.localScale = layer.prefab.transform.localScale * layer.scale;
            if (layer.overrideColor) ApplyColor(go, layer.color);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                renderer.sortingOrder = layer.sortingOrder;

            SimulateTo(go, _time - birth);
            return go;
        }

        /// <summary>레이어 앵커를 프리뷰 좌표로 옮긴다. 시전 스펙은 왼쪽 자리, 피격 스펙은 오른쪽 자리가 기준이다.</summary>
        Vector3 AnchorPosition(VfxLayer layer)
        {
            var slot = _impactSide ? TargetSlot : CasterSlot;
            var origin = layer.anchor switch
            {
                VfxAnchor.Foot => new Vector3(slot.x, slot.y - BattlerHeight * 0.5f, 0f),
                VfxAnchor.Overhead => new Vector3(slot.x, slot.y + BattlerHeight * 0.5f, 0f),
                VfxAnchor.AllAllies => new Vector3(CasterSlot.x, CasterSlot.y, 0f),
                VfxAnchor.AllEnemies => new Vector3(TargetSlot.x, TargetSlot.y, 0f),
                _ => new Vector3(slot.x, slot.y, 0f),
            };
            return origin + new Vector3(layer.offset.x, layer.offset.y, 0f);
        }

        /// <summary>파티클 시작색을 레이어 색으로 갈아 끼운다. 알파는 원본을 지켜 페이드가 남는다.</summary>
        static void ApplyColor(GameObject go, Color color)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var start = main.startColor;
                var alpha = start.mode switch
                {
                    ParticleSystemGradientMode.Color => start.color.a,
                    ParticleSystemGradientMode.TwoColors => start.colorMax.a,
                    _ => 1f,
                };
                // 곱하지 않고 대입한다. 어두운 색을 곱하면 발광 파티클이 그대로 사라진다.
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(color.r, color.g, color.b, color.a * alpha));
            }
        }

        /// <summary>파티클과 애니메이션을 경과 시간만큼 굴려 그 시점에 멈춰 세운다.</summary>
        static void SimulateTo(GameObject go, float elapsed)
        {
            var t = Mathf.Max(elapsed, 0.0001f);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Simulate(t, true, true, false);

            // 애니메이터로 도는 프리팹은 Simulate가 닿지 않아 정규화 시간으로 따로 세운다.
            foreach (var animator in go.GetComponentsInChildren<Animator>(true))
            {
                var controller = animator.runtimeAnimatorController;
                if (controller == null) continue;
                var clips = controller.animationClips;
                var length = clips is { Length: > 0 } ? Mathf.Max(clips[0].length, 0.01f) : 1f;
                animator.Play(0, 0, t / length);
                animator.Update(0f);
            }
        }

        // ---- 전체 검사 ----

        /// <summary>배선 전체를 훑어 위반만 모은다.</summary>
        void RunAudit()
        {
            EnsureStage();
            _issues.Clear();
            foreach (var entry in _entries)
            {
                Inspect(entry, entry.Skill.castVfx, false);
                Inspect(entry, entry.Skill.impactVfx, true);
            }

            if (_issues.Count == 0) _issues.Add("걸린 것이 없다.");
            Rebuild();
        }

        void Inspect(Entry entry, VfxSpec spec, bool impactSide)
        {
            if (spec?.layers == null) return;

            for (var i = 0; i < spec.layers.Count; i++)
            {
                var layer = spec.layers[i];
                var where = $"{entry.Name} · {(impactSide ? "피격" : "시전")} · 레이어 {i + 1}";
                if (layer.prefab == null)
                {
                    _issues.Add($"{where}: 프리팹이 비었다.");
                    continue;
                }

                if (!impactSide && layer.holdTurns > 0)
                    _issues.Add($"{where}: 유지 레이어가 시전 스펙에 있다. 효과를 받는 쪽에 붙어야 한다.");

                if (impactSide && layer.anchor is VfxAnchor.AllAllies or VfxAnchor.AllEnemies)
                    _issues.Add($"{where}: 진영 앵커를 피격 스펙에 썼다. 대상 수만큼 같은 자리에 겹친다.");

                var (warp, drawable) = CountRenderers(layer.prefab);
                if (drawable == 0)
                    _issues.Add($"{where}: 그려지는 렌더러가 없다.");
                // 왜곡을 빼고 하나만 남으면 그 프리팹은 본체가 왜곡이었다는 뜻이다. 정상 배선은 여섯 개 넘게 남는다.
                else if (warp > 0 && drawable <= 1)
                    _issues.Add($"{where}: 왜곡 렌더러 {warp}개를 빼면 그려지는 것이 {drawable}개뿐이다.");

                var size = MeasureSize(layer);
                if (size <= 0f) continue;
                if (size < BattlerWidth * 0.15f)
                    _issues.Add($"{where}: 배틀러 폭 {BattlerWidth:0}에 비해 너무 작다 (실측 {size:0.00}).");
                else if (size > ScreenWidth)
                    _issues.Add($"{where}: 화면 폭 {ScreenWidth:0}을 덮는다 (실측 {size:0.0}).");
            }
        }

        /// <summary>프리팹 안 렌더러를 왜곡 전용과 실제로 그려지는 것으로 나눠 센다.</summary>
        static (int Warp, int Drawable) CountRenderers(GameObject prefab)
        {
            var warp = 0;
            var drawable = 0;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (ScreenWarpFilter.IsWarpOnly(renderer.sharedMaterial)) warp++;
                else if (renderer.enabled && renderer.sharedMaterial != null) drawable++;
            }

            return (warp, drawable);
        }

        /// <summary>레이어가 화면에서 차지하는 폭. 켜진 렌더러를 다 감싼 크기에 배율을 곱한다.</summary>
        /// <returns>그려지는 것이 없으면 0.</returns>
        float MeasureSize(VfxLayer layer)
        {
            var go = Instantiate(layer.prefab, _root.transform);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(true);
            go.transform.position = Vector3.zero;
            SimulateTo(go, layer.holdTurns > 0 ? HoldSampleTime : BurstSampleTime);

            var bounds = new Bounds();
            var found = false;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else
                {
                    bounds = renderer.bounds;
                    found = true;
                }
            }

            DestroyImmediate(go);
            // 배율은 트랜스폼이 아니라 잰 값에 곱한다. 부모 배율을 받지 않는 프리팹이 섞여 있어 트랜스폼에
            // 주면 크기가 그대로다.
            return found ? Mathf.Max(bounds.size.x, bounds.size.y) * layer.scale : 0f;
        }

        // ---- 화면 ----

        void OnGUI()
        {
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                DrawStage();
            }

            if (_issues.Count > 0) DrawIssues();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                GUILayout.Label($"{_entries.Count}개", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("전체 검사", EditorStyles.toolbarButton, GUILayout.Width(70))) RunAudit();
                if (_issues.Count > 0 && GUILayout.Button("결과 닫기", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    _issues.Clear();
                if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70))) Rescan();
            }
        }

        void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(200)))
            using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll))
            {
                _listScroll = scroll.scrollPosition;

                var filtered = _entries.Where(e => string.IsNullOrEmpty(_search)
                    || e.Name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0);

                string unit = null;
                foreach (var entry in filtered)
                {
                    if (entry.Unit != unit)
                    {
                        unit = entry.Unit;
                        EditorGUILayout.LabelField(unit, EditorStyles.boldLabel);
                    }

                    var style = entry == _selected ? EditorStyles.helpBox : EditorStyles.label;
                    if (GUILayout.Button(entry.Name, style)) Select(entry);
                }
            }
        }

        void DrawStage()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_selected == null)
                {
                    EditorGUILayout.HelpBox("왼쪽 목록에서 스킬을 고르세요.", MessageType.Info);
                    return;
                }

                DrawHeader();

                var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                RenderStage(rect);

                DrawTimeBar();
                DrawLayerSummary();
            }
        }

        void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(_selected.Skill.displayName ?? _selected.Name, EditorStyles.boldLabel,
                    GUILayout.Width(160));

                EditorGUI.BeginChangeCheck();
                var side = GUILayout.Toolbar(_impactSide ? 1 : 0, new[] { "시전", "피격" }, GUILayout.Width(120));
                if (EditorGUI.EndChangeCheck())
                {
                    _impactSide = side == 1;
                    Rebuild();
                }

                var spec = CurrentSpec();
                using (new EditorGUI.DisabledScope(spec == null))
                    if (GUILayout.Button("스펙 열기", GUILayout.Width(80)))
                        Selection.activeObject = spec;

                GUILayout.FlexibleSpace();
            }

            if (CurrentSpec() == null)
                EditorGUILayout.HelpBox($"이 스킬은 {(_impactSide ? "피격" : "시전")} 이펙트가 없다.", MessageType.None);
        }

        void DrawTimeBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("시점", EditorStyles.miniLabel, GUILayout.Width(28));
                EditorGUI.BeginChangeCheck();
                _time = GUILayout.HorizontalSlider(_time, 0f, MaxTime, GUILayout.Width(180));
                if (EditorGUI.EndChangeCheck()) Rebuild();
                GUILayout.Label($"{_time:0.00}s", EditorStyles.miniLabel, GUILayout.Width(44));

                foreach (var preset in BurstTimes) DrawPresetButton(preset);
                GUILayout.Space(6);
                foreach (var preset in HoldTimes) DrawPresetButton(preset);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _autoPlay = GUILayout.Toggle(_autoPlay, "자동 진행", EditorStyles.miniButton, GUILayout.Width(70));
                if (EditorGUI.EndChangeCheck() && !_autoPlay) Rebuild();

                GUILayout.Label("속도", EditorStyles.miniLabel, GUILayout.Width(28));
                _autoSpeed = GUILayout.HorizontalSlider(_autoSpeed, 0.1f, 2f, GUILayout.Width(100));
                GUILayout.Label($"{_autoSpeed:0.0}x", EditorStyles.miniLabel, GUILayout.Width(36));

                if (GUILayout.Button("처음부터", GUILayout.Width(70)))
                {
                    _time = 0f;
                    Rebuild();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("가운데 사각형이 배틀러 한 명 크기(가로 3 · 세로 4)다.", EditorStyles.miniLabel);
            }
        }

        void DrawPresetButton(float preset)
        {
            var on = Mathf.Abs(_time - preset) < 0.005f;
            var style = on ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
            if (!GUILayout.Button($"{preset:0.0}", style, GUILayout.Width(34))) return;
            _time = preset;
            Rebuild();
        }

        void DrawLayerSummary()
        {
            var spec = CurrentSpec();
            if (spec?.layers == null) return;

            for (var i = 0; i < spec.layers.Count; i++)
            {
                var layer = spec.layers[i];
                var hold = layer.holdTurns > 0 ? $" · 유지({layer.holdMode})" : string.Empty;
                var repeat = layer.repeatCount > 1 ? $" · {layer.repeatCount}회 {layer.repeatInterval:0.00}s" : string.Empty;
                EditorGUILayout.LabelField(
                    $"{i + 1}. {(layer.prefab != null ? layer.prefab.name : "(비었음)")} · {layer.anchor} · "
                    + $"배율 {layer.scale:0.##} · 지연 {layer.startDelay:0.00}s · 정렬 {layer.sortingOrder}{repeat}{hold}",
                    EditorStyles.miniLabel);
            }
        }

        void RenderStage(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            EnsureStage();

            _preview.BeginPreview(rect, GUIStyle.none);
            _preview.camera.Render();
            var texture = _preview.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        void DrawIssues()
        {
            EditorGUILayout.LabelField($"검사 결과 {_issues.Count}건", EditorStyles.boldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_issueScroll, GUILayout.Height(140)))
            {
                _issueScroll = scroll.scrollPosition;
                foreach (var issue in _issues) EditorGUILayout.LabelField(issue, EditorStyles.miniLabel);
            }
        }
    }
}
