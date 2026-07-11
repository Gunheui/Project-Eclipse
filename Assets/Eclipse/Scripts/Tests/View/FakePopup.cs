using System;
using Cysharp.Threading.Tasks;
using Eclipse.View.Infra;
using UnityEngine;

namespace Eclipse.Tests.View
{
    /// <summary>
    /// 테스트용 IPopup 구현. Open에 예외를 주입해 Show 도중 실패 시 dim/정리 경로를 재현한다.
    /// </summary>
    public class FakePopup : MonoBehaviour, IPopup<bool>
    {
        public bool ThrowOnOpen;

        public UniTask Open()
        {
            if (ThrowOnOpen)
                throw new Exception("FakePopup.Open 강제 예외(테스트).");
            return UniTask.CompletedTask;
        }

        public UniTask Close() => UniTask.CompletedTask;

        public UniTask<bool> Result => UniTask.FromResult(true);
    }
}
