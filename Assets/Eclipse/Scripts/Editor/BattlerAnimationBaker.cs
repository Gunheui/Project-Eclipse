using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Eclipse.EditorTools
{
    public static class BattlerAnimationBaker
    {
        private const string FrameRoot = "Assets/Eclipse/Art/BattlerAnim";
        private const string ClipDir = FrameRoot + "/Clips";
        private const string ControllerDir = FrameRoot + "/Controllers";
        private const string SharedControllerPath = ControllerDir + "/Battler.controller";
        private const string AtlasPath = FrameRoot + "/BattlerAnim.spriteatlasv2";

        /// <summary>
        /// 유닛마다 굽는 모션 넷. 프레임 파일명의 접미가 곧 조회 키이고, 이 순서가 클립 배열과
        /// 컨트롤러 상태 순서를 함께 정한다. <c>ReturnsToIdle</c>은 다 돌고 대기로 복귀할지다 —
        /// 사망만 마지막 자세에서 멈춘다.
        /// </summary>
        private static readonly (string Suffix, float Fps, bool Loop, bool ReturnsToIdle)[] Motions =
        {
            ("_Idle", 24f, true, false),
            ("_Attack", 24f, false, true),
            ("_Hit", 24f, false, true),
            ("_Dead", 24f, false, false),
        };

        // 이름으로 찾아 쓰는 두 자리. 발 라인 기준과 타격 시점 환산이 이 둘만 콕 집어 읽는다.
        private const int IdleMotion = 0;
        private const int AttackMotion = 1;

        // 캔버스 크기와 PPU는 짝이다. 512/227과 256/113.5가 같은 2.256유닛이라 어느 쪽이든 화면 크기가 같다.
        private const float FramePixelsPerUnit = 227f;
        private const int FrameMaxSize = 512;

        private const byte AlphaThreshold = 24;

        // 프레임을 내보낼 때 딸려 온 어두운 배경 잔여물의 알파가 30대다. 눈에는 거의 안 띄지만
        // 타이트 메시를 캔버스 한 칸으로 넓혀 발 라인·HP바 앵커·탭 영역을 밀고, 아웃라인 팽창이
        // 그걸 물어 박스를 그린다. 이 값 미만은 완전 투명으로 지운다.
        private const byte MinFrameAlpha = 48;

        // 프레임이 512라 2048 시트에는 넉 장 남짓만 들어간다. 시트 수가 네 배로 늘면 배칭이 그만큼 끊긴다.
        // 4모션 856장 기준 4096 시트 7장에 자투리 하나, ASTC 6x6으로 101MB다. 줄여야 하면
        // FrameMaxSize와 FramePixelsPerUnit을 짝으로 낮추는 것이 화면 크기를 유지하는 유일한 손잡이다.
        private const int AtlasMaxSize = 4096;
        private const int AtlasPadding = 8;

        /// <summary>
        /// 유닛 12종. <c>StandingPath</c>는 애니 도입 전의 정지 그림으로, 발 라인을 맞출 기준이다.
        /// 이 기준을 SO의 현재 값에서 읽으면 첫 베이크가 그 값을 애니 프레임으로 바꿔 놓아
        /// 다음 베이크가 자기 자신을 기준 삼고, 해상도를 바꿀 때마다 발 라인이 조금씩 밀린다.
        /// <c>ImpactFrame</c>은 공격 클립에서 무기가 닿는 프레임 번호(0부터)다. 클립이 13~26프레임으로
        /// 유닛마다 두 배 차이라 비율 하나로는 못 맞춘다.
        /// </summary>
        private static readonly (string Folder, string DefinitionPath, string StandingPath, int ImpactFrame)[] Units =
        {
            ("Arin", "Assets/Eclipse/GameData/Characters/Arin.asset", "Assets/Eclipse/Art/Battlers/Arin.png", 6),
            ("Eliana", "Assets/Eclipse/GameData/Characters/Eliana.asset", "Assets/Eclipse/Art/Battlers/Eliana.png", 11),
            ("Kael", "Assets/Eclipse/GameData/Characters/Kael.asset", "Assets/Eclipse/Art/Battlers/Kael.png", 6),
            ("Ria", "Assets/Eclipse/GameData/Characters/Ria.asset", "Assets/Eclipse/Art/Battlers/Ria.png", 4),
            ("Selene", "Assets/Eclipse/GameData/Characters/Selene.asset", "Assets/Eclipse/Art/Battlers/Selene.png", 7),
            ("Adventurer", "Assets/Eclipse/GameData/Enemies/Swordsman.asset", "Assets/Eclipse/Art/Enemies/Adventurer.png", 8),
            ("Barkan", "Assets/Eclipse/GameData/Enemies/Barkan.asset", "Assets/Eclipse/Art/Enemies/Barkan.png", 10),
            ("Mirea", "Assets/Eclipse/GameData/Enemies/Mirea.asset", "Assets/Eclipse/Art/Enemies/Mirea.png", 5),
            ("Plant", "Assets/Eclipse/GameData/Enemies/Blossom.asset", "Assets/Eclipse/Art/Enemies/Plant.png", 9),
            ("Slime", "Assets/Eclipse/GameData/Enemies/Slime.asset", "Assets/Eclipse/Art/Enemies/Slime.png", 10),
            ("Spider", "Assets/Eclipse/GameData/Enemies/Spider.asset", "Assets/Eclipse/Art/Enemies/Spider.png", 15),
            ("Wolf", "Assets/Eclipse/GameData/Enemies/Hound.asset", "Assets/Eclipse/Art/Enemies/Wolf.png", 8),
        };

        [MenuItem("Eclipse/배틀러 애니메이션/1. 프레임 임포트 세팅")]
        public static void ApplyImportSettings()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var unit in Units)
                {
                    // 발 라인을 재기 전에 지운다. 잔여 배경이 남아 있으면 그 밑변을 발로 잡는다.
                    int stripped = FramePaths(unit.Folder).Count(StripBackdropAlpha);
                    float pivotY = ResolvePivotY(unit.Folder, unit.StandingPath);
                    foreach (string path in FramePaths(unit.Folder))
                        ApplyFrameSettings(path, pivotY);
                    Debug.Log($"[BattlerAnim] {unit.Folder} 피벗 y={pivotY:F4} · 배경 잔여 정리 {stripped}장");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("Eclipse/배틀러 애니메이션/2. 클립·컨트롤러·아틀라스·SO 굽기")]
        public static void Bake()
        {
            Directory.CreateDirectory(ClipDir);
            Directory.CreateDirectory(ControllerDir);
            AssetDatabase.Refresh();

            var clips = Units.ToDictionary(u => u.Folder, u => BakeUnitClips(u.Folder));

            var shared = BuildSharedController(clips[Units[0].Folder]);
            foreach (var unit in Units)
            {
                var aoc = LoadOrCreateOverride(unit.Folder, shared, clips[unit.Folder]);
                WireDefinition(unit, aoc, clips[unit.Folder]);
            }

            BuildAtlas();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BattlerAnim] 클립 {clips.Count * Motions.Length}개 · 오버라이드 {clips.Count}개 · 아틀라스 완료");
        }

        // ── 임포트 ─────────────────────────────────────────────────────────────

        private static void ApplyFrameSettings(string path, float pivotY)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = FramePixelsPerUnit;
            importer.maxTextureSize = FrameMaxSize;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            // 압축은 아틀라스가 맡는다. 소스까지 압축하면 이미 뭉갠 픽셀을 다시 뭉쳐 넣는다.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            // 실루엣 계산이 타이트 메시 정점을 읽는다. Full Rect로 두면 탭 영역과 앵커가 캔버스 전체로 잡힌다.
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, pivotY);
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        /// <summary>
        /// 내보내기 잔여 배경을 지운다. 기준 미만 알파를 완전 투명으로 만들어 파일에 다시 쓴다.
        /// </summary>
        /// <returns>지울 픽셀이 있어 파일을 고쳐 쓴 경우 true. 이미 깨끗하면 파일을 건드리지 않는다.</returns>
        private static bool StripBackdropAlpha(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            texture.LoadImage(File.ReadAllBytes(path));
            var pixels = texture.GetPixels32();

            bool changed = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0 || pixels[i].a >= MinFrameAlpha) continue;
                pixels[i].a = 0;
                changed = true;
            }

            if (changed)
            {
                texture.SetPixels32(pixels);
                texture.Apply(updateMipmaps: false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }

            Object.DestroyImmediate(texture);
            return changed;
        }

        /// <summary>
        /// 애니 프레임의 발 라인을 정지 그림과 같은 높이에 세우는 피벗 y를 구한다.
        /// </summary>
        /// <returns>캔버스 높이에 대한 비율. 정지 그림이 없으면 애니 프레임의 발 라인을 그대로 쓴다.</returns>
        private static float ResolvePivotY(string folder, string standingPath)
        {
            string idlePath = FramePaths(folder).First(p => p.Contains(Motions[IdleMotion].Suffix));
            float animFoot = AlphaBottomRatio(idlePath, out int animHeight);

            var standing = AssetDatabase.LoadAssetAtPath<Sprite>(standingPath);
            if (standing == null) return animFoot;

            // 정지 그림에서 발이 원점보다 얼마나 아래인지(월드 단위). 피벗은 비율이라 캔버스 높이를 곱해 환산한다.
            float standingFoot = AlphaBottomRatio(standingPath, out _);
            float standingPivot = standing.pivot.y / standing.rect.height;
            float footOffset = (standingFoot - standingPivot) * standing.rect.height / standing.pixelsPerUnit;

            return animFoot - footOffset * FramePixelsPerUnit / animHeight;
        }

        /// <summary>
        /// 그림에서 알파가 처음 잡히는 줄의 높이를 캔버스 아래에서부터의 비율로 잰다.
        /// </summary>
        /// <returns>불투명한 픽셀이 하나도 없으면 0.</returns>
        private static float AlphaBottomRatio(string assetPath, out int height)
        {
            // 임포트본은 최대 크기로 줄어들 수 있어 원본 파일을 직접 읽는다.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            texture.LoadImage(File.ReadAllBytes(assetPath));
            var pixels = texture.GetPixels32();
            int width = texture.width;
            height = texture.height;
            Object.DestroyImmediate(texture);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (pixels[y * width + x].a >= AlphaThreshold)
                        return (float)y / height;
            return 0f;
        }

        // ── 클립·컨트롤러 ──────────────────────────────────────────────────────

        private static AnimationClip[] BakeUnitClips(string folder)
            => Motions
                .Select(m => BakeClip(folder + m.Suffix, LoadFrames(folder, m.Suffix), m.Fps, m.Loop))
                .ToArray();

        /// <summary>스프라이트 시퀀스를 클립 하나로 굽는다. 이미 있으면 커브만 갈아 끼운다.</summary>
        private static AnimationClip BakeClip(string name, Sprite[] frames, float fps, bool loop)
        {
            string path = $"{ClipDir}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.frameRate = fps;
            // 스프라이트만 키잉한다. 색·활성·좌우반전까지 실리면 조준 dim·사망 숨김·아군 뒤집기와 싸운다.
            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            // 참조 커브는 Unity가 클립 끝을 마지막 키보다 한 프레임 뒤로 잡아 준다. 끝에 같은 그림을
            // 한 번 더 찍으면 그 그림만 두 칸을 차지한다.
            var keys = frames
                .Select((sprite, i) => new ObjectReferenceKeyframe { time = i / fps, value = sprite })
                .ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>
        /// 12종이 공유하는 컨트롤러를 매번 새로 짓는다. 배속은 런타임에서 animator.speed로 건다.
        /// 남은 자산을 고쳐 쓰지 않는 이유는 모션이 늘어도 옛 상태 집합이 그대로 남기 때문이다.
        /// </summary>
        /// <param name="baseClips">덮어쓸 자리를 잡아 줄 기준 모션. 유닛별 오버라이드가 이 자리를 갈아 끼운다.</param>
        private static AnimatorController BuildSharedController(AnimationClip[] baseClips)
        {
            AssetDatabase.DeleteAsset(SharedControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(SharedControllerPath);

            var machine = controller.layers[0].stateMachine;
            var states = Motions
                .Select((motion, i) =>
                {
                    var state = machine.AddState(StateName(motion.Suffix));
                    state.motion = baseClips[i];
                    return state;
                })
                .ToArray();

            machine.defaultState = states[IdleMotion];

            for (int i = 0; i < Motions.Length; i++)
            {
                if (!Motions[i].ReturnsToIdle) continue;
                var back = states[i].AddTransition(states[IdleMotion]);
                back.hasExitTime = true;
                back.exitTime = 1f;
                back.hasFixedDuration = true;
                back.duration = 0f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static string StateName(string motionSuffix) => motionSuffix.TrimStart('_');

        /// <summary>유닛 하나의 오버라이드 컨트롤러. 기준 자리에 그 유닛 클립을 끼운다.</summary>
        private static AnimatorOverrideController LoadOrCreateOverride(string folder, AnimatorController shared,
            AnimationClip[] clips)
        {
            string path = $"{ControllerDir}/{folder}.overrideController";
            var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (aoc == null)
            {
                aoc = new AnimatorOverrideController();
                AssetDatabase.CreateAsset(aoc, path);
            }

            aoc.runtimeAnimatorController = shared;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
            aoc.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                var slot = overrides[i].Key;
                int motion = System.Array.FindIndex(Motions, m => slot.name.EndsWith(m.Suffix));
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(slot, clips[motion]);
            }

            aoc.ApplyOverrides(overrides);
            EditorUtility.SetDirty(aoc);
            return aoc;
        }

        // ── 아틀라스·SO ────────────────────────────────────────────────────────

        /// <summary>652장을 아틀라스 하나로 묶는다. 압축을 걸지 않으면 2048 시트가 장당 16MB다.</summary>
        private static void BuildAtlas()
        {
            var atlas = new SpriteAtlasAsset();
            atlas.Add(Units
                .Select(u => AssetDatabase.LoadAssetAtPath<Object>($"{FrameRoot}/{u.Folder}"))
                .ToArray());
            SpriteAtlasAsset.Save(atlas, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath);

            // 패킹·압축은 에셋이 아니라 임포터가 들고 있다. 저장 뒤 한 번 임포트해야 임포터가 생긴다.
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                enableRotation = false,
                // 타이트 패킹은 이웃 스프라이트를 여백 틈에 끼워 넣는다. 아웃라인 셰이더가 그걸 물어 온다.
                enableTightPacking = false,
                padding = AtlasPadding,
                blockOffset = 1,
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
            };
            importer.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "DefaultTexturePlatform",
                maxTextureSize = AtlasMaxSize,
                format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.Compressed,
            });
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 유닛 정의에 컨트롤러를 연결하고, 기준 그림을 대기 첫 프레임으로 갈아 끼운다.
        /// </summary>
        /// <param name="clips">
        /// 이 유닛의 모션 클립. <c>Motions</c>와 순서가 같다. 대기 첫 키의 그림을 기준 그림으로 쓰고,
        /// 공격 프레임 수로 타격 프레임을 검사한다.
        /// </param>
        private static void WireDefinition(
            (string Folder, string DefinitionPath, string StandingPath, int ImpactFrame) unit,
            AnimatorOverrideController aoc, AnimationClip[] clips)
        {
            int frames = SpriteKeys(clips[AttackMotion]).Length;
            if (unit.ImpactFrame < 0 || unit.ImpactFrame >= frames)
                throw new System.InvalidOperationException(
                    $"[BattlerAnim] {unit.Folder} 타격 프레임 {unit.ImpactFrame}이 공격 클립 {frames}프레임을 벗어난다.");

            var definition = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(unit.DefinitionPath));
            definition.FindProperty("battlerAnimator").objectReferenceValue = aoc;
            // 실루엣을 재는 기준이 실제로 보이는 그림이어야 탭 영역·HP바·이펙트 앵커가 어긋나지 않는다.
            definition.FindProperty("battlerAssetRef").objectReferenceValue = FirstFrame(clips[IdleMotion]);
            // 런타임은 프레임 수를 모른다. 여기서 초로 환산해 두면 fps를 바꿔 다시 구워도 값이 따라온다.
            definition.FindProperty("battlerImpactTime").floatValue = unit.ImpactFrame / Motions[AttackMotion].Fps;
            definition.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite FirstFrame(AnimationClip clip) => SpriteKeys(clip)[0].value as Sprite;

        private static ObjectReferenceKeyframe[] SpriteKeys(AnimationClip clip)
        {
            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            return AnimationUtility.GetObjectReferenceCurve(clip, binding);
        }

        // ── 프레임 조회 ────────────────────────────────────────────────────────

        private static IEnumerable<string> FramePaths(string folder)
            => Directory.GetFiles($"{FrameRoot}/{folder}", "*.png").OrderBy(p => p, System.StringComparer.Ordinal);

        /// <summary>한 유닛의 한 동작 프레임을 파일명 순서로 읽는다.</summary>
        private static Sprite[] LoadFrames(string folder, string motionSuffix)
            => FramePaths(folder)
                .Where(p => Path.GetFileNameWithoutExtension(p).Contains(motionSuffix))
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .ToArray();
    }
}
