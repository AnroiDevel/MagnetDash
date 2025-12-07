using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class LevelSelectController : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private RectTransform _contentRoot;
    [SerializeField] private LevelItemView _itemPrefab;
    [SerializeField] private bool _clearOnBuild = true;

    [Header("Manual Levels (scenes)")]
    [SerializeField] private int _firstBuildIndex = 1;
    [SerializeField] private int _levelsToShow = 12;

    [Header("Runtime JSON Levels")]
    [SerializeField] private TextAsset _levelsJson;
    [SerializeField] private bool _showRuntimeLevels = true;

    private readonly List<LevelItemView> _items = new();

    private IProgressService _progress;
    private LevelJsonRoot _runtimeConfig;
    private int _manualLevelsCount;
    private int _runtimeLevelsCount;

    private void OnEnable()
    {
        BuildLayout(); // строим кнопки без прогресса

        // подписываемся на прогресс
        ServiceLocator.WhenAvailable<IProgressService>(p =>
        {
            _progress = p;
            _progress.Loaded += OnProgressLoaded;

            if(_progress.IsLoaded)
                OnProgressLoaded();
        });
    }

    private void OnDisable()
    {
        if(_progress != null)
            _progress.Loaded -= OnProgressLoaded;
    }

    // ============================
    //   СТРОИМ СЕТКУ КНОПОК
    // ============================

    private void BuildLayout()
    {
        if(_contentRoot == null || _itemPrefab == null)
            return;

        int totalScenes = SceneManager.sceneCountInBuildSettings;

        // ----- Manual -----
        int manualCount =
            _levelsToShow > 0
            ? _levelsToShow
            : Mathf.Max(0, totalScenes - _firstBuildIndex);

        manualCount = Mathf.Clamp(manualCount, 0, totalScenes - _firstBuildIndex);
        _manualLevelsCount = manualCount;

        // ----- JSON -----
        EnsureRuntimeConfigLoaded();
        _runtimeLevelsCount = (_showRuntimeLevels && _runtimeConfig != null)
            ? _runtimeConfig.levels.Count
            : 0;

        int total = manualCount + _runtimeLevelsCount;

        // ----- Rebuild UI -----
        if(_clearOnBuild)
        {
            foreach(var it in _items)
                if(it)
                    Destroy(it.gameObject);
            _items.Clear();
        }

        while(_items.Count < total)
            _items.Add(Instantiate(_itemPrefab, _contentRoot));

        while(_items.Count > total)
        {
            Destroy(_items[^1].gameObject);
            _items.RemoveAt(_items.Count - 1);
        }

        // ----- Bind первичных данных (без прогресса) -----
        int index = 0;

        // Manual
        for(int i = 0; i < manualCount; i++, index++)
        {
            int number = index + 1;

            _items[index].Bind(
                id: index,
                levelNumber: number,
                stars: 0,
                unlocked: false,
                onClick: OnLevelClicked
            );
        }

        // JSON
        for(int i = 0; i < _runtimeLevelsCount; i++, index++)
        {
            int number = index + 1;

            _items[index].Bind(
                id: index,
                levelNumber: number,
                stars: 0,
                unlocked: false,
                onClick: OnLevelClicked
            );
        }
    }

    // ============================
    //   ПРИМЕНЯЕМ ПРОГРЕСС
    // ============================

    private void OnProgressLoaded()
    {
        ApplyProgress();
    }

    private void ApplyProgress()
    {
        if(_progress == null)
            return;

        int total = _items.Count;

        int lastCompletedLogical = -1;
        _progress.TryGetLastCompletedLevel(out lastCompletedLogical);

        for(int logicalIndex = 0; logicalIndex < total; logicalIndex++)
        {
            bool isManual = logicalIndex < _manualLevelsCount;
            int progressKey;
            int stars;

            if(isManual)
            {
                int buildIndex = _firstBuildIndex + logicalIndex;
                progressKey = buildIndex;
            }
            else
            {
                int jsonIndex = logicalIndex - _manualLevelsCount;
                progressKey = -(jsonIndex + 1);
            }

            stars = Mathf.Clamp(_progress.GetStars(progressKey), 0, 3);
            bool unlocked = IsLogicalUnlocked(logicalIndex, lastCompletedLogical);

            int number = logicalIndex + 1;

            _items[logicalIndex].Bind(
                id: logicalIndex,
                levelNumber: number,
                stars: stars,
                unlocked: unlocked,
                onClick: OnLevelClicked
            );
        }
    }

    private bool IsLogicalUnlocked(int logicalIndex, int lastCompletedLogical)
    {
        // первый уровень всегда доступен
        if(logicalIndex == 0)
            return true;

        if(lastCompletedLogical < 0)
            return false;

        bool isRuntime = logicalIndex >= _manualLevelsCount;

        // базовое правило: можно пройти всё, что не дальше, чем на один шаг вперёд
        bool bySequence = logicalIndex <= lastCompletedLogical + 1;

        if(!isRuntime)
            return bySequence;

        // runtime уровни: только после того, как завершены все ручные
        bool allManualCompleted = lastCompletedLogical >= _manualLevelsCount - 1;
        return bySequence && allManualCompleted;
    }



    // ============================
    //   КЛИК ПО УРОВНЮ
    // ============================

    private void OnLevelClicked(int logicalIndex)
    {
        if(!ServiceLocator.TryGet<ILevelFlow>(out var flow))
            return;

        // проверку unlocked мы уже сделали при Bind, здесь можно
        // либо доверять UI, либо ещё раз вызвать IsLogicalUnlocked

        // интерфейс ILevelFlow лучше расширить:
        // void LoadByLogicalIndex(int logicalIndex);

        flow.LoadByLogicalIndex(logicalIndex);
    }

    // ============================
    //   JSON
    // ============================

    private void EnsureRuntimeConfigLoaded()
    {
        if(_runtimeConfig != null || _levelsJson == null)
            return;

        _runtimeConfig = JsonUtility.FromJson<LevelJsonRoot>(_levelsJson.text);
    }

}
