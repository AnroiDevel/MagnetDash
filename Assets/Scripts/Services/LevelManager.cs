using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Управляет прогрессией уровней, временем и звёздами.
/// Живёт в сцене Systems и не уничтожается между загрузками.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelManager : MonoBehaviour, ILevelFlow
{
    [Header("Scenes / flow")]
    [SerializeField] private string _systemsSceneName = "Systems";
    [SerializeField] private int _menuSceneIndex = 1;  // билд-индекс главного меню
    [SerializeField] private int _firstLevelSceneIndex = 2;  // первая сцена уровня в Build Settings
    [SerializeField] private float _autoNextDelay = 1.2f;
    [SerializeField, Min(1f)] private float _sceneOpTimeout = 15f;

    [Header("Stars")]
    [SerializeField, Min(0)] private int _starsPerLevel = 3;  // фиксированное число звёзд на уровень
    private int _collectedStars;


    // UI результатов уровня (регистрируется самим LevelResultPanel)
    private LevelResultPanel _resultPanel;

    // состояние попытки
    private float _levelStartTime;
    private bool _isLevelCompleted;
    private int _currentLevelIndex;

    // сервисы
    private IProgressService _progress;
    private IUIService _ui;

    public GameState State { get; private set; } = GameState.Boot;

    private void Awake()
    {
        // Доступ к менеджеру для других компонентов
        ServiceLocator.Register<ILevelFlow>(this);
        ServiceLocator.Register(this);

        ServiceLocator.WhenAvailable<IProgressService>(p => _progress = p);
        ServiceLocator.WhenAvailable<IUIService>(ui => _ui = ui);
    }

    private void Start()
    {
        // На случай, если при старте активна уже сцена уровня (а не меню)
        var active = SceneManager.GetActiveScene();
        if(IsLevelScene(active))
        {
            InitLevelState(active.buildIndex);
            State = GameState.Playing;
        }
    }

    private void OnDestroy()
    {
        // На всякий случай отцепим панель
        if(_resultPanel != null)
        {
            _resultPanel.RetryRequested -= OnResultRetry;
            _resultPanel.NextRequested -= OnResultNext;
            _resultPanel.MenuRequested -= OnResultMenu;
        }

        ServiceLocator.Unregister<ILevelFlow>(this);
    }

    private void Update()
    {
        var active = SceneManager.GetActiveScene();
        if(_isLevelCompleted || !IsLevelScene(active))
            return;

        _ui?.SetTime(Time.time - _levelStartTime);
    }

    // ===== Публичные методы, которыми пользуются другие компоненты =====

    /// <summary>Регистрация панели результатов. Вызывает сама панель при появлении.</summary>
    public void RegisterResultPanel(LevelResultPanel panel)
    {
        if(panel == null)
            return;

        // Отписываемся от старой, если была
        if(_resultPanel != null)
        {
            _resultPanel.RetryRequested -= OnResultRetry;
            _resultPanel.NextRequested -= OnResultNext;
            _resultPanel.MenuRequested -= OnResultMenu;
        }

        _resultPanel = panel;
        _resultPanel.Hide();
        _resultPanel.RetryRequested += OnResultRetry;
        _resultPanel.NextRequested += OnResultNext;
        _resultPanel.MenuRequested += OnResultMenu;
    }

    /// <summary>Отвязка панели. Вызывает сама панель при уничтожении/выключении.</summary>
    public void UnregisterResultPanel(LevelResultPanel panel)
    {
        if(panel == null || panel != _resultPanel)
            return;

        _resultPanel.RetryRequested -= OnResultRetry;
        _resultPanel.NextRequested -= OnResultNext;
        _resultPanel.MenuRequested -= OnResultMenu;
        _resultPanel = null;
    }

    public void CollectStar()
    {
        if(_isLevelCompleted)
            return;

        if(_collectedStars < _starsPerLevel)
            _collectedStars++;
    }

    public int GetStarsCollected() => _collectedStars;
    public int GetStarsPerLevel() => _starsPerLevel;

    public void CompleteLevel()
    {
        if(State != GameState.Playing)
            return; // защита от повторных триггеров портала и любых «лишних» вызовов

        State = GameState.LevelCompleted;

        ShowWinResult();
    }

    public void KillPlayer()
    {
        if(State != GameState.Playing)
            return; // нельзя убить игрока, если уровень уже завершён/поставлен на паузу/загружается

        State = GameState.LevelFailed;

        ShowWinResult();
    }


    private void ShowWinResult()
    {
        var scene = SceneManager.GetActiveScene();
        int build = scene.buildIndex;
        int levelNo = build - _firstLevelSceneIndex + 1;

        float elapsed = Time.time - _levelStartTime;

        bool pb = _progress?.SetBestTimeIfBetter(build, elapsed) ?? false;
        _progress?.SetStarsMax(build, _collectedStars);

        float? best = null;
        if(_progress != null && _progress.TryGetBestTime(build, out var bestTime))
            best = bestTime;

        // подсказка (потом можно взять из конфигурации уровня)
        string hint = "Используй обе полярности!";

        if(_resultPanel != null)
        {
            _resultPanel.ShowWin(
                levelNo,
                elapsed,
                best,
                _collectedStars,
                pb,
                hint);
        }
        else
        {
            // fallback, если панели нет
            _ui?.ShowWinToast(elapsed, pb, _collectedStars);
            Invoke(nameof(LoadNext), _autoNextDelay);
        }
    }

    // ===== Обработчики событий панели =====

    private void OnResultRetry() => Reload();
    private void OnResultNext() => LoadNext();
    private void OnResultMenu() => LoadMenu();

    // ===== Навигация по сценам (публичные методы) =====

    public void LoadNext()
    {
        if(State != GameState.LevelCompleted)
            return;

        var current = SceneManager.GetActiveScene();
        if(!IsLevelScene(current))
            return;

        int nextIndex = current.buildIndex + 1;
        if(!IsValidBuildIndex(nextIndex))
            nextIndex = _firstLevelSceneIndex;

        if(!IsValidBuildIndex(nextIndex))
        {
            Debug.LogError($"[LevelManager] Invalid _firstLevelBuildIdx={_firstLevelSceneIndex}");
            return;
        }

        LoadSceneInternal(nextIndex);
    }

    public void Reload()
    {
        // Разрешаем перезапуск только из Paused / LevelFailed / LevelCompleted
        if(State != GameState.Paused &&
           State != GameState.LevelFailed &&
           State != GameState.LevelCompleted)
            return;

        var current = SceneManager.GetActiveScene();
        if(!IsLevelScene(current))
            return;

        int idx = current.buildIndex;
        if(!IsValidBuildIndex(idx))
        {
            Debug.LogError($"[LevelManager] Invalid active buildIndex={idx}");
            return;
        }

        LoadSceneInternal(_currentLevelIndex);
    }


    public void LoadLevel(int buildIndex)
    {
        LoadSceneInternal(buildIndex);
    }

    public void LoadMenu()
    {
        // Из любых «меню» состояний это ок, из уровня — только если не инвариант нарушаем
        if(State == GameState.LoadingLevel)
            return;

        if(!IsValidBuildIndex(_menuSceneIndex))
        {
            Debug.LogError($"[LevelManager] Invalid _menuBuildIndex={_menuSceneIndex}");
            return;
        }

        LoadSceneInternal(_menuSceneIndex);
    }

    // ===== Внутренние вспомогательные методы =====

    private bool IsValidBuildIndex(int idx)
    {
        return idx >= 0 && idx < SceneManager.sceneCountInBuildSettings;
    }

    private bool IsLevelScene(Scene scene)
    {
        if(!scene.IsValid())
            return false;
        if(scene.name == _systemsSceneName)
            return false;
        if(scene.buildIndex == _menuSceneIndex)
            return false;
        return scene.buildIndex >= _firstLevelSceneIndex;
    }

    /// <summary>Инициализация состояния при входе в сцену уровня.</summary>
    //private void InitLevelState(Scene scene)
    //{
    //    if(!IsLevelScene(scene))
    //        return;

    //    _levelStartTime = Time.time;
    //    _isLevelCompleted = false;
    //    _isLoading = false;
    //    _collectedStars = 0;

    //    int levelNumber = scene.buildIndex - _firstLevelSceneIndex + 1;
    //    _ui?.SetLevel(levelNumber);
    //    _ui?.SetTime(0f);

    //    if(_progress != null && _progress.TryGetBestTime(scene.buildIndex, out var best))
    //        _ui?.RefreshBest(best);
    //    else
    //        _ui?.RefreshBest(null);
    //}


    private void InitLevelState(int levelBuildIndex)
    {
        _currentLevelIndex = levelBuildIndex;
        _isLevelCompleted = false;
        _collectedStars = 0;

        _levelStartTime = Time.time;

        _ui?.SetLevel(levelBuildIndex);
        _ui?.RefreshBest(levelBuildIndex, _progress);
    }


    private void LoadSceneInternal(int buildIndex)
    {
        StopAllCoroutines();
        State = GameState.LoadingLevel;
        StartCoroutine(CoSwitchToScene(buildIndex));
    }


    // ===== Переключение сцен (Systems + одна контентная) =====

    private IEnumerator CoSwitchToScene(int targetBuildIndex)
    {
        bool success = false;

        try
        {
            if(!IsValidBuildIndex(targetBuildIndex))
            {
                Debug.LogError($"[LevelManager] Target buildIndex {targetBuildIndex} is invalid.");
                yield break;
            }

            var current = SceneManager.GetActiveScene();

            // 1) Гарантируем, что Systems загружена
            var systems = SceneManager.GetSceneByName(_systemsSceneName);
            if(!systems.IsValid() || !systems.isLoaded)
            {
                var sysOp = SceneManager.LoadSceneAsync(_systemsSceneName, LoadSceneMode.Additive);
                if(sysOp == null)
                {
                    Debug.LogError($"[LevelManager] Failed to start loading Systems '{_systemsSceneName}'.");
                    yield break;
                }

                float tSys = 0f;
                while(!sysOp.isDone)
                {
                    tSys += Time.unscaledDeltaTime;
                    if(tSys > _sceneOpTimeout)
                    {
                        Debug.LogError($"[LevelManager] Timeout while loading Systems '{_systemsSceneName}'.");
                        yield break;
                    }
                    yield return null;
                }

                systems = SceneManager.GetSceneByName(_systemsSceneName);
            }

            // 2) Грузим целевую сцену и запоминаем именно НОВУЮ
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
                Debug.LogError($"[LevelManager] Failed to start loading scene index {targetBuildIndex}.");
                yield break;
            }

            float tLoad = 0f;
            while(!loadOp.isDone)
            {
                tLoad += Time.unscaledDeltaTime;
                if(tLoad > _sceneOpTimeout)
                {
                    SceneManager.sceneLoaded -= OnLoaded;
                    Debug.LogError($"[LevelManager] Timeout while loading scene {targetBuildIndex}.");
                    yield break;
                }
                yield return null;
            }

            SceneManager.sceneLoaded -= OnLoaded;

            // fallback: если по какой-то причине обработчик не зацепил сцену
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
                Debug.LogError($"[LevelManager] Could not identify newly loaded scene {targetBuildIndex}.");
                yield break;
            }

            // 3) Активируем новую сцену и инициализируем состояние, если это уровень
            SceneManager.SetActiveScene(newlyLoaded);
            yield return null; // дать кадр на Awake / OnEnable UI
            InitLevelState(newlyLoaded.buildIndex);

            // 4) Собираем и выгружаем все сцены, кроме Systems и новой
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
                    Debug.LogWarning($"[LevelManager] Unload op null for scene '{s.name}'. Skipping.");
                    continue;
                }

                float tUn = 0f;
                while(!unOp.isDone)
                {
                    tUn += Time.unscaledDeltaTime;
                    if(tUn > _sceneOpTimeout)
                    {
                        Debug.LogError($"[LevelManager] Timeout while unloading scene '{s.name}'. Continue.");
                        break;
                    }
                    yield return null;
                }
            }

            success = true;
        }
        finally
        {

            if(targetBuildIndex == _menuSceneIndex)
            {
                State = GameState.MainMenu;
            }
            else
            {
                State = GameState.Playing;
            }

            if(!success)
                Debug.LogWarning("[LevelManager] Scene switch finished with errors; state reset.");
        }
    }

    internal void Pause()
    {
        State = GameState.Paused;
    }

    internal void Resume()
    {
        State = GameState.Playing;
    }
}
