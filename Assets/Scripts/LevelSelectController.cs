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

    [Header("Levels")]
    [SerializeField] private int _firstBuildIndex = 1;
    [SerializeField] private int _levelsToShow = 12;

    private readonly List<LevelItemView> _items = new();
    private IProgressService _progress;
    private bool _builtOnce;

    private void OnEnable()
    {
        // Если прогресс уже есть — строим сразу
        if(ServiceLocator.TryGet(out _progress))
        {
            BuildOrRefresh();
            return;
        }

        // Если появится позже — перестроим один раз
        ServiceLocator.WhenAvailable<IProgressService>(p =>
        {
            _progress = p;

            // защита от двойного Build
            if(!_builtOnce)
                BuildOrRefresh();
        });

        // Если прогресс ещё не найден — строим "пустую" версию
        BuildOrRefresh();
    }

    public void BuildOrRefresh()
    {
        if(_contentRoot == null || _itemPrefab == null)
            return;

        _builtOnce = true;

        int totalScenes = SceneManager.sceneCountInBuildSettings;
        int count = _levelsToShow > 0 ? _levelsToShow : (totalScenes - _firstBuildIndex);
        if(count < 0)
            count = 0;

        // Rebuild (если нужно)
        if(_items.Count != count)
        {
            if(_clearOnBuild)
            {
                foreach(var it in _items)
                    if(it)
                        Destroy(it.gameObject);

                _items.Clear();
            }

            for(int i = 0; i < count; i++)
            {
                var view = Instantiate(_itemPrefab, _contentRoot);
                _items.Add(view);
            }
        }

        // Bind
        for(int i = 0; i < count; i++)
        {
            int buildIndex = _firstBuildIndex + i;
            int number = i + 1;

            int stars = _progress?.GetStars(buildIndex) ?? 0;
            bool unlocked = IsUnlocked(buildIndex);

            _items[i].name = $"Level_{number}";
            _items[i].Bind(buildIndex, number, Mathf.Clamp(stars, 0, 3), unlocked, OnLevelClicked);
        }
    }

    private bool IsUnlocked(int buildIndex)
    {
        if(_progress == null)
            return buildIndex == _firstBuildIndex;

        if(buildIndex == _firstBuildIndex)
            return true;

        int prev = buildIndex - 1;

        // лучшее условие — считаем уровень пройденным, если он игрался и набрал >= 1 звезды
        return _progress.GetStars(prev) > 0;
    }

    private void OnLevelClicked(int levelBuildIndex)
    {
        if(ServiceLocator.TryGet<ILevelFlow>(out var flow))
        {
            flow.LoadLevel(levelBuildIndex);
        }
        else
        {
            Debug.LogError("[LevelSelectController] ILevelFlow service not found.");
        }
    }
}
