using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// VfxSpec 하나를 재생하는 이펙트 인스턴스. 레이어마다 파티클 프리팹을 스폰해 앵커 위치·회전·배율·색·정렬을
    /// 입히고, 대기 시간이 지나면 방출을 멈춘 뒤 스스로 파괴된다. follow를 켠 레이어는 앵커를 매 프레임 다시
    /// 읽어 배틀러를 따라가고, 끈 레이어는 스폰한 자리에 남는다. 유지 레이어가 있으면 파괴를 미루고 배틀러가 걷을 때까지 남는다. 수명은 배틀러가 정하므로 이 재생기는 턴을 세지 않는다.
    /// SpriteEffectPlayer와 같은 "스폰 후 자기 소멸" 패턴.
    /// </summary>
    public class VfxPlayer : MonoBehaviour
    {
        // 방출을 멈춘 뒤 남은 입자가 잦아들 때까지 두는 여유(초). 바로 파괴하면 입자가 뚝 끊긴다.
        private const float FadeGrace = 1f;

        // 치우기 직전 색을 지우는 시간(초). 여유를 줘도 수명이 긴 입자는 남으므로, 마지막엔 색을 눌러 없앤다.
        private const float FadeOut = 0.35f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>페이드 대상 렌더러 하나와 그 원본 색. 매 프레임 이 색에 배율을 곱해 덮어쓴다.</summary>
        private readonly struct TintTarget
        {
            public readonly Renderer Renderer;
            public readonly int PropertyId;
            public readonly Color Color;

            public TintTarget(Renderer renderer, int propertyId, Color color)
            {
                Renderer = renderer;
                PropertyId = propertyId;
                Color = color;
            }
        }

        /// <summary>유지 중인 레이어 하나와 그 인스턴스. EachTurn은 턴마다 새로 스폰하므로 Instance가 비어 있다.</summary>
        private class HeldLayer
        {
            public VfxLayer Layer;
            public GameObject Instance;
        }

        private readonly List<HeldLayer> _held = new();

        // follow를 켠 인스턴스와 그 레이어. LateUpdate가 매 프레임 앵커 자리로 다시 옮긴다.
        private readonly List<(GameObject Instance, VfxLayer Layer)> _following = new();

        // 배틀러 파괴 토큰. 취소되면 앵커를 물어볼 곳이 사라지므로 추적을 멈춘다.
        private CancellationToken _ct;

        // 연출 배속으로 나눌 값. Play에서 세운 뒤 유지 레이어가 턴을 넘겨가며 계속 쓴다.
        private float _div = 1f;

        // 레이어 앵커를 월드 좌표로 바꾸는 함수. 스폰한 인스턴스가 살아 있는 동안 매 프레임 다시 부른다.
        private Func<VfxAnchor, Vector3> _anchorAt;

        /// <summary>유지 중인 레이어가 남아 있는지. false면 이 재생기는 대기가 끝나는 대로 사라진다.</summary>
        public bool HasHold => _held.Count > 0;

        /// <summary>
        /// 스펙을 재생한다. 반환 태스크는 레이어별 대기 시간이 지나면 완료되고, 프리팹이 그보다 길어도 기다리지
        /// 않는다 — 최대 10초짜리 프리팹이 턴을 붙잡지 못하게 한다.
        /// </summary>
        /// <param name="speed">연출 배속(1 또는 2). 지연·대기 시간을 나눈다.</param>
        /// <param name="anchorAt">
        /// 레이어 앵커를 월드 좌표로 바꾸는 함수. 배틀러가 넘기며, 스폰한 인스턴스가 남아 있는 동안 보관된다.
        /// </param>
        public async UniTask Play(VfxSpec spec, int speed, Func<VfxAnchor, Vector3> anchorAt, CancellationToken ct)
        {
            _div = Mathf.Max(1, speed);
            _anchorAt = anchorAt;
            _ct = ct;

            if (spec != null && spec.layers != null && spec.layers.Count > 0)
            {
                var layers = new List<UniTask>(spec.layers.Count);
                foreach (var layer in spec.layers)
                    layers.Add(PlayLayer(layer, ct));
                await UniTask.WhenAll(layers);
            }

            if (this != null && _held.Count == 0) FadeThenDestroy(gameObject, FadeGrace).Forget();
        }

        /// <summary>「턴마다」 유지 레이어를 한 번씩 다시 터뜨린다. 「켜 두기」 레이어는 그대로 둔다.</summary>
        public void FlashEachTurn()
        {
            foreach (var hold in _held)
                if (hold.Layer.holdMode == VfxHold.EachTurn) Flash(hold);
        }

        /// <summary>
        /// 유지 중인 레이어를 전부 걷고 이 재생기를 치운다. 출처 효과가 풀렸거나 배틀러가 죽었을 때 부른다.
        /// </summary>
        public void StopHold()
        {
            foreach (var hold in _held) Retire(hold.Instance);
            _held.Clear();
            if (this != null) FadeThenDestroy(gameObject, FadeGrace).Forget();
        }

        /// <summary>레이어 하나를 재생한다.</summary>
        /// <returns>유지 레이어는 시작 지연만 기다리고, 나머지는 반복과 대기 시간이 끝나면 완료된다.</returns>
        private async UniTask PlayLayer(VfxLayer layer, CancellationToken ct)
        {
            if (layer.prefab == null) return;

            // 등록은 지연보다 먼저 한다. 지연 중에 호출부가 HasHold를 읽어도 유지 레이어를 놓치지 않는다.
            var hold = layer.holdTurns > 0 ? RegisterHold(layer) : null;

            if (layer.startDelay > 0f)
            {
                await UniTask.WaitForSeconds(layer.startDelay / _div, cancellationToken: ct);
                if (this == null) return;
            }

            if (hold != null)
            {
                Flash(hold);
                return;
            }

            int repeats = Mathf.Max(1, layer.repeatCount);
            var spawned = new List<GameObject>(repeats);
            for (int i = 0; i < repeats; i++)
            {
                spawned.Add(Spawn(layer, AnchorPosition(layer)));
                if (i + 1 >= repeats) break;
                await UniTask.WaitForSeconds(layer.repeatInterval / _div, cancellationToken: ct);
                if (this == null) return;
            }

            await UniTask.WaitForSeconds(layer.awaitSeconds / _div, cancellationToken: ct);
            foreach (var go in spawned) StopEmitting(go);
        }

        private HeldLayer RegisterHold(VfxLayer layer)
        {
            var hold = new HeldLayer { Layer = layer };
            _held.Add(hold);
            return hold;
        }

        /// <summary>follow를 켠 인스턴스를 앵커 자리에 다시 붙인다. 배틀러가 대시하거나 흔들려도 따라간다.</summary>
        private void LateUpdate()
        {
            // 배틀러가 파괴되면 앵커를 구할 곳이 사라진다. 남은 입자는 마지막 자리에서 잦아든다.
            if (_ct.IsCancellationRequested) return;

            for (int i = _following.Count - 1; i >= 0; i--)
            {
                var (go, layer) = _following[i];
                // 「턴마다」 인스턴스는 스스로 파괴돼 빈칸을 남긴다.
                if (go == null) _following.RemoveAt(i);
                else go.transform.position = AnchorPosition(layer);
            }
        }

        private Vector3 AnchorPosition(VfxLayer layer)
            => _anchorAt(layer.anchor) + new Vector3(layer.offset.x, layer.offset.y, 0f);

        /// <summary>
        /// 유지 레이어의 이번 턴 몫을 재생한다. Continuous는 인스턴스를 붙들고, EachTurn은 대기가 끝나면 치운다.
        /// </summary>
        private void Flash(HeldLayer hold)
        {
            var go = Spawn(hold.Layer, AnchorPosition(hold.Layer));
            if (hold.Layer.holdMode == VfxHold.EachTurn) FadeThenDestroy(go, hold.Layer.awaitSeconds / _div + FadeGrace).Forget();
            else hold.Instance = go;
        }

        /// <summary>프리팹 한 벌을 스폰해 위치·회전·배율·색·정렬을 입힌다.</summary>
        private GameObject Spawn(VfxLayer layer, Vector3 position)
        {
            var go = Instantiate(layer.prefab, transform);
            var tr = go.transform;
            tr.position = position;
            tr.rotation = Quaternion.Euler(layer.rotation);
            tr.localScale = layer.prefab.transform.localScale * layer.scale;
            if (layer.overrideColor) ApplyColor(go, layer.color);
            ApplySpeed(go, layer.speed);
            if (layer.materialTint.a > 0f) ApplyMaterialTint(go, layer.materialTint);
            ApplySortingOrder(go, layer.sortingOrder);
            if (layer.follow) _following.Add((go, layer));
            return go;
        }

        /// <summary>
        /// 파티클 재생 속도를 레이어 배율로 곱한다. speed를 적지 않은 기존 스펙은 0으로 읽히므로 1로 본다.
        /// </summary>
        private static void ApplySpeed(GameObject go, float speed)
        {
            if (speed <= 0f || Mathf.Approximately(speed, 1f)) return;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.simulationSpeed *= speed;
            }
        }

        /// <summary>
        /// 머티리얼 발광색을 틴트로 덮는다. 파티클 시작색이 먹지 않는 메시·텍스처 프리팹(크리스탈 등)용이다.
        /// 기본색은 곱셈이라 파란 텍스처를 보라로 끌어오지 못한다 — 더하는 발광색이라야 원색을 눌러 이긴다.
        /// </summary>
        // ponytail: material 접근이 인스턴스를 뜨므로 이펙트 하나당 머티리얼 한 벌이 늘어난다. 전투 한 판 분량은
        // 무시할 만하다 — 틴트 쓰는 레이어가 늘어 눈에 띄면 MaterialPropertyBlock으로 바꾼다.
        private static void ApplyMaterialTint(GameObject go, Color tint)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == null) continue;
                var mat = r.material;
                if (!mat.HasProperty(EmissionColorId)) continue;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorId, tint);
            }
        }

        /// <summary>파티클 시작색을 레이어 색으로 갈아 끼운다. 알파는 원본을 지켜 페이드 연출이 남는다.</summary>
        private static void ApplyColor(GameObject go, Color color)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var start = main.startColor;
                // 그라디언트 모드는 알파 곡선을 버리고 1로 눕힌다. 페이드가 죽는 프리팹이 나오면 모드별로 가른다.
                float alpha = start.mode switch
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

        /// <summary>
        /// 프리팹 안 모든 렌더러의 정렬 순서를 덮어쓴다. 원본값이 팩마다 0~125로 제각각이라 그대로 두면
        /// 데미지 숫자와 HP바를 가리는 프리팹이 섞인다.
        /// </summary>
        private static void ApplySortingOrder(GameObject go, int order)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sortingOrder = order;
        }

        /// <summary>새 입자 방출만 멈춘다. 이미 떠 있는 입자는 수명대로 사라진다.</summary>
        private static void StopEmitting(GameObject go)
        {
            if (go == null) return;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>방출을 멈추고 남은 입자가 잦아든 뒤 치운다.</summary>
        private static void Retire(GameObject go)
        {
            if (go == null) return;
            StopEmitting(go);
            FadeThenDestroy(go, FadeGrace).Forget();
        }

        /// <summary>
        /// 여유 시간을 기다린 뒤 색을 서서히 지우고 치운다. 그냥 파괴하면 수명이 남은 입자가 한 프레임에 사라져
        /// 연출이 뚝 끊긴다.
        /// </summary>
        private static async UniTaskVoid FadeThenDestroy(GameObject go, float delay)
        {
            if (go == null) return;
            if (delay > 0f) await UniTask.WaitForSeconds(delay);
            if (go == null) return;

            var targets = CaptureColors(go);
            var block = new MaterialPropertyBlock();
            for (float t = 0f; t < FadeOut; t += Time.deltaTime)
            {
                if (go == null) return;
                Dim(targets, 1f - t / FadeOut, block);
                await UniTask.Yield();
            }

            if (go != null) Destroy(go);
        }

        /// <summary>페이드에 쓸 렌더러와 원본 색을 모은다. 색 프로퍼티가 없는 렌더러는 건너뛴다.</summary>
        private static List<TintTarget> CaptureColors(GameObject go)
        {
            var targets = new List<TintTarget>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mat = r.sharedMaterial;
                if (mat == null) continue;
                int id = mat.HasProperty(BaseColorId) ? BaseColorId
                    : mat.HasProperty(ColorId) ? ColorId
                    : 0;
                if (id == 0) continue;
                targets.Add(new TintTarget(r, id, mat.GetColor(id)));
            }
            return targets;
        }

        /// <summary>원본 색에 배율을 곱해 덮는다. 알파와 함께 RGB도 줄여야 가산 합성 입자가 같이 사그라든다.</summary>
        private static void Dim(List<TintTarget> targets, float k, MaterialPropertyBlock block)
        {
            foreach (var target in targets)
            {
                if (target.Renderer == null) continue;
                target.Renderer.GetPropertyBlock(block);
                var c = target.Color;
                block.SetColor(target.PropertyId, new Color(c.r * k, c.g * k, c.b * k, c.a * k));
                target.Renderer.SetPropertyBlock(block);
            }
        }
    }
}
