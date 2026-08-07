using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Eclipse.EditorTools
{
    /// <summary>
    /// 화면 색만 그리는 렌더러를 임포트 시점에 꺼 둔다. 2D 렌더러가 그 색을 공급하지 않아 그대로 두면
    /// 회색 판으로 그려지고, 이펙트를 키우면 화면을 통째로 덮는다.
    /// 어느 팩의 무엇이 그런지는 여기만 알고, 게임 코드는 프리팹을 그냥 띄운다.
    /// </summary>
    public static class ScreenWarpFilter
    {
        // 이 셰이더를 쓰면 머티리얼과 무관하게 왜곡 전용이다.
        private static readonly HashSet<string> WarpOnlyShaders = new()
        {
            "Shader Graphs/Slash World",
            "Shader Graphs/Orb Warp",
            "Shader Graphs/Orb Warp Lit",
            "GAP_SG/ParallaxOcclusion",
        };

        // Zyncope는 셰이더 하나를 이펙트 전 렌더러가 공유하고 _Distort로 왜곡 패스만 가른다.
        private const string ZyncopeShader = "Shader Graphs/zyn_shd_multifeature_cd_v1.0.1";
        private static readonly int DistortId = Shader.PropertyToID("_Distort");

        /// <summary>이 머티리얼이 그리는 것이 화면 색뿐인지.</summary>
        /// <remarks>
        /// 셰이더 이름만으로는 못 가른다 — Zyncope는 한 셰이더를 이펙트 전체가 써서 _Distort까지 봐야 하고,
        /// 반대로 Cartoon FX는 같은 이름의 프로퍼티를 제 텍스처를 흔드는 세기(0.05~0.1)로 쓴다.
        /// 값만 보고 끄면 이펙트 본체가 사라진다.
        /// </remarks>
        public static bool IsWarpOnly(Material material)
        {
            if (material == null || material.shader == null) return false;
            if (WarpOnlyShaders.Contains(material.shader.name)) return true;
            return material.shader.name == ZyncopeShader
                   && material.HasProperty(DistortId)
                   && material.GetFloat(DistortId) >= 1f;
        }

        /// <summary>프리팹 안 왜곡 렌더러를 끄고 저장한다.</summary>
        /// <returns>끈 것이 있으면 true. 없으면 저장하지 않는다 — 저장이 재임포트를 부르므로 루프를 끊는다.</returns>
        public static bool DisableIn(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return false;

            var changed = false;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !IsWarpOnly(renderer.sharedMaterial)) continue;
                renderer.enabled = false;
                changed = true;
            }

            if (changed) PrefabUtility.SavePrefabAsset(prefab);
            return changed;
        }

        /// <summary>이미 들어와 있는 프리팹에 한 번에 적용한다. 이 규칙을 새로 붙이거나 고친 뒤 부른다.</summary>
        [MenuItem("Eclipse/화면 왜곡 렌더러 정리")]
        public static void DisableInAll()
        {
            var changed = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (DisableIn(path)) changed.Add(path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(changed.Count == 0
                ? "화면 왜곡 렌더러: 끌 것이 없습니다."
                : $"화면 왜곡 렌더러를 끈 프리팹 {changed.Count}개\n{string.Join("\n", changed)}");
        }
    }

    /// <summary>임포트되는 프리팹에 <see cref="ScreenWarpFilter"/>를 건다.</summary>
    public sealed class ScreenWarpPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    ScreenWarpFilter.DisableIn(path);
        }
    }
}
