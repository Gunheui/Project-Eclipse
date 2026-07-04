using UnityEngine;
using VContainer;

namespace Eclipse.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        [Inject]
        private IAppLogger _appLogger;

        private void Start()
        {
            _appLogger.Log("Eclipse booted");
        }
    }
}