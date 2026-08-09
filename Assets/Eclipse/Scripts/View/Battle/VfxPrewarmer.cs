using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// 전투 VFX 프리팹을 화면이 페이드로 가려진 동안 한 번씩 그려 셰이더 컴파일을 미리 끝내는 예열기.
    /// </summary>
    public class VfxPrewarmer : MonoBehaviour
    {
        [Tooltip("예열할 스펙 목록. 컨텍스트 메뉴 \"전체 수집\"이 프로젝트의 VfxSpec 전부로 채운다.")]
        [SerializeField] private List<VfxSpec> specs = new();

        // 앱 세션당 1회만 예열한다. 컴파일된 셰이더는 프로세스가 살아 있는 동안 남는다.
        private static bool s_warmed;

        /// <summary>
        /// 스펙의 모든 파티클 프리팹을 카메라 앞에 스폰해 두 프레임 렌더한 뒤 파괴한다. 첫 호출만 실비용이고
        /// 이후 호출은 즉시 반환한다. 화면이 가려진 동안 호출해야 스폰이 보이지 않는다.
        /// </summary>
        public async UniTask WarmupOnceAsync(CancellationToken ct)
        {
            if (s_warmed) return;

            var spawned = new List<GameObject>();
            try
            {
                // 프러스텀 밖 스폰은 컬링돼 드로우가 일어나지 않으므로 반드시 카메라 앞이어야 한다.
                // UGUI 페이드 오버레이는 시각적으로만 가리고 파티클 드로우는 그대로 일어난다.
                var cam = Camera.main;
                Vector3 pos = cam != null
                    ? cam.transform.position + cam.transform.forward * 10f
                    : Vector3.zero;

                var seen = new HashSet<(GameObject prefab, bool tinted)>();
                foreach (var spec in specs)
                {
                    if (spec == null) continue;
                    foreach (var layer in spec.layers)
                    {
                        if (layer.prefab == null) continue;
                        // 틴트 여부까지 중복 제거 키에 넣는다. _EMISSION 키워드가 켜지면 다른 셰이더 변형이라 따로 예열해야 한다.
                        bool tinted = layer.materialTint.a > 0f;
                        if (!seen.Add((layer.prefab, tinted))) continue;

                        var go = Instantiate(layer.prefab, pos, Quaternion.identity);
                        if (tinted) VfxPlayer.ApplyMaterialTint(go, layer.materialTint);
                        spawned.Add(go);

                        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            // 루트만 Simulate한다. 자식에 개별 restart를 걸면 부모 시뮬레이션이 방출한 서브이미터 입자가 지워진다.
                            if (ps.GetComponentsInParent<ParticleSystem>(true).Length > 1) continue;
                            // 빨리감기로 지연 버스트·서브이미터·트레일까지 이번 프레임에 입자를 갖게 한다.
                            ps.Simulate(0.5f, withChildren: true, restart: true);
                        }
                    }
                }

                // 렌더 스레드가 실제로 그릴 때 컴파일이 일어난다. 그리기 전에 파괴하면 예열이 헛돈다.
                await UniTask.DelayFrame(2, cancellationToken: ct);

                // 그리기까지 마쳤을 때만 완료로 기록한다. 중간에 끊기면 다음 방 진입이 다시 시도한다.
                s_warmed = true;
            }
            finally
            {
                foreach (var go in spawned)
                    if (go != null) Destroy(go);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("전체 수집")]
        private void CollectAll()
        {
            specs.Clear();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:VfxSpec"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                specs.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<VfxSpec>(path));
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
