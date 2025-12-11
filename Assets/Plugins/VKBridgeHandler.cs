using UnityEngine;

public sealed class VKBridgeHandler : MonoBehaviour
{
    public void OnAppHide()
    {
        AudioListener.pause = true;
        Time.timeScale = 0f;
    }

    public void OnAppRestore()
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
    }
}
