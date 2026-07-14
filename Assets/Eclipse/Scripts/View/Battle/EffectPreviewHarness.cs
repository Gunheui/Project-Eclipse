using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// EffectSpec를 Play 모드에서 반복 재생해 확인하는 프리뷰 하니스. 전용 프리뷰 씬에 두고 Play를 켜면
    /// 지정한 spec들을 순서대로 계속 재생한다 — 전투를 거치지 않고 이펙트만 눈으로 확인한다.
    /// Play 모드라 DOTween이 정상 구동되므로 별도 수동 틱이 필요 없다.
    /// </summary>
    public class EffectPreviewHarness : MonoBehaviour
    {
        [SerializeField] private SpriteEffectPlayer effectPlayerPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private EffectSpec[] specs;
        [SerializeField] private int speed = 1;
        [SerializeField] private float gapBetween = 0.8f;

        private void Start() => LoopAsync(this.GetCancellationTokenOnDestroy()).Forget();

        private async UniTaskVoid LoopAsync(CancellationToken ct)
        {
            if (effectPlayerPrefab == null || specs == null || specs.Length == 0) return;

            while (!ct.IsCancellationRequested)
            {
                foreach (var spec in specs)
                {
                    if (spec == null) continue;
                    await PlayOnce(spec, ct);
                    await UniTask.WaitForSeconds(gapBetween, cancellationToken: ct);
                }
            }
        }

        private UniTask PlayOnce(EffectSpec spec, CancellationToken ct)
        {
            var pos = spawnPoint != null ? spawnPoint.position : transform.position;
            var player = Instantiate(effectPlayerPrefab, pos, Quaternion.identity);
            return player.Play(spec, Mathf.Max(1, speed), ct);
        }
    }
}
