// ProgressService.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProgressService : MonoBehaviour, IProgressService
{
    [Header("Storage")]
    [SerializeField] private string _slotId = "default";
    [SerializeField] private string _filePrefix = "save_";
    [SerializeField] private float _saveDebounce = 0.2f;

    public event Action<int, int> StarsChanged;
    public event Action<int, float> BestTimeChanged;

    private SaveData _state;
    private readonly Dictionary<int, int> _stars = new();         // buildIndex -> stars
    private readonly Dictionary<int, float> _bestTimes = new();   // buildIndex -> time
    private bool _saveScheduled;

    private string FilePath => Path.Combine(Application.persistentDataPath, $"{_filePrefix}{_slotId}.json");
    private string TempPath => FilePath + ".tmp";

    private void Awake()
    {
        LoadOrCreate();
        ServiceLocator.Register<IProgressService>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IProgressService>(this);
    }

    // ---------- Public API ----------


    public bool TryGetLastCompletedLevel(out int buildIndex)
    {
        buildIndex = default;

        if(_state == null || _state.levels == null || _state.levels.Count == 0)
            return false;

        int best = -1;

        foreach(var lr in _state.levels)
        {
            if(lr.stars > 0 && lr.buildIndex > best)
                best = lr.buildIndex;
        }

        if(best < 0)
            return false;

        buildIndex = best + 1;
        return true;
    }


    public int GetStars(int buildIndex) => _stars.TryGetValue(buildIndex, out var s) ? s : 0;

    public bool SetStarsMax(int buildIndex, int stars)
    {
        stars = Mathf.Clamp(stars, 0, 3);
        int prev = GetStars(buildIndex);
        if(stars <= prev)
            return false;

        _stars[buildIndex] = stars;
        UpsertLevel(buildIndex, lr => { lr.stars = stars; return lr; });
        StarsChanged?.Invoke(buildIndex, stars);
        ScheduleSave();
        return true;
    }

    public bool TryGetBestTime(int buildIndex, out float t)
    {
        if(_bestTimes.TryGetValue(buildIndex, out t))
            return true;
        t = float.MaxValue;
        return false;
    }

    public bool SetBestTimeIfBetter(int buildIndex, float t)
    {
        if(t <= 0f || !float.IsFinite(t))
            return false;
        if(TryGetBestTime(buildIndex, out var prev) && t >= prev)
            return false;

        _bestTimes[buildIndex] = t;
        UpsertLevel(buildIndex, lr => { lr.hasBestTime = true; lr.bestTime = t; return lr; });
        BestTimeChanged?.Invoke(buildIndex, t);
        ScheduleSave();
        return true;
    }

    public void ResetAll()
    {
        _state = new SaveData { slotId = _slotId };
        _stars.Clear();
        _bestTimes.Clear();
        WriteFileImmediate();
        StarsChanged?.Invoke(-1, 0);
    }

    // ---------- Internal ----------
    private void LoadOrCreate()
    {
        if(File.Exists(FilePath))
        {
            TryRead(out _state);
        }
        else
        {
            // Миграция с PlayerPrefs (старые ключи "stars_{idx}", "best_{idx}")
            _state = new SaveData { slotId = _slotId };
            TryMigrateFromPlayerPrefs(_state);
            WriteFileImmediate();
        }

        RebuildCaches();
    }

    private void RebuildCaches()
    {
        _stars.Clear();
        _bestTimes.Clear();
        foreach(var lr in _state.levels)
        {
            _stars[lr.buildIndex] = Mathf.Clamp(lr.stars, 0, 3);
            if(lr.hasBestTime && float.IsFinite(lr.bestTime))
                _bestTimes[lr.buildIndex] = lr.bestTime;
        }
    }


    private void UpsertLevel(int buildIndex, System.Func<LevelResultDto, LevelResultDto> mutate)
    {
        for(int i = 0; i < _state.levels.Count; i++)
        {
            if(_state.levels[i].buildIndex == buildIndex)
            {
                var lr = _state.levels[i];
                lr = mutate(lr);              // <- получаем изменённую копию
                _state.levels[i] = lr;        // <- сохраняем обратно
                return;
            }
        }

        var created = new LevelResultDto { buildIndex = buildIndex };
        created = mutate(created);
        _state.levels.Add(created);
    }

    private void ScheduleSave()
    {
        if(_saveScheduled)
            return;
        _saveScheduled = true;
        StartCoroutine(CoDebouncedSave());
    }

    private IEnumerator CoDebouncedSave()
    {
        float t = 0f;
        while(t < _saveDebounce)
        { t += Time.unscaledDeltaTime; yield return null; }
        WriteFileImmediate();
        _saveScheduled = false;
    }

    private void WriteFileImmediate()
    {
        try
        {
            var json = JsonUtility.ToJson(_state, prettyPrint: false);
            File.WriteAllText(TempPath, json);
            if(File.Exists(FilePath))
                File.Delete(FilePath);
            File.Move(TempPath, FilePath); // атомарнее, чем перезапись
        }
        catch(Exception e)
        {
            Debug.LogError($"Progress save error: {e}");
        }
    }

    private bool TryRead(out SaveData data)
    {
        try
        {
            string json = File.ReadAllText(FilePath);
            data = JsonUtility.FromJson<SaveData>(json);
            if(data == null)
            { data = new SaveData { slotId = _slotId }; return false; }
            if(string.IsNullOrEmpty(data.slotId))
                data.slotId = _slotId;
            return true;
        }
        catch(Exception e)
        {
            Debug.LogError($"Progress read error: {e}");
            data = new SaveData { slotId = _slotId };
            return false;
        }
    }

    private void TryMigrateFromPlayerPrefs(SaveData target)
    {
        // перебором по BuildSettings обычно нельзя узнать количество сцен — это делай лениво
        // мигрируем встречающиеся ключи 0..300 на всякий случай
        for(int idx = 0; idx <= 300; idx++)
        {
            string sk = $"stars_{idx}";
            string bk = $"best_{idx}";
            bool touched = false;

            var lr = new LevelResultDto { buildIndex = idx };

            if(PlayerPrefs.HasKey(sk))
            {
                lr.stars = Mathf.Clamp(PlayerPrefs.GetInt(sk, 0), 0, 3);
                touched = true;
            }
            if(PlayerPrefs.HasKey(bk))
            {
                float t = PlayerPrefs.GetFloat(bk, float.MaxValue);
                if(float.IsFinite(t) && t < float.MaxValue)
                {
                    lr.bestTime = t;
                    lr.hasBestTime = true;
                    touched = true;
                }
            }

            if(touched)
                target.levels.Add(lr);
        }
    }
}
