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

        bool _twoD;
        bool _paused;
        Vector3 _spin; // 이펙트에 입혀 보는 회전. 정한 값을 VfxLayer.rotation에 그대로 옮긴다.
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
            if (!_paused)
            {
                _time += delta;
                _pendingDelta += delta;
                if (_time > _loopLength) Restart();
            }

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
                var flat = GUILayout.Toggle(_twoD, "2D", EditorStyles.toolbarButton, GUILayout.Width(34));
                if (flat != _twoD) SetTwoD(flat);
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
                        $"화면 왜곡 파티클 {_warpRendererCount}개가 임포트 때 꺼졌습니다. 2D 렌더러가 화면 색을 주지 않아 켜 두면 회색 판으로 보입니다.",
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
                    GUILayout.Label("시점", EditorStyles.miniLabel, GUILayout.Width(28));
                    EditorGUI.BeginChangeCheck();
                    var seek = GUILayout.HorizontalSlider(_time, 0f, _loopLength, GUILayout.Width(150));
                    // 손을 떼자마자 다시 흘러가면 잡은 순간을 볼 수 없다.
                    if (EditorGUI.EndChangeCheck())
                    {
                        _paused = true;
                        SeekTo(seek);
                    }

                    GUILayout.Label($"{_time:0.00}s / {_loopLength:0.00}s", EditorStyles.miniLabel, GUILayout.Width(90));
                    if (GUILayout.Button(_paused ? "재생" : "멈춤", GUILayout.Width(50))) _paused = !_paused;
                    if (GUILayout.Button("처음부터", GUILayout.Width(70))) Restart();
                    if (GUILayout.Button("정면", GUILayout.Width(50)))
                    {
                        _yaw = 0f;
                        _pitch = 0f;
                        FrameInstance();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("이펙트 회전", EditorStyles.miniLabel, GUILayout.Width(64));
                    EditorGUI.BeginChangeCheck();
                    _spin = EditorGUILayout.Vector3Field(GUIContent.none, _spin, GUILayout.Width(230));
                    if (GUILayout.Button("세우기", GUILayout.Width(56))) _spin = new Vector3(-90f, 0f, 0f);
                    if (GUILayout.Button("되돌리기", GUILayout.Width(66))) _spin = Vector3.zero;
                    // 눕힌 이펙트는 화면 밖으로 밀려난다. 각도를 건드릴 때마다 다시 잡아 준다.
                    if (EditorGUI.EndChangeCheck() && _instance != null)
                    {
                        _instance.transform.rotation = Quaternion.Euler(_spin);
                        FrameInstance();
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label("이 값을 VfxLayer.rotation에 그대로 넣는다.", EditorStyles.miniLabel);
                }

                EditorGUILayout.LabelField(
                    _twoD
                        ? "Shift+드래그 이펙트 회전 · 드래그 이동 · 휠 확대 · F 맞춤"
                        : "Shift+드래그 이펙트 회전 · 좌드래그 시야 회전 · 휠클릭 이동 · 우드래그 시점(+WASD/QE) · 휠 확대 · F 맞춤",
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

        // 직교에서는 물러선 거리가 크기를 바꾸지 않는다. 앞뒤로 긴 이펙트가 카메라 뒤로 넘어가 잘리지 않게 넉넉히 뺀다.
        Vector3 CameraPosition
            => _pivot - CameraRotation * Vector3.forward * (_twoD ? Mathf.Max(_distance, 250f) : _distance);

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

        // 정면 직교로 전환한다. 각도를 0으로 눕혀야 이동·플라이스루가 쓰는 CameraRotation이 화면 축과 맞는다.
        void SetTwoD(bool on)
        {
            _twoD = on;
            _yaw = 0f;
            _pitch = 0f;
            // 앞서 돌리고 끌던 피벗과 거리를 그대로 두면 전환한 화면에 이펙트가 엉뚱한 자리에 선다.
            if (_instance != null) FrameInstance();
        }

        void ApplyDrag(Event e)
        {
            switch (_dragButton)
            {
                case 0 when e.shift:
                    Spin(e.delta);
                    break;
                case 1 when e.alt:
                    Zoom(-e.delta.x - e.delta.y);
                    break;
                case 1 when !_twoD:
                    LookAround(e.delta);
                    break;
                case 2:
                case 1:
                    Pan(e.delta);
                    break;
                default:
                    if (_twoD) Pan(e.delta);
                    else Orbit(e.delta);
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

        // 화면 축으로 돌린다. 가로 드래그가 세로축, 세로 드래그가 가로축이다. 결과 각도는 위 필드에 그대로 뜬다.
        void Spin(Vector2 delta)
        {
            _spin.y += delta.x * 0.5f;
            _spin.x += delta.y * 0.5f;
            if (_instance != null) _instance.transform.rotation = Quaternion.Euler(_spin);
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
            _warpRendererCount = CountScreenWarpRenderers(_instance);
            _loopLength = MeasureLoopLength();
            FrameInstance();
            Restart();
        }

        // 끄는 것은 임포트가 이미 했다. 여기서는 안내 문구에 쓸 개수만 센다.
        static int CountScreenWarpRenderers(GameObject root)
        {
            var count = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!ScreenWarpFilter.IsWarpOnly(renderer.sharedMaterial)) continue;
                count++;
            }

            return count;
        }

        void Restart() => SeekTo(0f);

        /// <summary>원하는 시점으로 건너뛴다. 뒤로 가려면 처음부터 다시 굴리는 수밖에 없어 매번 다시 세운다.</summary>
        void SeekTo(float time)
        {
            _time = Mathf.Clamp(time, 0f, _loopLength);
            _pendingDelta = 0f;
            foreach (var ps in _particles) ps.Simulate(_time, false, true, false);

            foreach (var animator in _animators)
            {
                var controller = animator.runtimeAnimatorController;
                if (controller == null) continue;
                var clips = controller.animationClips;
                var length = clips is { Length: > 0 } ? Mathf.Max(clips[0].length, 0.01f) : 1f;
                animator.Play(0, 0, _time / length);
                animator.Update(0f);
            }
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

            // 정면 직교에서는 깊이가 화면에 안 보인다. 앞뒤로 긴 이펙트까지 세면 쓸데없이 멀리 물러선다.
            var size = !found ? 2f
                : _twoD ? Mathf.Max(bounds.size.x, bounds.size.y)
                : bounds.size.magnitude;
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

            _instance.transform.rotation = Quaternion.Euler(_spin);
            _preview.camera.orthographic = _twoD;
            // 직교에서는 거리가 화면 크기를 바꾸지 않으므로, 같은 손잡이로 줌이 되게 시야 높이로 옮겨 준다.
            if (_twoD) _preview.camera.orthographicSize = Mathf.Max(_distance * 0.5f, 0.01f);
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
            instance.transform.rotation = Quaternion.Euler(_spin);

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
