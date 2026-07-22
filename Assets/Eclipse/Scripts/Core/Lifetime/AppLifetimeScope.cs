using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.Service;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Eclipse.Core
{
    /// <summary>
    /// 앱 루트 라이프타임 스코프.
    /// 씬이 바뀌어도 유지해야 하는 상태(재화, 세이브, 내비 컨텍스트, 스프라이트 로더)를 들고 있다.
    /// 앱 시작 시 세이브 파일을 읽어 각 상태 홀더의 초기값을 만들고,
    /// 백그라운드 전환 시점(iOS suspend, 브라우저 탭 이탈)에 마지막 스냅샷을 저장한다.
    /// </summary>
    public class AppLifetimeScope : LifetimeScope
    {
        // 초기 로스터 시드 겸 세이브 복원용 id→CharacterSO 카탈로그. 인스펙터에서 채운다.
        [Serializable]
        private struct RosterEntry
        {
            public CharacterSO character;
            public int level;
        }

        [SerializeField] private RosterEntry[] dummyRoster;

        private SaveService _saveService;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.Register<ISceneFlow, SceneFlowService>(Lifetime.Singleton);

            // 씬 전환에도 살아남는 상태들. 세이브 파일이 있으면 그 값으로, 없으면 신규 계정 기본값으로 시작한다.
            var data = SaveService.LoadOrNew(SaveService.DefaultFilePath);
            builder.RegisterInstance(LoadOrSeedSave(data));
            builder.Register(_ => new CurrencyWallet(data.essence, data.gold, data.manual), Lifetime.Singleton);
            builder.Register(_ =>
            {
                var progress = new StageProgress();
                SaveService.ApplyChapters(data, progress);
                return progress;
            }, Lifetime.Singleton);
            builder.Register<ISpriteProvider, DirectSpriteProvider>(Lifetime.Singleton);
            builder.Register<IRewardService, StageRewardService>(Lifetime.Singleton);
            builder.Register(r => new SaveService(
                r.Resolve<PlayerSave>(), r.Resolve<CurrencyWallet>(), r.Resolve<StageProgress>()), Lifetime.Singleton);
            builder.Register<NavigationContext>(Lifetime.Singleton);

            // 라이프사이클 훅이 쓸 참조를 빌드 완료 시점에 잡아둔다.
            builder.RegisterBuildCallback(c => _saveService = c.Resolve<SaveService>());
        }

        // 세이브 데이터에서 PlayerSave를 복원한다. 복원해도 보유 캐릭터가 없으면(신규 계정이거나
        // 카탈로그와 id가 하나도 안 맞는 경우) 인스펙터 로스터로 시드한다.
        // Configure 중에 불리므로 절대 던지면 안 된다. 여기서 실패하면 컨테이너 빌드가 깨져 검은 화면이 뜬다.
        private PlayerSave LoadOrSeedSave(SaveData data)
        {
            var catalog = dummyRoster
                .Where(e => e.character != null && !string.IsNullOrEmpty(e.character.id))
                .GroupBy(e => e.character.id)
                .ToDictionary(g => g.Key, g => g.First().character);

            var restored = SaveService.BuildPlayerSave(data, catalog);
            if (restored.OwnedCharacters.Count > 0)
                return restored;

            var owned = dummyRoster
                .Where(e => e.character != null)
                .Select(e => new OwnedCharacter(e.character, e.level))
                .ToList();
            return new PlayerSave(owned);
        }

        // iOS는 suspend라 OnApplicationQuit이 안 불리고 WebGL은 종료 콜백 자체가 없어서
        // 백그라운드 전환 신호에서 저장한다. 컨테이너 빌드 전(_saveService == null)에는 저장할 상태가 없다.
        private void OnApplicationPause(bool paused)
        {
            if (paused) _saveService?.Save(); // iOS suspend
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) _saveService?.Save(); // 브라우저 탭 이탈
        }
    }
}
