using System;
using System.Collections.Generic;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Eclipse.Core
{
    /// <summary>
    /// 해당 프로젝트 LifeTimeScope
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
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

            //Inject 목록
            builder.RegisterComponentInHierarchy<GameBootstrap>();
            builder.RegisterComponentInHierarchy<ScreenManager>();
            builder.RegisterComponentInHierarchy<PopupManager>();

            builder.RegisterInstance(BuildDummySave());
            builder.Register<CharacterListViewModel>(Lifetime.Singleton);
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
