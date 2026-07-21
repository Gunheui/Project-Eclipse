using System;
using System.Collections.Generic;
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
    /// 해당 프로젝트의 루트 라이프타임 스코프.
    /// 씬 전환에서 살아남아야 하는 상태(재화·세이브·내비 컨텍스트·스프라이트 로더)를 보유한다.
    /// </summary>
    public class AppLifetimeScope : LifetimeScope
    {
        // 더미 보유 캐릭터 한 항목(정의 + 시작 레벨). 인스펙터에서 채운다.
        [Serializable]
        private struct RosterEntry
        {
            public CharacterSO character;
            public int level;
        }

        [SerializeField] private RosterEntry[] dummyRoster;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.Register<IAppLogger, ConsoleAppLogger>(Lifetime.Singleton);
            builder.Register<ISceneFlow, SceneFlowService>(Lifetime.Singleton);

            // 항상 유지되어야 하는 목록
            builder.RegisterInstance(BuildDummySave());
            builder.Register<ISpriteProvider, DirectSpriteProvider>(Lifetime.Singleton);
            builder.Register<CurrencyWallet>(Lifetime.Singleton);
            builder.Register<IRewardService, StageRewardService>(Lifetime.Singleton);
            builder.Register<StageProgress>(Lifetime.Singleton);
            builder.Register<NavigationContext>(Lifetime.Singleton);
        }

        /// <summary>인스펙터의 dummyRoster로 더미 PlayerSave를 만든다.</summary>
        private PlayerSave BuildDummySave()
        {
            var owned = new List<OwnedCharacter>();
            foreach (var entry in dummyRoster)
                owned.Add(new OwnedCharacter(entry.character, entry.level));
            return new PlayerSave(owned);
        }
    }
}