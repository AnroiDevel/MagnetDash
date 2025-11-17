// SystemsBootstrap.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SystemsBootstrap
{
    private const string SystemsSceneName = "Systems";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureSystemsLoaded()
    {
        var s = SceneManager.GetSceneByName(SystemsSceneName);
        if(!s.IsValid() || !s.isLoaded)
            SceneManager.LoadScene(SystemsSceneName, LoadSceneMode.Additive);
    }
}

