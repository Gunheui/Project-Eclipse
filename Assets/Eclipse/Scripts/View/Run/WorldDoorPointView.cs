using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Eclipse.Domain;
using Eclipse.Presentation;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// 문 지점 화면. 씬에 미리 세워 둔 문 3개를 이번 추첨 결과에 연결하고, 하나가 탭될 때까지 기다린다.
    /// 문 개수가 3으로 고정이라 런타임 생성 없이 앵커를 재사용한다.
    /// </summary>
    public class WorldDoorPointView : MonoBehaviour
    {
        [SerializeField] private WorldDoorView[] doors;

        private UniTaskCompletionSource<int> _choice;

        /// <summary> 문을 세우고 선택을 기다린다. 선택 전에 문이 내려가면 대기는 취소로 끝난다. </summary>
        /// <returns>사용자가 탭한 문의 자리. 탭 한 번이 곧 확정이다.</returns>
        /// <exception cref="ArgumentException">세워 둔 문보다 선택지가 많을 때.</exception>
        public UniTask<int> ShowAsync(IReadOnlyList<DoorOption> options)
        {
            int count = options?.Count ?? 0;
            if (count > doors.Length)
                throw new ArgumentException(
                    $"문 지점 선택지가 {count}개인데 세워 둔 문은 {doors.Length}개다.", nameof(options));

            AbandonPending();
            _choice = new UniTaskCompletionSource<int>();
            for (int i = 0; i < doors.Length; i++)
            {
                if (i < count) doors[i].Bind(options[i], OnDoorTapped);
                else doors[i].Clear();
            }
            return _choice.Task;
        }

        /// <summary> 문을 전부 내린다. 이미 비어 있으면 무동작이다. </summary>
        public void Clear()
        {
            AbandonPending();
            foreach (var door in doors)
                door.Clear();
        }

        private void OnDoorTapped(WorldDoorView door)
        {
            foreach (var other in doors)
                other.SetTappable(false);
            // 세워 둔 자리 번호가 곧 제시물의 인덱스다 — Bind가 i번 문에 i번 선택지를 걸어 둔다.
            _choice?.TrySetResult(Array.IndexOf(doors, door));
        }

        /// <summary>
        /// 아직 선택을 못 받은 대기를 취소로 끊는다. 이 뷰는 팝업과 달리 방마다 파괴되지 않고 살아남으므로,
        /// 끊어 두지 않으면 앞선 대기가 영영 완료되지 않는다. 이미 확정된 대기는 그대로 둔다.
        /// </summary>
        private void AbandonPending()
        {
            _choice?.TrySetCanceled();
            _choice = null;
        }

        private void OnDestroy() => AbandonPending();
    }
}
