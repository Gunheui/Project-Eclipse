using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Eclipse.Data;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// EffectSpec 하나를 재생하는 이펙트 인스턴스. 레이어마다 자식 SpriteRenderer를 만들어 스케일·회전·
    /// 이동·페이드를 DOTween으로 돌리고, 모든 레이어가 끝나면 스스로 파괴된다. FloatingText와 같은
    /// "스폰 후 자기 소멸" 패턴. 흰색 마스크 스프라이트를 tint로 색 입히고, additive면 발광 머티리얼을 쓴다.
    /// </summary>
    public class SpriteEffectPlayer : MonoBehaviour
    {
        // additive 레이어에 씌울 가산 블렌드 머티리얼(프리팹에서 지정). 없으면 기본 머티리얼로 폴백.
        [SerializeField] private Material additiveMaterial;

        /// <summary>
        /// 스펙을 재생한다. 반환 태스크는 모든 레이어 연출이 끝나 이 오브젝트가 파괴될 때 완료된다.
        /// </summary>
        /// <param name="speed">연출 배속(1 또는 2). 지연·지속 시간을 나눈다.</param>
        public async UniTask Play(EffectSpec spec, int speed, CancellationToken ct)
        {
            if (spec != null && spec.layers != null && spec.layers.Count > 0)
            {
                float div = Mathf.Max(1, speed);
                var layers = new List<UniTask>(spec.layers.Count);
                foreach (var layer in spec.layers)
                    layers.Add(PlayLayer(layer, div, ct));
                await UniTask.WhenAll(layers);
            }

            if (this != null) Destroy(gameObject);
        }

        // 레이어 하나를 자식 SpriteRenderer로 생성해 애니메이션한다. startDelay 동안은 숨겼다가 시작한다.
        private async UniTask PlayLayer(EffectLayer layer, float div, CancellationToken ct)
        {
            var go = new GameObject("EffectLayer");
            var tr = go.transform;
            tr.SetParent(transform, false);
            tr.localPosition = new Vector3(layer.offset.x, layer.offset.y, 0f);
            tr.localScale = new Vector3(layer.startScale.x, layer.startScale.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = layer.sprite;
            sr.color = layer.tint;
            sr.sortingOrder = layer.sortingOrder;
            if (layer.additive && additiveMaterial != null) sr.sharedMaterial = additiveMaterial;

            float delay = layer.startDelay / div;
            if (delay > 0f)
            {
                sr.enabled = false;
                await UniTask.WaitForSeconds(delay, cancellationToken: ct);
                if (sr == null) return;
                sr.enabled = true;
            }

            float dur = layer.duration / div;
            var ease = Map(layer.ease);
            var parts = new List<UniTask>(4);

            if (layer.endScale != layer.startScale)
                parts.Add(tr.DOScale(new Vector3(layer.endScale.x, layer.endScale.y, 1f), dur)
                    .SetEase(ease).ToUniTask(cancellationToken: ct));

            if (layer.moveBy != Vector2.zero)
                parts.Add(tr.DOLocalMove(tr.localPosition + new Vector3(layer.moveBy.x, layer.moveBy.y, 0f), dur)
                    .SetEase(ease).ToUniTask(cancellationToken: ct));

            if (layer.spinDegPerSec != 0f)
                parts.Add(tr.DOLocalRotate(new Vector3(0f, 0f, layer.spinDegPerSec * dur), dur, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear).ToUniTask(cancellationToken: ct));

            parts.Add(sr.DOFade(layer.endAlpha, dur).SetEase(ease).ToUniTask(cancellationToken: ct));

            await UniTask.WhenAll(parts);
        }

        // Data 레이어의 EffectEase를 DOTween Ease로 매핑한다(Data는 DOTween을 참조하지 않으므로 여기서 변환).
        private static Ease Map(EffectEase e) => e switch
        {
            EffectEase.Linear => Ease.Linear,
            EffectEase.OutQuad => Ease.OutQuad,
            EffectEase.InQuad => Ease.InQuad,
            EffectEase.OutCubic => Ease.OutCubic,
            EffectEase.OutBack => Ease.OutBack,
            EffectEase.InOutQuad => Ease.InOutQuad,
            _ => Ease.Linear,
        };
    }
}
