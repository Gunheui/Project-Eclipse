using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using UnityEngine;

namespace Eclipse.Service
{
    /// <summary>
    /// 캐릭터 초상 스프라이트 로딩 경로의 이음새. 소비 측(ViewModel)은 로딩 방식을 모른다.
    /// 구현체를 교체하면(직접 참조 → Addressables 등) 시그니처가 그대로라 소비 측은 바뀌지 않는다.
    /// </summary>
    public interface ISpriteProvider
    {
        /// <summary> 정의에 연결된 초상 스프라이트를 로드한다. null 정의면 null을 돌려준다. </summary>
        UniTask<Sprite> LoadPortraitAsync(CharacterSO definition, CancellationToken ct = default);
    }
}
