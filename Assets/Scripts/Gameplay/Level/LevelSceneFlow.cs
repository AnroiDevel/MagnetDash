using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LevelSceneFlow
{
    private readonly MonoBehaviour _host;
    private readonly string _systemsSceneName;
    private readonly float _sceneOpTimeoutSeconds;
    private readonly Func<int, bool> _isValidBuildIndex;

    private Coroutine _switchRoutine;

    public LevelSceneFlow(
        MonoBehaviour host,
        string systemsSceneName,
        float sceneOpTimeoutSeconds,
        Func<int, bool> isValidBuildIndex)
    {
        _host = host;
        _systemsSceneName = systemsSceneName;
        _sceneOpTimeoutSeconds = sceneOpTimeoutSeconds;
        _isValidBuildIndex = isValidBuildIndex;
    }

    public void Cancel()
    {
        if(_switchRoutine != null)
        {
            _host.StopCoroutine(_switchRoutine);
            _switchRoutine = null;
        }
    }

    public Coroutine SwitchTo(
        int targetBuildIndex,
        Action<Scene> onActivated,
        Action<bool> onDone)
    {
        Cancel();
        _switchRoutine = _host.StartCoroutine(CoSwitchToScene(targetBuildIndex, onActivated, onDone));
        return _switchRoutine;
    }

    private IEnumerator CoSwitchToScene(int targetBuildIndex, Action<Scene> onActivated, Action<bool> onDone)
    {
        bool success = false;

        try
        {
            if(!_isValidBuildIndex(targetBuildIndex))
            {
                Debug.LogError($"[LevelSceneFlow] Target buildIndex {targetBuildIndex} is invalid.");
                yield break;
            }

            // 1) ensure Systems loaded
            var systems = SceneManager.GetSceneByName(_systemsSceneName);
            if(!systems.IsValid() || !systems.isLoaded)
            {
                var sysOp = SceneManager.LoadSceneAsync(_systemsSceneName, LoadSceneMode.Additive);
                if(sysOp == null)
                {
                    Debug.LogError($"[LevelSceneFlow] Failed to start loading Systems '{_systemsSceneName}'.");
                    yield break;
                }

                float tSys = 0f;
                while(!sysOp.isDone)
                {
                    tSys += Time.unscaledDeltaTime;
                    if(tSys > _sceneOpTimeoutSeconds)
                    {
                        Debug.LogError($"[LevelSceneFlow] Timeout while loading Systems '{_systemsSceneName}'.");
                        yield break;
                    }
                    yield return null;
                }

                systems = SceneManager.GetSceneByName(_systemsSceneName);
            }

            // 2) load target additively and identify newly loaded scene
            Scene newlyLoaded = default;

            void OnLoaded(Scene s, LoadSceneMode mode)
            {
                if(s.buildIndex == targetBuildIndex && s != systems)
                    newlyLoaded = s;
            }

            SceneManager.sceneLoaded += OnLoaded;

            var loadOp = SceneManager.LoadSceneAsync(targetBuildIndex, LoadSceneMode.Additive);
            if(loadOp == null)
            {
                SceneManager.sceneLoaded -= OnLoaded;
                Debug.LogError($"[LevelSceneFlow] Failed to start loading scene index {targetBuildIndex}.");
                yield break;
            }

            float tLoad = 0f;
            while(!loadOp.isDone)
            {
                tLoad += Time.unscaledDeltaTime;
                if(tLoad > _sceneOpTimeoutSeconds)
                {
                    SceneManager.sceneLoaded -= OnLoaded;
                    Debug.LogError($"[LevelSceneFlow] Timeout while loading scene {targetBuildIndex}.");
                    yield break;
                }
                yield return null;
            }

            SceneManager.sceneLoaded -= OnLoaded;

            // fallback: detect by iterating loaded scenes
            if(!newlyLoaded.IsValid())
            {
                for(int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if(s.isLoaded && s.buildIndex == targetBuildIndex && s != systems)
                    {
                        newlyLoaded = s;
                        break;
                    }
                }
            }

            if(!newlyLoaded.IsValid())
            {
                Debug.LogError($"[LevelSceneFlow] Could not identify newly loaded scene {targetBuildIndex}.");
                yield break;
            }

            // 3) activate
            SceneManager.SetActiveScene(newlyLoaded);
            yield return null; // let Awake/OnEnable run

            onActivated?.Invoke(newlyLoaded);

            // 4) unload everything except Systems + newlyLoaded
            var toUnload = new List<Scene>(SceneManager.sceneCount);
            for(int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if(!s.isLoaded)
                    continue;
                if(s == newlyLoaded)
                    continue;
                if(s == systems)
                    continue;
                toUnload.Add(s);
            }

            foreach(var s in toUnload)
            {
                var unOp = SceneManager.UnloadSceneAsync(s);
                if(unOp == null)
                {
                    Debug.LogWarning($"[LevelSceneFlow] Unload op null for scene '{s.name}'. Skipping.");
                    continue;
                }

                float tUn = 0f;
                while(!unOp.isDone)
                {
                    tUn += Time.unscaledDeltaTime;
                    if(tUn > _sceneOpTimeoutSeconds)
                    {
                        Debug.LogError($"[LevelSceneFlow] Timeout while unloading scene '{s.name}'. Continue.");
                        break;
                    }
                    yield return null;
                }
            }

            success = true;
        }
        finally
        {
            onDone?.Invoke(success);
        }
    }
}
