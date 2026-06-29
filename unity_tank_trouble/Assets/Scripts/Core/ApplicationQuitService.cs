using UnityEngine;

namespace TankTrouble.Core
{
    public static class ApplicationQuitService
    {
        public static void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
