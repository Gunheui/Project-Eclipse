using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Eclipse.EditorTools
{
    /// <summary>프로젝트의 이펙트 프리팹을 한 창에서 검색·재생·배치하는 브라우저.</summary>
    public sealed class VfxBrowserWindow : EditorWindow
    {
        sealed class Entry
        {
            public string Path;
            public string Name;
            public string Pack;
            public GameObject Asset;
        }

        // 화면 색을 읽어 일그러뜨리는 셰이더들. 프리뷰 카메라는 2D Renderer의 Camera Sorting Layer Texture 패스를
        // 타지 않아 항상 회색 판으로 렌더된다 — 실제 씬은 그 설정이 켜져 있으면 정상이다.
        // ponytail: 셰이더 이름 하드코딩 — Scene Color 사용 여부는 셰이더 그래프 밖에서 알 수 없다. 새 팩 추가 시 여기에 더한다.
        static readonly HashSet<string> ScreenWarpShaders = new()
        {
            "Shader Graphs/Slash World",
            "Shader Graphs/Orb Warp",
            "Shader Graphs/Orb Warp Lit",
            "GAP_SG/ParallaxOcclusion",
        };

        readonly List<Entry> _entries = new();
        string _search = string.Empty;
        Vector2 _listScroll;
        Entry _selected;

        PreviewRenderUtility _preview;
        GameObject _instance;
        ParticleSystem[] _particles = System.Array.Empty<ParticleSystem>();
        Animator[] _animators = System.Array.Empty<Animator>();
        int _warpRendererCount;
        float _time;
        float _pendingDelta;
        float _loopLength = 1f;
        double _lastTick;

        float _yaw = 20f;
        float _pitch = 10f;
        float _baseDistance = 6f; // 이펙트 크기에 맞춰 자동 계산한 기준 거리 (맞춤 버튼의 기준)
        float _distance = 6f;
        Vector3 _pivot;
        int _dragButton = -1;

        [MenuItem("Eclipse/VFX Browser")]
        static void Open() => GetWindow<VfxBrowserWindow>("VFX 브라우저").minSize = new Vector2(720, 440);

        void OnEnable()
        {
            Rescan();
            _lastTick = EditorApplication.timeSinceStartup;
        }

        void OnDisable() => ReleasePreview();

        // 프리뷰는 창이 계속 다시 그려져야 시간이 흐른다.
        void Update()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = Mathf.Clamp((float)(now - _lastTick), 0f, 0.1f);
            _lastTick = now;

            if (_instance == null) return;
            _time += delta;
            _pendingDelta += delta;
            if (_time > _loopLength) Restart();
            Repaint();
        }

        void Rescan()
        {
            _entries.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                // 파티클 또는 Animator 기반(메시 슬래시 등) 프리팹만 이펙트로 본다.
                if (go.GetComponentInChildren<ParticleSystem>(true) == null &&
                    go.GetComponentInChildren<Animator>(true) == null) continue;

                var segments = path.Split('/');
                _entries.Add(new Entry
                {
                    Path = path,
                    Name = go.name,
                    Pack = segments.Length > 1 ? segments[1] : "(root)",
                    Asset = go,
                });
            }

            _entries.Sort((a, b) => string.CompareOrdinal(a.Pack + a.Name, b.Pack + b.Name));
        }

        void OnGUI()
        {
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                DrawPreview();
            }
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(240));
                GUILayout.Label($"{_entries.Count}개", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70))) Rescan();
            }
        }

        void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(280)))
            using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll))
            {
                _listScroll = scroll.scrollPosition;

                var filtered = _entries.Where(e =>
                    string.IsNullOrEmpty(_search) ||
                    e.Name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.Pack.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0);

                string pack = null;
                foreach (var entry in filtered)
                {
                    if (entry.Pack != pack)
                    {
                        pack = entry.Pack;
                        EditorGUILayout.LabelField(pack, EditorStyles.boldLabel);
                    }

                    var style = entry == _selected ? EditorStyles.helpBox : EditorStyles.label;
                    if (GUILayout.Button(entry.Name, style)) Select(entry);
                }
            }
        }

        void DrawPreview()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_selected == null)
                {
                    EditorGUILayout.HelpBox("왼쪽 목록에서 이펙트를 선택하세요.", MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField(_selected.Name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_selected.Path, EditorStyles.miniLabel);

                if (_warpRendererCount > 0)
                    EditorGUILayout.HelpBox(
                        ScreenWarpSupported()
                            ? $"화면 왜곡 파티클 {_warpRendererCount}개는 프리뷰에서만 숨겨집니다. 씬에서는 정상 재생됩니다."
                            : $"화면 왜곡 파티클 {_warpRendererCount}개를 숨겼습니다. Renderer2D의 Camera Sorting Layer Texture가 꺼져 있어 씬에서도 회색 판으로 보이므로, 배치할 때도 함께 끕니다.",
                        MessageType.Info);

                var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                HandleNavigation(rect);
                RenderPreview(rect);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("확대", EditorStyles.miniLabel, GUILayout.Width(28));
                    var zoom = _baseDistance / Mathf.Max(_distance, 0.001f);
                    var moved = GUILayout.HorizontalSlider(zoom, 0.1f, 10f, GUILayout.Width(140));
                    if (!Mathf.Approximately(moved, zoom)) _distance = _baseDistance / Mathf.Max(moved, 0.01f);
                    GUILayout.Label($"{zoom:0.0}x", EditorStyles.miniLabel, GUILayout.Width(34));
                    if (GUILayout.Button("맞춤 (F)", GUILayout.Width(60))) FrameInstance();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{_time:0.00}s / {_loopLength:0.00}s", EditorStyles.miniLabel, GUILayout.Width(90));
                    if (GUILayout.Button("처음부터", GUILayout.Width(70))) Restart();
                    if (GUILayout.Button("정면", GUILayout.Width(50)))
                    {
                        _yaw = 0f;
                        _pitch = 0f;
                        FrameInstance();
                    }
                }

                EditorGUILayout.LabelField(
                    "좌드래그 회전 · 휠클릭 이동 · 우드래그 시점(+WASD/QE) · 휠 확대 · F 맞춤",
                    EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("씬에 배치")) Place(null);

                    var parent = Selection.activeGameObject;
                    using (new EditorGUI.DisabledScope(parent == null))
                        if (GUILayout.Button(parent == null ? "선택 오브젝트에 붙이기" : $"'{parent.name}'에 붙이기"))
                            Place(parent.transform);
                }
            }
        }

        Quaternion CameraRotation => Quaternion.Euler(_pitch, _yaw, 0f);

        Vector3 CameraPosition => _pivot - CameraRotation * Vector3.forward * _distance;

        // Scene 뷰와 같은 손버릇: 좌드래그 궤도회전 · 휠클릭 이동 · 우드래그 시점회전(+WASDQE) · 휠 확대 · F 맞춤
        void HandleNavigation(Rect rect)
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when rect.Contains(e.mousePosition):
                    _dragButton = e.button;
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    e.Use();
                    break;

                case EventType.MouseUp when _dragButton == e.button:
                    _dragButton = -1;
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;

                case EventType.MouseDrag when _dragButton >= 0:
                    ApplyDrag(e);
                    e.Use();
                    break;

                case EventType.ScrollWheel when rect.Contains(e.mousePosition):
                    Zoom(e.delta.y);
                    e.Use();
                    break;

                case EventType.KeyDown when rect.Contains(e.mousePosition) || _dragButton == 1:
                    if (HandleKey(e.keyCode)) e.Use();
                    break;
            }
        }

        void ApplyDrag(Event e)
        {
            switch (_dragButton)
            {
                case 1 when e.alt:
                    Zoom(-e.delta.x - e.delta.y);
                    break;
                case 1:
                    LookAround(e.delta);
                    break;
                case 2:
                    Pan(e.delta);
                    break;
                default:
                    Orbit(e.delta);
                    break;
            }
        }

        bool HandleKey(KeyCode key)
        {
            if (key == KeyCode.F)
            {
                FrameInstance();
                return true;
            }

            // Scene 뷰의 플라이스루. 우드래그로 시점을 돌리는 동안에만 먹는다.
            if (_dragButton != 1) return false;

            var direction = key switch
            {
                KeyCode.W => Vector3.forward,
                KeyCode.S => Vector3.back,
                KeyCode.A => Vector3.left,
                KeyCode.D => Vector3.right,
                KeyCode.E => Vector3.up,
                KeyCode.Q => Vector3.down,
                _ => Vector3.zero,
            };
            if (direction == Vector3.zero) return false;

            _pivot += CameraRotation * direction * (_distance * 0.06f);
            return true;
        }

        void Orbit(Vector2 delta)
        {
            _yaw += delta.x * 0.3f;
            _pitch = Mathf.Clamp(_pitch + delta.y * 0.3f, -89f, 89f);
        }

        // 카메라 위치는 두고 바라보는 방향만 돌린다 → 피벗을 새 방향 앞으로 다시 놓는다.
        void LookAround(Vector2 delta)
        {
            var position = CameraPosition;
            _yaw += delta.x * 0.3f;
            _pitch = Mathf.Clamp(_pitch + delta.y * 0.3f, -89f, 89f);
            _pivot = position + CameraRotation * Vector3.forward * _distance;
        }

        void Pan(Vector2 delta)
        {
            _pivot -= CameraRotation * new Vector3(delta.x, -delta.y, 0f) * (_distance * 0.0025f);
        }

        // 거리에 비례해 줌해야 크기가 제각각인 이펙트에서 한 번의 조작 체감이 같다.
        void Zoom(float amount)
        {
            _distance = Mathf.Clamp(_distance * (1f + amount * 0.03f), 0.05f, 500f);
        }

        void Select(Entry entry)
        {
            _selected = entry;
            EditorGUIUtility.PingObject(entry.Asset);
            SpawnPreviewInstance();
        }

        void SpawnPreviewInstance()
        {
            DestroyInstance();
            _preview ??= CreatePreviewUtility();

            _instance = Instantiate(_selected.Asset);
            _instance.hideFlags = HideFlags.HideAndDontSave;
            _instance.SetActive(true); // 비활성 상태로 저장된 프리팹은 Simulate가 먹지 않는다.
            _preview.AddSingleGO(_instance);

            _particles = _instance.GetComponentsInChildren<ParticleSystem>(true);
            _animators = _instance.GetComponentsInChildren<Animator>(true);
            _warpRendererCount = DisableScreenWarpRenderers(_instance, true);
            _loopLength = MeasureLoopLength();
            FrameInstance();
            Restart();
        }

        // 2D Renderer가 화면 색 텍스처를 제공하는 유일한 경로. 꺼져 있으면 왜곡 셰이더는 씬에서도 회색 판이 된다.
        static bool ScreenWarpSupported()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return true;

            var rendererData = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            if (rendererData == null || rendererData.arraySize == 0) return true;

            var renderer = rendererData.GetArrayElementAtIndex(0).objectReferenceValue;
            if (renderer == null) return true;

            var flag = new SerializedObject(renderer).FindProperty("m_UseCameraSortingLayersTexture");
            return flag == null || flag.boolValue; // 2D Renderer가 아니면 프로퍼티 자체가 없다
        }

        static int DisableScreenWarpRenderers(GameObject root, bool hide)
        {
            var count = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (material == null || material.shader == null) continue;
                if (!ScreenWarpShaders.Contains(material.shader.name)) continue;
                renderer.enabled = !hide;
                count++;
            }

            return count;
        }

        void Restart()
        {
            _time = 0f;
            _pendingDelta = 0f;
            foreach (var ps in _particles) ps.Simulate(0f, false, true, false);
            foreach (var animator in _animators)
                if (animator.runtimeAnimatorController != null)
                    animator.Play(0, 0, 0f);
        }

        // 파티클은 t=0에 아무것도 없어 크기를 알 수 없다. 중간 시점까지 시뮬레이션한 렌더러 bounds로 카메라 거리를 잡는다.
        void FrameInstance()
        {
            foreach (var ps in _particles) ps.Simulate(Mathf.Min(_loopLength * 0.5f, 2f), false, true, false);

            var bounds = new Bounds();
            var found = false;
            foreach (var renderer in _instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else
                {
                    bounds = renderer.bounds;
                    found = true;
                }
            }

            var size = found ? bounds.size.magnitude : 2f;
            _baseDistance = Mathf.Clamp(size * 1.6f, 2f, 80f);
            _distance = _baseDistance;
            _pivot = found ? bounds.center : Vector3.up * 0.5f;
        }

        static PreviewRenderUtility CreatePreviewUtility()
        {
            var preview = new PreviewRenderUtility();
            preview.camera.cameraType = CameraType.Preview;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            preview.camera.nearClipPlane = 0.05f;
            preview.camera.farClipPlane = 500f;
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            preview.lights[1].intensity = 0.8f;
            return preview;
        }

        // 원샷 이펙트를 자동 반복하려면 가장 늦게 끝나는 파티클/애니메이션 길이가 필요하다.
        float MeasureLoopLength()
        {
            var length = 0f;
            foreach (var ps in _particles)
            {
                var main = ps.main;
                length = Mathf.Max(length, main.duration + main.startLifetime.constantMax + main.startDelay.constantMax);
            }

            foreach (var animator in _animators)
            {
                var clips = animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.animationClips
                    : null;
                if (clips == null) continue;
                foreach (var clip in clips) length = Mathf.Max(length, clip.length);
            }

            return Mathf.Clamp(length, 1f, 20f);
        }

        void RenderPreview(Rect rect)
        {
            if (Event.current.type != EventType.Repaint || _instance == null || _preview == null) return;

            _preview.camera.transform.rotation = CameraRotation;
            _preview.camera.transform.position = CameraPosition;

            // 프리뷰 씬은 자동으로 갱신되지 않으므로 경과 시간을 직접 먹인다. 매번 0초부터 다시 돌리면 시간이 갈수록 무거워지므로 증분으로 진행한다.
            var delta = _pendingDelta;
            _pendingDelta = 0f;
            if (delta > 0f)
            {
                foreach (var ps in _particles) ps.Simulate(delta, false, false, true);
                foreach (var animator in _animators)
                    if (animator.runtimeAnimatorController != null)
                        animator.Update(delta);
            }

            _preview.BeginPreview(rect, GUIStyle.none);
            _preview.camera.Render();
            var texture = _preview.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        void Place(Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(_selected.Asset);
            Undo.RegisterCreatedObjectUndo(instance, $"Place {_selected.Name}");
            if (!ScreenWarpSupported()) DisableScreenWarpRenderers(instance, true);

            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
                instance.transform.localPosition = Vector3.zero;
            }
            else
            {
                instance.transform.position = SceneView.lastActiveSceneView != null
                    ? SceneView.lastActiveSceneView.pivot
                    : Vector3.zero;
            }

            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        void DestroyInstance()
        {
            if (_instance != null) DestroyImmediate(_instance);
            _instance = null;
            _particles = System.Array.Empty<ParticleSystem>();
            _animators = System.Array.Empty<Animator>();
        }

        void ReleasePreview()
        {
            DestroyInstance();
            _preview?.Cleanup();
            _preview = null;
        }
    }
}
