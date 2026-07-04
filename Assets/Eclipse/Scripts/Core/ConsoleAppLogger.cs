using UnityEngine;

namespace Eclipse.Core
{
    public class ConsoleAppLogger : IAppLogger
    {
        public void Log(string message)
        {
            Debug.Log(message);
        }
    }
}