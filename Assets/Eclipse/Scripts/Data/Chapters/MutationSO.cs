using Eclipse.Data.Enums;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 침식 변이 1종. 적 한 마리의 스탯 하나를 배수로 올리고, 이름 접두와 배틀러 틴트로 표시한다.
    /// 한 마리에 최대 1종만 붙는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Stages/Mutation Data")]
    public sealed class MutationSO : ScriptableObject
    {
        /// <summary> 참조·조회용 고정 키. </summary>
        public string id;

        /// <summary> 배수가 걸리는 스탯. None은 쓸 수 없다. </summary>
        public StatType statAxis;

        /// <summary> 해당 스탯에 곱하는 배수. 0보다 커야 한다. </summary>
        public float multiplier = 1f;

        /// <summary> 표시명 앞에 붙는 접두. 뒤 공백까지 포함해 입력한다("강화된 "). </summary>
        public string namePrefix;

        /// <summary> 배틀러 스프라이트에 입히는 틴트. </summary>
        public Color tintColor = Color.white;
    }
}
