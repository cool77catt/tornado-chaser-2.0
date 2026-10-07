using UnityEngine;

namespace TornadoChaser
{
    static class AppSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            // Mobile platforms default to 30 fps.
            Application.targetFrameRate = 60;
        }
    }
}
