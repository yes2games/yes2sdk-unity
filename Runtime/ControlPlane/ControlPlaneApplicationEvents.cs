using UnityEngine;

namespace Yes2SDK
{
    internal sealed class ControlPlaneApplicationEvents : MonoBehaviour
    {
        private void OnApplicationPause(bool paused)
        {
            Yes2ControlPlane.Instance.SetApplicationPaused(paused);
        }

        private void OnApplicationFocus(bool focused)
        {
            Yes2ControlPlane.Instance.SetApplicationFocused(focused);
        }

        private void OnApplicationQuit()
        {
            Yes2ControlPlane.Instance.Shutdown();
        }
    }
}
