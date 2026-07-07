using UnityEngine;

namespace Eclipse.Core
{
    /// <summary>
    /// Unity 콘솔로 출력하는 <see cref="IAppLogger"/> 기본 구현체.
    /// </summary>
    public class ConsoleAppLogger : IAppLogger
    {
        public void Log(string message)
        {
            Debug.Log(message);
        }
    }
}