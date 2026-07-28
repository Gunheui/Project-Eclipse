using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Eclipse.Data;
using Eclipse.Data.Enums;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// 방에서 받은 재화를 적이 죽은 자리에 떨어뜨려 수량을 공개하는 연출. 문이 금액을 숨기므로
    /// 굴림값이 드러나는 자리가 여기다. 아이콘은 떨어진 곳에 그대로 있다가 수량 숫자와 함께 사라진다.
    /// </summary>
    public class CurrencyDropSpawner : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer dropPrefab;

        /// <summary> 재화 종류별 아이콘. <see cref="CurrencyType"/> 값이 곧 인덱스다. </summary>
        [SerializeField] private Sprite[] currencyIcons;

        [SerializeField] private FloatingText amountTextPrefab;
        [SerializeField] private Color amountColor = new(1f, 0.87f, 0.45f);
        [SerializeField] private float popInDuration = 0.12f;
        [SerializeField] private float vanishDuration = 0.15f;

        /// <summary>
        /// 드랍을 전부 뿌리고 마지막 하나가 사라질 때까지 기다린다. 반환 대기가 곧 방 전환 게이트다.
        /// </summary>
        /// <param name="drops">이미 지급이 끝난 영수증. 같은 종류는 하나로 합쳐 뿌린다.</param>
        /// <param name="origins">스폰 자리(적 사망 위치). 종류 수보다 적으면 앞에서부터 돌려 쓴다.</param>
        public UniTask PlayAsync(IReadOnlyList<RewardEntry> drops, IReadOnlyList<Vector3> origins,
            CancellationToken ct)
        {
            if (dropPrefab == null || drops == null || origins == null || origins.Count == 0)
                return UniTask.CompletedTask;

            // 에스크로 골드와 미드보스 골드처럼 같은 종류가 두 건 올 수 있다. 종류당 하나로 접는다.
            var merged = drops.Where(d => d.amount > 0)
                .GroupBy(d => d.type)
                .Select(g => new RewardEntry { type = g.Key, amount = g.Sum(d => d.amount) })
                .ToList();
            if (merged.Count == 0)
                return UniTask.CompletedTask;

            return UniTask.WhenAll(merged.Select(
                (entry, i) => PlayOneAsync(entry, origins[i % origins.Count], ct)));
        }

        /// <summary>
        /// 드랍 하나를 팝인 → 수량 공개 → 소멸 순으로 재생한다. 재생이 끝나면 오브젝트를 파괴한다.
        /// </summary>
        private async UniTask PlayOneAsync(RewardEntry entry, Vector3 origin, CancellationToken ct)
        {
            var icon = Instantiate(dropPrefab, origin, Quaternion.identity, transform);
            icon.sprite = IconOf(entry.type);
            // 프리팹이 정한 크기가 목표값이다. 1로 키우면 원본 스프라이트 크기(512px)가 그대로 나온다.
            var fullScale = icon.transform.localScale;
            icon.transform.localScale = Vector3.zero;
            try
            {
                await icon.transform.DOScale(fullScale, popInDuration).SetEase(Ease.OutBack)
                    .ToUniTask(TweenCancelBehaviour.Complete, ct);

                await UniTask.WaitForSeconds(ShowAmount(entry.amount, origin), cancellationToken: ct);

                await DOTween.Sequence()
                    .Append(icon.transform.DOScale(Vector3.zero, vanishDuration).SetEase(Ease.InBack))
                    .Join(icon.DOFade(0f, vanishDuration))
                    .ToUniTask(TweenCancelBehaviour.Complete, ct);
            }
            finally
            {
                if (icon != null) Destroy(icon.gameObject);
            }
        }

        /// <summary> 수량을 데미지 숫자와 같은 방식으로 띄운다. 재화 3종 모두 <c>+N</c> 표기다. </summary>
        /// <returns>숫자가 다 사라지기까지 걸리는 시간(초). 아이콘 소멸 시점을 여기에 맞춘다.</returns>
        private float ShowAmount(int amount, Vector3 origin)
        {
            if (amountTextPrefab == null) return 0f;
            // 배속 1 고정 — 드랍은 전투가 끝난 뒤라 전투 배속을 따르지 않는다.
            return Instantiate(amountTextPrefab, origin, Quaternion.identity, transform)
                .Show("+" + amount, amountColor, 1);
        }

        private Sprite IconOf(CurrencyType type)
        {
            int index = (int)type;
            return currencyIcons != null && index < currencyIcons.Length ? currencyIcons[index] : null;
        }
    }
}
