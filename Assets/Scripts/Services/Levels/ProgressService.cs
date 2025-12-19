using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProgressService : MonoBehaviour, IProgressService, ICurrencyService
{
    [Header("Storage (local file)")]
    [SerializeField] private string _slotId = "default";
    [SerializeField] private string _filePrefix = "save_";
    [SerializeField] private float _saveDebounce = 0.2f;

    [Header("Cloud (VK)")]
    [SerializeField] private string _vkStorageKey = "magnet_save_default";

    public event Action Loaded;
    public event Action<int, int> StarsChanged;
    public event Action<int, float> BestTimeChanged;
    public event Action<int> EngineDurabilityChanged;

    public event Action<int> AmountChanged;

    private SaveData _state = new() { slotId = "default" };
    private readonly Dictionary<int, int> _stars = new();         // buildIndex/id -> stars
    private readonly Dictionary<int, float> _bestTimes = new();   // buildIndex/id -> time
    private bool _saveScheduled;
    private bool _loaded;     // данные загружены (для WebGL/облака)

    private string FilePath => Path.Combine(Application.persistentDataPath, $"{_filePrefix}{_slotId}.json");
    private string TempPath => FilePath + ".tmp";

#if UNITY_WEBGL

    [DllImport("__Internal")] private static extern void VK_SaveString(string key, string value);
    [DllImport("__Internal")] private static extern void VK_LoadString(string key, string goName, string methodName);
#endif

    public bool IsLoaded => _loaded;

    private Coroutine _saveRoutine;

    private void Awake()
    {
        if(ServiceLocator.TryGet<IProgressService>(out var existing) && !ReferenceEquals(existing, this))
        {
            Debug.LogWarning("[ProgressService] Duplicate instance detected. Destroying new one.");
            Destroy(gameObject);
            return;
        }

        _state.slotId = _slotId;

        ServiceLocator.Register<IProgressService>(this);
        ServiceLocator.Register<ICurrencyService>(this);

#if UNITY_WEBGL && !UNITY_EDITOR
    if(IsVkEnvironment())
    {
        // грузим только из облака VK
        StartCoroutine(CoLoadFromVkCloud());
    }
    else
    {
        // обычное локальное сохранение
        LoadOrCreateLocal();
    }
#else
        // Editor / Standalone / прочий runtime
        LoadOrCreateLocal();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
private bool IsVkEnvironment()
{
    string url = Application.absoluteURL;
    if(string.IsNullOrEmpty(url))
        return false;

    url = url.ToLowerInvariant();
    return url.Contains("vk.com")
        || url.Contains("m.vk.com")
        || url.Contains("vk.ru")
        || url.Contains("vkplay.ru");
}
#endif


    private void OnDestroy()
    {
        ServiceLocator.Unregister<IProgressService>(this);
        ServiceLocator.Unregister<ICurrencyService>(this);
    }

    // ================= PUBLIC API =================

    public int EngineDurability => Mathf.Clamp(_state.engineDurability, 0, 100);

    public float EnginePower
    {
        get
        {
            // 100 → 1.0; 0 → 0.5
            float dur = EngineDurability;
            return (0.5f + (dur / 100f) * 0.5f);
        }
    }


    public void DamageEngine(int amount)
    {
        if(amount <= 0 || !_loaded)
            return;

        Debug.Log($"[ProgressService] DamageEngine amount={amount} before={_state.engineDurability}\n{Environment.StackTrace}");

        int prev = _state.engineDurability;
        _state.engineDurability = Mathf.Clamp(_state.engineDurability - amount, 0, 100);

        if(_state.engineDurability == prev)
            return;

        ScheduleSave();
        EngineDurabilityChanged?.Invoke(_state.engineDurability);

        if(ServiceLocator.TryGet<IUIService>(out var ui))
            ui.UpdateEngineDangerIndicator(_state.engineDurability);
    }

    public void RepairEngineFull()
    {
        Debug.Log($"[ProgressService] RepairEngineFull instance={GetHashCode()}");


        int prev = _state.engineDurability;
        _state.engineDurability = 100;

        if(prev == 100)
            return;

        ScheduleSave(forceImmediate: true);
        EngineDurabilityChanged?.Invoke(_state.engineDurability);

        if(ServiceLocator.TryGet<IUIService>(out var ui))
            ui.UpdateEngineDangerIndicator(_state.engineDurability);
    }

    // ===== CURRENCY =====

    public int Amount => _state.currency;

    public bool CanSpend(int amount)
    {
        if(amount <= 0)
            return true;
        return _state.currency >= amount;
    }

    public bool TrySpend(int amount)
    {
        if(amount < 0)
            return false;

        if(_state.currency < amount)
            return false;

        _state.currency -= amount;
        ScheduleSave();
        AmountChanged?.Invoke(_state.currency);
        return true;
    }

    public void Add(int amount)
    {
        if(amount <= 0)
            return;

        _state.currency += amount;
        ScheduleSave();
        AmountChanged?.Invoke(_state.currency);
    }



    public bool TryGetLastCompletedLevel(out int lastCLevel)
    {
        if(_state == null || _state.lastCompletedLogicalLevel < 0)
        {
            lastCLevel = -1;
            return false;
        }

        lastCLevel = _state.lastCompletedLogicalLevel;
        return true;
    }

    public void SetLastCompletedLevelIfHigher(int logicalIndex)
    {
        if(logicalIndex < 0)
            return;

        if(_state.lastCompletedLogicalLevel >= logicalIndex)
            return;

        _state.lastCompletedLogicalLevel = logicalIndex;
        ScheduleSave();
    }

    public int GetStars(int buildIndex) =>
        _stars.TryGetValue(buildIndex, out var s) ? s : 0;

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
        _state = new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 };
        EnsureProgressInitialized();

        _stars.Clear();
        _bestTimes.Clear();

        ScheduleSave(forceImmediate: true);

        StarsChanged?.Invoke(-1, 0);
        AmountChanged?.Invoke(_state.currency);
    }

    // ================= INTERNAL: LOAD =================

    /// <summary>
    /// Локальная загрузка/создание (Editor, Standalone, WebGL без облака).
    /// </summary>
    private void LoadOrCreateLocal()
    {
        if(File.Exists(FilePath))
        {
            TryReadLocal(out _state);
        }
        else
        {
            _state = new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 };
            TryMigrateFromPlayerPrefs(_state);
            WriteFileImmediateLocal();
        }

        EnsureProgressInitialized();   // <-- вместо EnsureEngineInitialized
        RebuildCaches();
        MarkLoaded();
    }


    private void EnsureEngineInitialized()
    {
        if(_state.engineDurability <= 0 || _state.engineDurability > 100)
            _state.engineDurability = 100;
    }


    private void EnsureProgressInitialized()
    {
        // 1) двигатель
        if(_state.engineDurability <= 0 || _state.engineDurability > 100)
            _state.engineDurability = 100;

        // 2) прогресс уровней
        // Если сейв реально пустой (нет записей уровней), но lastCompleted == 0,
        // это почти наверняка "дефолт int", а не реальный прогресс.
        if((_state.levels == null || _state.levels.Count == 0) && _state.lastCompletedLogicalLevel == 0)
            _state.lastCompletedLogicalLevel = -1;
    }

    private void MarkLoaded()
    {
        _loaded = true;
        Loaded?.Invoke();

        AmountChanged?.Invoke(_state.currency);

        if(ServiceLocator.TryGet<IUIService>(out var ui))
            ui.UpdateEngineDangerIndicator(EngineDurability);
    }


#if UNITY_WEBGL 
    /// <summary>
    /// Загрузка из VK Storage (WebGL).
    /// </summary>
    private IEnumerator CoLoadFromVkCloud()
    {
        _loaded = false;

        // дергаем JS → он вернёт строку через SendMessage
        VK_LoadString(_vkStorageKey, gameObject.name, nameof(OnVkStorageLoaded));

        // ждём, пока коллбек выставит _loaded = true
        float timeout = 5f;
        float t = 0f;
        while(!_loaded && t < timeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if(!_loaded)
        {
            Debug.LogWarning("[ProgressService] VK cloud load timeout, fallback to empty state.");
            _state = new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 };
            EnsureProgressInitialized();
            RebuildCaches();
            MarkLoaded();
        }
    }

    /// <summary>
    /// Коллбек из JS. Получаем JSON или пустую строку.
    /// </summary>
    private void OnVkStorageLoaded(string json)
    {
        if(string.IsNullOrEmpty(json))
        {
            _state = new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 };
            TryMigrateFromPlayerPrefs(_state);
        }
        else
        {
            try
            {
                var data = JsonUtility.FromJson<SaveData>(json);
                _state = data ?? new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 };
            }
            catch(Exception e)
            {
                Debug.LogError($"[ProgressService] VK load parse error: {e}");
                _state = new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 };
            }
        }

        EnsureProgressInitialized();   // <-- вместо EnsureEngineInitialized
        RebuildCaches();
        MarkLoaded();
    }
#endif

    private void RebuildCaches()
    {
        _stars.Clear();
        _bestTimes.Clear();
        if(_state?.levels == null)
            return;

        foreach(var lr in _state.levels)
        {
            _stars[lr.levelId] = Mathf.Clamp(lr.stars, 0, 3);
            if(lr.hasBestTime && float.IsFinite(lr.bestTime))
                _bestTimes[lr.levelId] = lr.bestTime;
        }
    }

    private void UpsertLevel(int buildIndex, Func<LevelResultDto, LevelResultDto> mutate)
    {
        for(int i = 0; i < _state.levels.Count; i++)
        {
            if(_state.levels[i].levelId == buildIndex)
            {
                var lr = _state.levels[i];
                lr = mutate(lr);
                _state.levels[i] = lr;
                return;
            }
        }

        var created = new LevelResultDto { levelId = buildIndex };
        created = mutate(created);
        _state.levels.Add(created);
    }

    // ================= INTERNAL: SAVE =================

    private void ScheduleSave(bool forceImmediate = false)
    {
        if(!_loaded)
            return;

        if(forceImmediate)
        {
            if(_saveRoutine != null)
            {
                StopCoroutine(_saveRoutine);
                _saveRoutine = null;
            }

            _saveScheduled = false;
            WriteImmediate();
            return;
        }

        if(_saveScheduled)
            return;

        _saveScheduled = true;
        _saveRoutine = StartCoroutine(CoDebouncedSave());
    }

    private IEnumerator CoDebouncedSave()
    {
        float t = 0f;
        while(t < _saveDebounce)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        WriteImmediate();
        _saveScheduled = false;
        _saveRoutine = null;
    }

    private void WriteImmediate()
    {
        var json = JsonUtility.ToJson(_state, prettyPrint: false);

#if UNITY_WEBGL && !UNITY_EDITOR       
    if (IsVkEnvironment())
    {
        VK_SaveString(_vkStorageKey, json);
    }
    else
    {
        WriteFileImmediateLocal(json);
    }
#else
        WriteFileImmediateLocal(json);
#endif
    }

    private void WriteFileImmediateLocal()
    {
        var json = JsonUtility.ToJson(_state, prettyPrint: false);
        WriteFileImmediateLocal(json);
    }

    private void WriteFileImmediateLocal(string json)
    {
        try
        {
            File.WriteAllText(TempPath, json);
            if(File.Exists(FilePath))
                File.Delete(FilePath);
            File.Move(TempPath, FilePath);
        }
        catch(Exception e)
        {
            Debug.LogError($"[ProgressService] local save error: {e}");
        }
    }

    private bool TryReadLocal(out SaveData data)
    {
        try
        {
            string json = File.ReadAllText(FilePath);
            data = JsonUtility.FromJson<SaveData>(json);
            if(data == null)
            {
                data = new SaveData { slotId = _slotId };
                return false;
            }
            if(string.IsNullOrEmpty(data.slotId))
                data.slotId = _slotId;
            return true;
        }
        catch(Exception e)
        {
            Debug.LogError($"[ProgressService] local read error: {e}");
            data = new SaveData { slotId = _slotId };
            return false;
        }
    }

    private void TryMigrateFromPlayerPrefs(SaveData target)
    {
        for(int idx = 0; idx <= 300; idx++)
        {
            string sk = $"stars_{idx}";
            string bk = $"best_{idx}";
            bool touched = false;

            var lr = new LevelResultDto { levelId = idx };

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
