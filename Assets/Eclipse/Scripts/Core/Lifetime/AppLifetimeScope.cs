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
    /// 앱 루트 라이프타임 스코프. 씬이 바뀌어도 유지되는 상태(재화, 세이브, 내비 컨텍스트, 스프라이트 로더)를
    /// 보유한다. 앱 시작 시 세이브 파일로 초기값을 만들고, 백그라운드 전환 시점(iOS suspend, 브라우저 탭 이탈)에 저장한다.
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
        [SerializeField] private GrowthConfigSO growthConfig;

        [Header("디버그")]
        [Tooltip("켜면 앱 시작 시 보유 전원의 레벨·스킬 레벨·돌파를 상한까지 올린다. 테스트 플레이용 — 이후 저장 시점에 세이브에도 남는다.")]
        [SerializeField] private bool debugMaxGrowth;

        private SaveService _saveService;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.Register<ISceneFlow, SceneFlowService>(Lifetime.Singleton);

            // 씬 전환에도 살아남는 상태들. 세이브 파일이 있으면 그 값으로, 없으면 신규 계정 기본값으로 시작한다.
            var data = SaveService.LoadOrNew(SaveService.DefaultFilePath);
            builder.RegisterInstance(LoadOrSeedSave(data));
            builder.Register(_ => new CurrencyWallet(data.essence, data.gold, data.manual), Lifetime.Singleton);
            builder.Register<ICurrencyService, CurrencyService>(Lifetime.Singleton);
            builder.Register(_ =>
            {
                var progress = new ChapterProgress();
                SaveService.ApplyChapters(data, progress);
                return progress;
            }, Lifetime.Singleton);
            builder.Register<ISpriteProvider, DirectSpriteProvider>(Lifetime.Singleton);
            builder.Register<IRewardService, RunRewardService>(Lifetime.Singleton);
            builder.Register(r => new SaveService(
                r.Resolve<PlayerSave>(), r.Resolve<CurrencyWallet>(), r.Resolve<ChapterProgress>()), Lifetime.Singleton);
            builder.RegisterInstance(growthConfig);
            builder.Register<CharacterGrowthSignals>(Lifetime.Singleton);
            builder.Register<GrowthService>(Lifetime.Singleton);
            builder.Register<SkillEnhanceService>(Lifetime.Singleton);
            builder.Register<AscensionService>(Lifetime.Singleton);
            builder.Register<NavigationContext>(Lifetime.Singleton);

            // 라이프사이클 훅이 쓸 참조를 빌드 완료 시점에 잡아둔다.
            builder.RegisterBuildCallback(c => _saveService = c.Resolve<SaveService>());
        }

        /// <summary>
        /// 세이브 데이터에서 PlayerSave를 복원한다.
        /// Configure 중에 불리므로 절대 던지면 안 된다. 여기서 실패하면 컨테이너 빌드가 깨져 검은 화면이 뜬다.
        /// </summary>
        private PlayerSave LoadOrSeedSave(SaveData data)
        {
            var catalog = dummyRoster
                .Where(e => e.character != null && !string.IsNullOrEmpty(e.character.id))
                .GroupBy(e => e.character.id)
                .ToDictionary(g => g.Key, g => g.First().character);

            var restored = SaveService.BuildPlayerSave(data, catalog);
            if (restored.OwnedCharacters.Count > 0)
                return ApplyDebugMaxGrowth(restored);

            // 복원해도 보유 캐릭터가 없으면(신규 계정이거나 카탈로그와 id가 하나도 안 맞는 경우)
            // 인스펙터 로스터로 시드한다.
            var owned = dummyRoster
                .Where(e => e.character != null)
                .Select(e => new OwnedCharacter(e.character, e.level))
                .ToList();
            // 4칸이 다 차야 챕터에 들어갈 수 있다. 시드 계정은 보유 앞 4명을 편성에 채워 첫 실행부터 진입을 연다.
            var seeded = new PlayerSave(owned);
            for (int i = 0; i < PlayerSave.PartySlotCount && i < owned.Count; i++)
                seeded.Party[i] = owned[i];
            return ApplyDebugMaxGrowth(seeded);
        }

        /// <summary>
        /// <see cref="debugMaxGrowth"/>가 켜져 있으면 보유 전원의 성장치를 상한으로 덮어쓴다.
        /// 여기서 파일을 쓰지는 않지만 이후 아무 저장 시점(포커스 이탈·레벨업 등)에 이 값이 세이브에 남는다 —
        /// 원래 진행값으로 되돌리려면 세이브 파일을 지운다.
        /// </summary>
        private PlayerSave ApplyDebugMaxGrowth(PlayerSave save)
        {
            if (!debugMaxGrowth)
                return save;

            foreach (var owned in save.OwnedCharacters)
            {
                int maxLevel = owned.Definition.growthCurve != null ? owned.Definition.growthCurve.maxLevel : owned.Level;
                while (owned.Level < maxLevel)
                    owned.IncreaseLevel();
                for (int slot = 0; slot < OwnedCharacter.SkillSlotCount; slot++)
                    while (owned.SkillLevels[slot] < OwnedCharacter.MaxSkillLevel)
                        owned.IncreaseSkillLevel(slot);
                owned.AscensionTier = OwnedCharacter.MaxAscensionTier;
            }
            Debug.LogWarning($"[디버그] 보유 {save.OwnedCharacters.Count}명을 만렙으로 시작한다 — debugMaxGrowth를 끄면 세이브 값으로 돌아온다.");
            return save;
        }

        /// <summary>
        /// 백그라운드 전환 신호에서 저장한다.
        /// iOS는 suspend라 OnApplicationQuit이 안 불리고 WebGL은 종료 콜백 자체가 없다.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            // 컨테이너 빌드 전(_saveService == null)에는 저장할 상태가 없다.
            if (paused) _saveService?.Save(); // iOS suspend
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) _saveService?.Save(); // 브라우저 탭 이탈
        }

        /// <summary>
        /// 개발용 돌파 부여 경로. 돌파 재료(가챠 중복)가 아직 없어, 플레이 중 인스펙터 컨텍스트 메뉴로 검증한다.
        /// </summary>
        [ContextMenu("디버그: 보유 전원 돌파 +1")]
        private void DebugAscendAll()
        {
            if (Container == null)
            {
                Debug.LogWarning("컨테이너가 없다 — 플레이 모드에서만 쓸 수 있다.");
                return;
            }
            var ascension = Container.Resolve<AscensionService>();
            foreach (var owned in Container.Resolve<PlayerSave>().OwnedCharacters)
            {
                var result = ascension.TryAscend(owned);
                Debug.Log($"{owned.Definition.displayName} 돌파 {result} → {owned.AscensionTier}단계");
            }
        }
    }
}
