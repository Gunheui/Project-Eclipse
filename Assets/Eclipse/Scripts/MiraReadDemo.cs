using System;
using Eclipse.Data;
using Eclipse.Domain;
using UnityEngine;

namespace Eclipse.Scripts
{
    public class MiraReadDemo : MonoBehaviour
    {
        [SerializeField] private CharacterSO mira;

        private void Start()
        {
            Debug.Log($"미라: {mira.displayName} / HP {mira.baseStats.hp} / ATK {mira.baseStats.atk}");
            Debug.Log($"궁극기: {mira.ultimateSkill.displayName} / 쿨 {mira.ultimateSkill.cooldownTurns}턴");
            Debug.Log($"HP@Lv10: {mira.growthCurve.StatAtLevel(mira.baseStats.hp, 10)}");

            var rt = new SkillRuntime(mira.ultimateSkill);
            Debug.Log($"[사용 전] IsReady={rt.IsReady}, 남은쿨={rt.CurrentCooldown}");
            Debug.Log($"TryUse()={rt.TryUse()}");
            Debug.Log($"[사용 후] IsReady={rt.IsReady}, 남은쿨={rt.CurrentCooldown}");
            for (int i = 1; i <= 5; i++)
            {
                rt.Tick();
                Debug.Log($"Tick {i} → 남은쿨={rt.CurrentCooldown}, IsReady={rt.IsReady}");
            }

            var a = new SkillRuntime(mira.ultimateSkill);
            var b = new SkillRuntime(mira.ultimateSkill);
            a.TryUse();
            Debug.Log($"[공유 정의, 개별 상태] a.IsReady={a.IsReady}, b.IsReady={b.IsReady}, SO쿨={mira.ultimateSkill.cooldownTurns}");
        }
    }
}