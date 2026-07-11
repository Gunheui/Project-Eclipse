using System;
using Cysharp.Threading.Tasks;
using Eclipse.View.Infra;
using UnityEngine;

namespace Eclipse.Tests.View
{
    /// <summary>
    /// 테스트용 IScreen 구현. 등장/퇴장에 프레임 지연·예외를 주입해 async 전환 상황을 재현한다.
    /// </summary>
    public class FakeScreen : MonoBehaviour, IScreen
    {
        public int EnterDelayFrames;
        public int ExitDelayFrames;
        public bool ThrowOnExit;

        public int EnterCount { get; private set; }
        public int ExitCount { get; private set; }

        public async UniTask OnEnter() 
        {
            EnterCount++;
            for (int i = 0; i < EnterDelayFrames; i++)
                await UniTask.Yield();
        }

        public async UniTask OnExit()
        {
            ExitCount++;
            for (int i = 0; i < ExitDelayFrames; i++)
                await UniTask.Yield();
            if (ThrowOnExit)
                throw new Exception("FakeScreen.OnExit 강제 예외(테스트).");
        }
    }
}
