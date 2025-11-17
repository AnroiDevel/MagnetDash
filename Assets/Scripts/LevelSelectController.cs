using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class LevelSelectController : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private RectTransform _contentRoot;  // Grid/Vertical parent
    [SerializeField] private LevelItemView _itemPrefab;   // префаб карточки
    [SerializeField] private bool _clearOnBuild = true;

    [Header("Levels")]
    [SerializeField] private int _firstBuildIndex = 1;    // первая сцена-уровень
    [SerializeField] private int _levelsToShow = 12;      // 0 = до конца BuildSettings

    private readonly List<LevelItemView> _items = new();
    private IProgressService _progress;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IProgressService>(p =>
        {
            _progress = p;
            BuildOrRefresh();
        });

        // Если прогресс ещё не зарегистрирован — отрисуем без него
        if(_progress == null)
            BuildOrRefresh();
    }

    public void BuildOrRefresh()
    {
        if(_contentRoot == null || _itemPrefab == null)
            return;

        int totalScenes = SceneManager.sceneCountInBuildSettings;
        int count = _levelsToShow > 0 ? _levelsToShow : (totalScenes - _firstBuildIndex);
        if(count < 0)
            count = 0;

        // (Re)build
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

            int stars = _progress != null ? _progress.GetStars(buildIndex) : 0;
            bool unlocked = IsUnlocked(buildIndex);

            _items[i].gameObject.name = $"Level_{number}";
            _items[i].Bind(buildIndex, number, Mathf.Clamp(stars, 0, 3), unlocked, OnLevelClicked);
        }
    }

    private bool IsUnlocked(int buildIndex)
    {
        if(_progress == null)
            return buildIndex == _firstBuildIndex; // пока грузится сервис — открыт только 1-й
        if(buildIndex == _firstBuildIndex)
            return true;
        int prev = buildIndex - 1;
        return _progress.GetStars(prev) > 0; // откроется после ≥1 звезды на предыдущем
    }

    private void OnLevelClicked(int buildIndex)
    {
        // Запуск уровня: грузим аддитивно, выгружаем лишнее, Systems оставляем
        StartCoroutine(CoLoadLevel(buildIndex));
    }

    private System.Collections.IEnumerator CoLoadLevel(int buildIndex)
    {
        var systems = SceneManager.GetSceneByName("Systems");
        var current = SceneManager.GetActiveScene();

        yield return SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Additive);

        // найдём только что загруженную сцену
        Scene loaded = default;
        for(int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if(s.isLoaded && s.buildIndex == buildIndex)
            { loaded = s; break; }
        }
        SceneManager.SetActiveScene(loaded);
        yield return null;

        // выгрузим всё, кроме Systems и загруженного уровня
        for(int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if(!s.isLoaded)
                continue;
            if(s == systems || s == loaded)
                continue;
            yield return SceneManager.UnloadSceneAsync(s);
        }
    }
}
