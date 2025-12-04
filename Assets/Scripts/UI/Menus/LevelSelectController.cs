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
    [SerializeField] private int _firstBuildIndex = 1;   // первая сцена-уровень
    [SerializeField] private int _levelsToShow = 12;     // сколько ручных уровней показывать

    [Header("Runtime JSON Levels")]
    [SerializeField] private TextAsset _levelsJson;      // тот же levels.json, что и у LevelRuntimeLoader
    [SerializeField] private bool _showRuntimeLevels = true;

    private readonly List<LevelItemView> _items = new();

    private IProgressService _progress;
    private LevelJsonRoot _runtimeConfig;   // разобранный JSON (только для UI)
    private int _manualLevelsCount;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IProgressService>(p =>
        {
            _progress = p;
            BuildOrRefresh();
        });

        // На случай, если прогресс ещё не поднялся — построим "пустую" версию
        BuildOrRefresh();
    }

    public void BuildOrRefresh()
    {
        if(_contentRoot == null || _itemPrefab == null)
            return;

        int totalScenes = SceneManager.sceneCountInBuildSettings;

        // ----- 1. Ручные уровни -----
        int manualCount = _levelsToShow > 0
            ? _levelsToShow
            : (totalScenes - _firstBuildIndex);

        if(manualCount < 0)
            manualCount = 0;

        _manualLevelsCount = manualCount;

        // Проверяем, чтобы не выходить за BuildSettings
        if(_firstBuildIndex + manualCount > totalScenes)
            manualCount = Mathf.Max(0, totalScenes - _firstBuildIndex);

        _manualLevelsCount = manualCount;

        // ----- 2. JSON-уровни -----
        int runtimeCount = 0;
        if(_showRuntimeLevels && _levelsJson != null)
        {
            EnsureRuntimeConfigLoaded();
            if(_runtimeConfig != null && _runtimeConfig.levels != null)
                runtimeCount = _runtimeConfig.levels.Count;
        }

        int totalCount = manualCount + runtimeCount;

        // ----- Rebuild -----
        if(_items.Count != totalCount)
        {
            if(_clearOnBuild)
            {
                foreach(var it in _items)
                    if(it)
                        Destroy(it.gameObject);

                _items.Clear();
            }

            for(int i = 0; i < totalCount; i++)
            {
                var view = Instantiate(_itemPrefab, _contentRoot);
                _items.Add(view);
            }
        }

        // Если вдруг стало меньше уровней, чем в прошлый раз
        if(totalCount == 0)
            return;

        // ----- Bind: ручные сцены -----
        int index = 0;

        for(int i = 0; i < manualCount; i++, index++)
        {
            int buildIndex = _firstBuildIndex + i;
            int number = index + 1;   // общий номер в списке

            int stars = _progress?.GetStars(buildIndex) ?? 0;
            bool unlocked = IsManualUnlocked(buildIndex);

            _items[index].name = $"Level_{number:D3}_Scene_{buildIndex}";
            _items[index].Bind(
                buildIndex,                                     // id = buildIndex (>=0)
                number,
                Mathf.Clamp(stars, 0, 3),
                unlocked,
                OnLevelClicked
            );
        }

        // ----- Bind: runtime JSON уровни -----
        for(int i = 0; i < runtimeCount; i++, index++)
        {
            int jsonIndex = i;                 // 0..N-1 в _runtimeConfig.levels
            int number = index + 1;            // продолжение нумерации после ручных

            int id = EncodeRuntimeId(jsonIndex);   // отрицательное число

            int stars = _progress?.GetStars(id) ?? 0;
            bool unlocked = IsRuntimeUnlocked(jsonIndex, id);


            _items[index].name = $"Level_{number:D3}_Json_{jsonIndex}";
            _items[index].Bind(
                id,          // id < 0 → runtime
                number,
                Mathf.Clamp(stars, 0, 3),
                unlocked,
                OnLevelClicked
            );
        }
    }

    // ===== Раздел: разблокировка =====

    private bool IsRuntimeUnlocked(int jsonIndex, int id)
    {
        // Нет прогресса — разрешаем только первый JSON,
        // и только если пройден последний ручной уровень.
        if(_progress == null)
            return jsonIndex == 0 && LastManualCompleted();

        if(jsonIndex == 0)
            return LastManualCompleted();

        // Все последующие — если предыдущий JSON имеет >= 1 звезду
        int prevId = EncodeRuntimeId(jsonIndex - 1);
        return _progress.GetStars(prevId) > 0;
    }

    private bool LastManualCompleted()
    {
        if(_manualLevelsCount <= 0)
            return true; // ручных уровней нет — считаем условие выполненным

        if(_progress == null)
            return false;

        int lastBuildIndex = _firstBuildIndex + _manualLevelsCount - 1;
        return _progress.GetStars(lastBuildIndex) > 0;
    }

    private bool IsManualUnlocked(int buildIndex)
    {
        if(_progress == null)
            return buildIndex == _firstBuildIndex;

        if(buildIndex == _firstBuildIndex)
            return true;

        int prev = buildIndex - 1;

        // считается пройденным, если набрали >= 1 звезды
        return _progress.GetStars(prev) > 0;
    }

    // ===== Раздел: клик по уровню =====

    private void OnLevelClicked(int id)
    {
        if(!ServiceLocator.TryGet<ILevelFlow>(out var flowRaw))
        {
            Debug.LogError("[LevelSelectController] ILevelFlow service not found.");
            return;
        }

        var levelManager = flowRaw as LevelManager;

        // Ручной уровень (buildIndex)
        if(id >= 0)
        {
            if(!IsManualUnlocked(id))
            {
                Debug.Log("[LevelSelectController] Level is locked, cannot start.");
                return;
            }

            flowRaw.LoadLevel(id);
            return;
        }

        // JSON-уровень
        int jsonIndex = DecodeRuntimeId(id);
        if(!IsRuntimeUnlocked(jsonIndex, id))
        {
            Debug.Log("[LevelSelectController] Runtime level is locked, cannot start.");
            return;
        }

        if(levelManager == null)
        {
            Debug.LogError("[LevelSelectController] Runtime level clicked, but ILevelFlow is not LevelManager.");
            return;
        }

        levelManager.LoadRuntimeJsonLevel(jsonIndex);
    }

    // ===== Раздел: JSON config =====

    private void EnsureRuntimeConfigLoaded()
    {
        if(_runtimeConfig != null)
            return;

        if(_levelsJson == null)
            return;

        _runtimeConfig = JsonUtility.FromJson<LevelJsonRoot>(_levelsJson.text);
        if(_runtimeConfig == null || _runtimeConfig.levels == null)
            Debug.LogError("[LevelSelectController] Failed to parse levels.json for runtime levels UI.");
    }

    // ===== Кодирование/декодирование id =====

    private static int EncodeRuntimeId(int jsonIndex)
    {
        // 0 -> -1, 1 -> -2, ...
        return -1 - jsonIndex;
    }

    private static int DecodeRuntimeId(int id)
    {
        // -1 -> 0, -2 -> 1, ...
        return -id - 1;
    }
}
