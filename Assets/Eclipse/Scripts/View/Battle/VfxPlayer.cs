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
    /// 입히고, 대기 시간이 지나면 방출을 멈춘 뒤 스스로 파괴된다. 유지 턴이 붙은 레이어가 있으면 파괴를 미루고
    /// 배틀러가 보내는 턴 통지를 받아 센다. SpriteEffectPlayer와 같은 "스폰 후 자기 소멸" 패턴.
    /// </summary>
    public class VfxPlayer : MonoBehaviour
    {
        // 방출을 멈춘 뒤 남은 입자가 잦아들 때까지 두는 여유(초). 바로 파괴하면 입자가 뚝 끊긴다.
        private const float FadeGrace = 1f;

        /// <summary>턴을 세는 중인 레이어 하나와 그 인스턴스. EachTurn은 턴마다 새로 스폰하므로 Instance가 비어 있다.</summary>
        private class HeldLayer
        {
            public VfxLayer Layer;
            public Vector3 Position;
            public int Remaining;
            public GameObject Instance;

            // 걸린 턴에는 남은 턴을 깎지 않는다. 첫 통지가 이 표시를 내린다.
            public bool Fresh = true;
        }

        private readonly List<HeldLayer> _held = new();

        // 연출 배속으로 나눌 값. Play에서 세운 뒤 유지 레이어가 턴을 넘겨가며 계속 쓴다.
        private float _div = 1f;

        /// <summary>턴을 세는 레이어가 남아 있는지. false면 이 재생기는 대기가 끝나는 대로 사라진다.</summary>
        public bool HasHold => _held.Count > 0;

        /// <summary>
        /// 스펙을 재생한다. 반환 태스크는 레이어별 대기 시간이 지나면 완료되고, 프리팹이 그보다 길어도 기다리지
        /// 않는다 — 최대 10초짜리 프리팹이 턴을 붙잡지 못하게 한다.
        /// </summary>
        /// <param name="speed">연출 배속(1 또는 2). 지연·대기 시간을 나눈다.</param>
        /// <param name="anchorAt">레이어 앵커를 월드 좌표로 푸는 함수. 배틀러가 넘긴다.</param>
        public async UniTask Play(VfxSpec spec, int speed, Func<VfxAnchor, Vector3> anchorAt, CancellationToken ct)
        {
            _div = Mathf.Max(1, speed);

            if (spec != null && spec.layers != null && spec.layers.Count > 0)
            {
                var layers = new List<UniTask>(spec.layers.Count);
                foreach (var layer in spec.layers)
                    layers.Add(PlayLayer(layer, anchorAt, ct));
                await UniTask.WhenAll(layers);
            }

            if (this != null && _held.Count == 0) Destroy(gameObject, FadeGrace);
        }

        /// <summary>
        /// 유지 중인 레이어의 남은 턴을 1 줄이고 다 쓴 레이어를 걷는다. EachTurn 레이어는 남아 있는 턴마다 다시 재생한다.
        /// 이펙트가 걸린 턴의 통지는 세지 않는다 — 도메인 지속 효과도 턴 스냅샷으로 같은 규칙을 쓴다.
        /// </summary>
        /// <returns>아직 유지 중이면 true. false면 이 재생기가 파괴 예약된 상태라 호출부가 목록에서 빼야 한다.</returns>
        public bool AdvanceTurn()
        {
            for (int i = _held.Count - 1; i >= 0; i--)
            {
                var hold = _held[i];
                if (!hold.Fresh) hold.Remaining--;
                hold.Fresh = false;

                if (hold.Remaining <= 0)
                {
                    Retire(hold.Instance);
                    _held.RemoveAt(i);
                    continue;
                }

                // 걷는 판정을 지난 뒤에 재생한다. 걷히는 턴에 한 번 더 띄우지 않는다.
                if (hold.Layer.holdMode == VfxHold.EachTurn) Flash(hold);
            }

            if (_held.Count > 0) return true;
            if (this != null) Destroy(gameObject, FadeGrace);
            return false;
        }

        /// <summary>
        /// 유지 중인 레이어를 전부 걷고 이 재생기를 치운다. 턴을 다 세기 전에 끊어야 할 때(사망·재바인딩) 부른다.
        /// </summary>
        public void StopHold()
        {
            foreach (var hold in _held) Retire(hold.Instance);
            _held.Clear();
            if (this != null) Destroy(gameObject, FadeGrace);
        }

        /// <summary>레이어 하나를 재생한다.</summary>
        /// <returns>유지 턴이 붙은 레이어는 시작 지연만 기다리고, 나머지는 반복과 대기 시간이 끝나면 완료된다.</returns>
        private async UniTask PlayLayer(VfxLayer layer, Func<VfxAnchor, Vector3> anchorAt, CancellationToken ct)
        {
            if (layer.prefab == null) return;

            var position = anchorAt(layer.anchor) + new Vector3(layer.offset.x, layer.offset.y, 0f);

            // 등록은 지연보다 먼저 한다. 지연 중에 호출부가 HasHold를 읽어도 유지 레이어를 놓치지 않는다.
            var hold = layer.holdTurns > 0 ? RegisterHold(layer, position) : null;

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
                spawned.Add(Spawn(layer, position));
                if (i + 1 >= repeats) break;
                await UniTask.WaitForSeconds(layer.repeatInterval / _div, cancellationToken: ct);
                if (this == null) return;
            }

            await UniTask.WaitForSeconds(layer.awaitSeconds / _div, cancellationToken: ct);
            foreach (var go in spawned) StopEmitting(go);
        }

        private HeldLayer RegisterHold(VfxLayer layer, Vector3 position)
        {
            var hold = new HeldLayer { Layer = layer, Position = position, Remaining = layer.holdTurns };
            _held.Add(hold);
            return hold;
        }

        /// <summary>
        /// 유지 레이어의 이번 턴 몫을 재생한다. Continuous는 인스턴스를 붙들고, EachTurn은 대기가 끝나면 치운다.
        /// </summary>
        private void Flash(HeldLayer hold)
        {
            var go = Spawn(hold.Layer, hold.Position);
            if (hold.Layer.holdMode == VfxHold.EachTurn) Destroy(go, hold.Layer.awaitSeconds / _div + FadeGrace);
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
            ApplySortingOrder(go, layer.sortingOrder);
            return go;
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
            Destroy(go, FadeGrace);
        }
    }
}
