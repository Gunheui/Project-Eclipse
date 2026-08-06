using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.Service;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    public class PartyPickViewModelTests
    {
        private static OwnedCharacter Owned(string name)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.id = name;
            so.displayName = name;
            return new OwnedCharacter(so, 1);
        }

        private sealed class FakeSceneFlow : ISceneFlow
        {
            public UniTask ToBattleAsync() => UniTask.CompletedTask;
            public UniTask ToMainAsync() => UniTask.CompletedTask;
        }

        private sealed class FakeSpriteProvider : ISpriteProvider
        {
            public UniTask<Sprite> LoadPortraitAsync(CharacterSO definition, CancellationToken ct = default)
                => UniTask.FromResult<Sprite>(null);

            public UniTask<Sprite> LoadPortraitFxAsync(CharacterSO definition, CancellationToken ct = default)
                => UniTask.FromResult<Sprite>(null);
        }

        private static (PartyPickViewModel pick, PartyFormationViewModel formation, List<OwnedCharacter> roster) Build(int rosterCount)
        {
            var roster = Enumerable.Range(0, rosterCount).Select(i => Owned("C" + i)).ToList();
            var save = new PlayerSave(roster);
            var chapter = ScriptableObject.CreateInstance<ChapterSO>();
            chapter.id = "chapter_t";
            var formation = new PartyFormationViewModel(new[] { chapter }, save, new NavigationContext(),
                new FakeSceneFlow(), saveService: null, new FakeSpriteProvider(), new CharacterGrowthSignals());
            var pick = new PartyPickViewModel(formation);
            return (pick, formation, roster);
        }

        [Test]
        public void Place_앵커_슬롯에_배치하고_슬롯번호를_매긴다()
        {
            var (pick, formation, roster) = Build(3);
            formation.BeginPick(2);
            pick.BeginSession();

            pick.Place(pick.Items[1]);

            Assert.AreSame(roster[1], formation.Slots[2].Value, "탭한 슬롯에 그대로 들어간다");
            Assert.IsNull(formation.Slots[0].Value, "앞 슬롯은 빈칸으로 남는다");
            Assert.AreEqual(3, pick.Items[1].SlotNumber.CurrentValue, "배지는 점유 슬롯 번호(1-based)");
            Assert.AreEqual(0, pick.Items[0].SlotNumber.CurrentValue);

            pick.Dispose();
        }

        [Test]
        public void Place_같은_캐릭터를_앵커에서_재탭하면_제거된다()
        {
            var (pick, formation, _) = Build(3);
            formation.BeginPick(1);
            pick.BeginSession();
            pick.Place(pick.Items[0]);

            pick.Place(pick.Items[0]);

            Assert.IsNull(formation.Slots[1].Value, "재탭은 슬롯을 비운다");
            Assert.AreEqual(0, pick.Items[0].SlotNumber.CurrentValue);

            pick.Dispose();
        }

        [Test]
        public void Place_다른_슬롯에_있던_캐릭터는_이동한다()
        {
            var (pick, formation, roster) = Build(3);
            formation.BeginPick(0);
            pick.BeginSession();
            pick.Place(pick.Items[0]);

            formation.BeginPick(3);
            pick.BeginSession();
            pick.Place(pick.Items[0]);

            Assert.IsNull(formation.Slots[0].Value, "원래 슬롯은 비워진다");
            Assert.AreSame(roster[0], formation.Slots[3].Value);
            Assert.AreEqual(4, pick.Items[0].SlotNumber.CurrentValue);

            pick.Dispose();
        }

        [Test]
        public void BeginSession_현재_편성으로_슬롯번호를_시드한다()
        {
            var (pick, formation, roster) = Build(4);
            formation.AssignToSlot(1, roster[0]);
            formation.AssignToSlot(3, roster[2]);
            formation.BeginPick(0);

            pick.BeginSession();

            Assert.AreEqual(2, pick.Items.First(it => it.Owned == roster[0]).SlotNumber.CurrentValue);
            Assert.AreEqual(4, pick.Items.First(it => it.Owned == roster[2]).SlotNumber.CurrentValue);
            Assert.AreEqual(0, pick.Items.First(it => it.Owned == roster[1]).SlotNumber.CurrentValue, "미편성은 0");
            Assert.IsNull(pick.RoleFilter.Value, "세션 시작 시 역할 필터는 전체");

            pick.Dispose();
        }
    }
}
