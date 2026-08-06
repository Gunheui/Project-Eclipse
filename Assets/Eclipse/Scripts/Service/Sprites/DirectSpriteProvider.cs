using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using UnityEngine;

namespace Eclipse.Service
{
    /// <summary>
    /// CharacterSO에 직접 연결된 Sprite 참조를 그대로 돌려주는 기본 구현.
    /// 동기적으로 완료되므로 프레임 지연이 없다.
    /// </summary>
    public sealed class DirectSpriteProvider : ISpriteProvider
    {
        public UniTask<Sprite> LoadPortraitAsync(CharacterSO definition, CancellationToken ct = default)
        {
            return UniTask.FromResult(definition != null ? definition.portraitAssetRef : null);
        }

        public UniTask<Sprite> LoadPortraitFxAsync(CharacterSO definition, CancellationToken ct = default)
        {
            return UniTask.FromResult(definition != null ? definition.portraitFxAssetRef : null);
        }
    }
}
