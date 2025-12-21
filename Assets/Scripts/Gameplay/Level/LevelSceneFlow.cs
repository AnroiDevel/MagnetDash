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
    private int _token;

    public LevelSceneFlow(
        MonoBehaviour host,
        string systemsSceneName,
        float sceneOpTimeoutSeconds,
        Func<int, bool> isValidBuildIndex)
    {
        _host = host ? host : throw new ArgumentNullException(nameof(host), "[LevelSceneFlow] host is null.");
        _systemsSceneName = systemsSceneName;
        _sceneOpTimeoutSeconds = sceneOpTimeoutSeconds > 0f ? sceneOpTimeoutSeconds : 30f;
        _isValidBuildIndex = isValidBuildIndex ?? throw new ArgumentNullException(nameof(isValidBuildIndex));
    }

    public void Cancel()
    {
        _token++;

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
        _switchRoutine = _host.StartCoroutine(CoSwitchToScene(_token, targetBuildIndex, onActivated, onDone));
        return _switchRoutine;
    }

    private IEnumerator CoSwitchToScene(
        int token,
        int targetBuildIndex,
        Action<Scene> onActivated,
        Action<bool> onDone)
    {
        bool success = false;

        try
        {
            if(token != _token)
                yield break;

            if(!_isValidBuildIndex(targetBuildIndex))
            {
                Debug.LogError($"[LevelSceneFlow] Target buildIndex {targetBuildIndex} is invalid.");
                yield break;
            }

            // 1) Ensure Systems loaded
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
                    if(token != _token)
                        yield break;

                    tSys += Time.unscaledDeltaTime;
                    if(tSys > _sceneOpTimeoutSeconds)
                    {
                        Debug.LogError($"[LevelSceneFlow] Timeout while loading Systems '{_systemsSceneName}'.");
                        yield break;
                    }

                    yield return null;
                }

                systems = SceneManager.GetSceneByName(_systemsSceneName);
                if(!systems.IsValid() || !systems.isLoaded)
                {
                    Debug.LogError($"[LevelSceneFlow] Systems '{_systemsSceneName}' reported loaded, but scene is not valid/loaded.");
                    yield break;
                }
            }

            if(token != _token)
                yield break;

            // 2) Load target additively and identify newly loaded scene
            Scene newlyLoaded = default;

            void OnLoaded(Scene s, LoadSceneMode mode)
            {
                if(token != _token)
                    return;

                if(s.isLoaded && s.buildIndex == targetBuildIndex && s.handle != systems.handle)
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
                if(token != _token)
                {
                    SceneManager.sceneLoaded -= OnLoaded;
                    yield break;
                }

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

            if(token != _token)
                yield break;

            // Fallback: detect by iterating loaded scenes
            if(!newlyLoaded.IsValid())
            {
                for(int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if(s.isLoaded && s.buildIndex == targetBuildIndex && s.handle != systems.handle)
                    {
                        newlyLoaded = s;
                        break;
                    }
                }
            }

            if(!newlyLoaded.IsValid() || !newlyLoaded.isLoaded)
            {
                Debug.LogError($"[LevelSceneFlow] Could not identify newly loaded scene {targetBuildIndex}.");
                yield break;
            }

            // 3) Activate
            SceneManager.SetActiveScene(newlyLoaded);
            yield return null;

            if(token != _token)
                yield break;

            try
            {
                onActivated?.Invoke(newlyLoaded);
            }
            catch(Exception e)
            {
                Debug.LogException(e);
                yield break;
            }

            if(token != _token)
                yield break;

            // 4) Unload everything except Systems + newlyLoaded
            var toUnload = new List<Scene>(SceneManager.sceneCount);
            for(int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if(!s.isLoaded)
                    continue;

                if(s.handle == newlyLoaded.handle)
                    continue;

                if(s.handle == systems.handle)
                    continue;

                toUnload.Add(s);
            }

            for(int i = 0; i < toUnload.Count; i++)
            {
                if(token != _token)
                    yield break;

                var s = toUnload[i];
                var unOp = SceneManager.UnloadSceneAsync(s);
                if(unOp == null)
                {
                    Debug.LogWarning($"[LevelSceneFlow] Unload op null for scene '{s.name}'. Skipping.");
                    continue;
                }

                float tUn = 0f;
                while(!unOp.isDone)
                {
                    if(token != _token)
                        yield break;

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
            if(token == _token)
            {
                // очищаем только если это актуальная операция
                _switchRoutine = null;
            }

            onDone?.Invoke(success);
        }
    }
}
