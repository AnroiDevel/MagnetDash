using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProgressService : MonoBehaviour,
    IProgressService, ICurrencyService, ILevelCompletionService, IShopState
{
    [Header("Save")]
    [SerializeField] private string _slotId = "default";
    [SerializeField] private float _saveDebounce = 0.2f;

    [Header("Shop")]
    [SerializeField] private string _defaultSkinId = "default";

    public event Action Loaded;

    // progress events
    public event Action<int, int> StarsChanged;
    public event Action<int, float> BestTimeChanged;
    public event Action<int> EngineDurabilityChanged;

    // economy events
    public event Action<int> AmountChanged;

    // shop events
    public event Action<string> CurrentSkinChanged;

    public bool IsLoaded => _loaded;

    private ISaveStorage _storage;
    private bool _booted;
    private bool _loaded;

    private SaveData _state = new() { slotId = "default" };

    private readonly Dictionary<int, int> _stars = new();
    private readonly Dictionary<int, float> _bestTimes = new();
    private readonly HashSet<int> _completed = new();
    private readonly HashSet<string> _ownedSkins = new();

    private bool _saveScheduled;
    private Coroutine _saveRoutine;

    public int EngineDurability => Mathf.Clamp(_state.engineDurability, 0, 100);

    public float EnginePower
    {
        get
        {
            float dur = EngineDurability;
            return 0.5f + (dur / 100f) * 0.5f;
        }
    }

    public int Amount => _state.currency;

    public string CurrentSkinId => _state.currentSkinId ?? string.Empty;

    public void Boot(ISaveStorage storage)
    {
        if(_booted)
            return;

        _booted = true;
        _storage = storage;

        _state.slotId = _slotId;

        StartLoad();
    }

    private void Start()
    {
        if(!_booted)
            Debug.LogError("[ProgressService] Boot() was not called. Progress will stay unloaded.", this);
    }

    private void StartLoad()
    {
        _loaded = false;

        if(_storage == null || !_storage.IsAvailable)
        {
            ApplyState(new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 });
            return;
        }

        _storage.Load(
            _slotId,
            onOk: json =>
            {
                SaveData data = null;

                if(!string.IsNullOrEmpty(json))
                {
                    try
                    { data = JsonUtility.FromJson<SaveData>(json); }
                    catch(Exception e) { Debug.LogError($"[ProgressService] JSON parse error: {e}"); }
                }

                data ??= new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 };
                ApplyState(data);
            },
            onFail: reason =>
            {
                Debug.LogWarning($"[ProgressService] Load failed: {reason}. Using empty state.", this);
                ApplyState(new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 });
            }
        );
    }

    private void ApplyState(SaveData data)
    {
        _state = data ?? new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 };
        _state.slotId = _slotId;

        EnsureInitialized();
        RebuildCaches();

        _loaded = true;

        Loaded?.Invoke();

        AmountChanged?.Invoke(_state.currency);
        EngineDurabilityChanged?.Invoke(EngineDurability);
        CurrentSkinChanged?.Invoke(CurrentSkinId);
    }

    private void EnsureInitialized()
    {
        if(_state.engineDurability <= 0 || _state.engineDurability > 100)
            _state.engineDurability = 100;

        _state.levels ??= new List<LevelResultDto>();
        _state.completedLevels ??= new List<int>();

        _state.ownedSkins ??= new List<string>();
        _state.currentSkinId ??= string.Empty;

        EnsureDefaultSkinNoAutoSave();
    }

    private void EnsureDefaultSkinNoAutoSave()
    {
        var defaultId = string.IsNullOrEmpty(_defaultSkinId) ? "default" : _defaultSkinId.Trim();

        if(string.IsNullOrEmpty(_state.currentSkinId))
            _state.currentSkinId = defaultId;

        // НЕ сохраняем здесь автоматически (чтобы не перетирать облако при пустой загрузке)
        if(!_ownedSkins.Contains(_state.currentSkinId))
        {
            _ownedSkins.Add(_state.currentSkinId);
            _state.ownedSkins.Add(_state.currentSkinId);
        }
    }

    // ---------- currency ----------
    public bool CanSpend(int amount) => amount <= 0 || _state.currency >= amount;

    public bool TrySpend(int amount)
    {
        if(!_loaded)
            return false;

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
        if(!_loaded)
            return;

        if(amount <= 0)
            return;

        _state.currency += amount;
        ScheduleSave();
        AmountChanged?.Invoke(_state.currency);
    }

    // ---------- engine ----------
    public void DamageEngine(int amount)
    {
        if(amount <= 0 || !_loaded)
            return;

        int prev = _state.engineDurability;
        _state.engineDurability = Mathf.Clamp(_state.engineDurability - amount, 0, 100);

        if(_state.engineDurability == prev)
            return;

        ScheduleSave();
        EngineDurabilityChanged?.Invoke(_state.engineDurability);
    }

    public void RepairEngineFull()
    {
        if(!_loaded)
            return;

        int prev = _state.engineDurability;
        _state.engineDurability = 100;

        if(prev == 100)
            return;

        ScheduleSave(forceImmediate: true);
        EngineDurabilityChanged?.Invoke(EngineDurability);
    }

    // ---------- progress ----------
    public int GetStars(int buildIndex) => _stars.TryGetValue(buildIndex, out var s) ? s : 0;

    public bool SetStarsMax(int buildIndex, int stars)
    {
        if(!_loaded)
            return false;

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
        if(!_loaded)
            return false;

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

    public bool TryGetLastCompletedLevel(out int logicalIndex)
    {
        if(_state.lastCompletedLogicalLevel < 0)
        {
            logicalIndex = -1;
            return false;
        }

        logicalIndex = _state.lastCompletedLogicalLevel;
        return true;
    }

    public void SetLastCompletedLevelIfHigher(int logicalIndex)
    {
        if(!_loaded)
            return;

        if(logicalIndex < 0)
            return;

        if(_state.lastCompletedLogicalLevel >= logicalIndex)
            return;

        _state.lastCompletedLogicalLevel = logicalIndex;
        ScheduleSave();
    }

    // ---------- completion ----------
    public bool WasCompleted(int progressKey) => _completed.Contains(progressKey);

    public bool MarkCompleted(int progressKey)
    {
        if(!_loaded)
            return false;

        if(progressKey < 0)
            return false;

        if(!_completed.Add(progressKey))
            return false;

        _state.completedLevels.Add(progressKey);
        ScheduleSave();
        return true;
    }

    public void ResetAll()
    {
        if(!_loaded)
            return;

        _state = new SaveData { slotId = _slotId, lastCompletedLogicalLevel = -1 };
        EnsureInitialized();

        _stars.Clear();
        _bestTimes.Clear();
        _completed.Clear();

        ScheduleSave(forceImmediate: true);

        StarsChanged?.Invoke(-1, 0);
        AmountChanged?.Invoke(_state.currency);
        EngineDurabilityChanged?.Invoke(EngineDurability);
        CurrentSkinChanged?.Invoke(CurrentSkinId);
    }

    // ---------- shop (IShopState) ----------
    public bool IsOwned(string skinId)
    {
        if(!_loaded)
            return false;

        skinId = skinId?.Trim();
        return !string.IsNullOrEmpty(skinId) && _ownedSkins.Contains(skinId);
    }

    public IReadOnlyCollection<string> GetOwnedSkins() => _ownedSkins;

    public bool AddOwned(string skinId)
    {
        if(!_loaded)
            return false;

        skinId = skinId?.Trim();
        if(string.IsNullOrEmpty(skinId))
            return false;

        if(!_ownedSkins.Add(skinId))
            return false;

        _state.ownedSkins.Add(skinId);
        ScheduleSave(forceImmediate: true);
        return true;
    }

    public bool TrySetCurrent(string skinId)
    {
        if(!_loaded)
            return false;

        skinId = skinId?.Trim();
        if(string.IsNullOrEmpty(skinId))
            return false;

        if(!_ownedSkins.Contains(skinId))
            return false;

        if(_state.currentSkinId == skinId)
            return true;

        _state.currentSkinId = skinId;
        ScheduleSave(forceImmediate: true);
        CurrentSkinChanged?.Invoke(_state.currentSkinId);
        return true;
    }

    public void Initialize(string defaultSkinId, ISkinDatabase db)
    {
        // оставил совместимость с твоим интерфейсом магазина.
        if(!_loaded)
            return;

        if(!string.IsNullOrEmpty(defaultSkinId))
            _defaultSkinId = defaultSkinId.Trim();

        EnsureDefaultSkinNoAutoSave();
        CurrentSkinChanged?.Invoke(CurrentSkinId);
    }

    // ---------- caches ----------
    private void RebuildCaches()
    {
        _stars.Clear();
        _bestTimes.Clear();
        _completed.Clear();
        _ownedSkins.Clear();

        if(_state.levels != null)
        {
            foreach(var lr in _state.levels)
            {
                _stars[lr.levelId] = Mathf.Clamp(lr.stars, 0, 3);
                if(lr.hasBestTime && float.IsFinite(lr.bestTime))
                    _bestTimes[lr.levelId] = lr.bestTime;
            }
        }

        if(_state.completedLevels != null)
        {
            for(int i = 0; i < _state.completedLevels.Count; i++)
                _completed.Add(_state.completedLevels[i]);
        }

        if(_state.ownedSkins != null)
        {
            for(int i = 0; i < _state.ownedSkins.Count; i++)
            {
                var id = _state.ownedSkins[i]?.Trim();
                if(!string.IsNullOrEmpty(id))
                    _ownedSkins.Add(id);
            }
        }

        _state.currentSkinId = _state.currentSkinId?.Trim() ?? string.Empty;
    }

    private void UpsertLevel(int buildIndex, Func<LevelResultDto, LevelResultDto> mutate)
    {
        for(int i = 0; i < _state.levels.Count; i++)
        {
            if(_state.levels[i].levelId == buildIndex)
            {
                _state.levels[i] = mutate(_state.levels[i]);
                return;
            }
        }

        var created = new LevelResultDto { levelId = buildIndex };
        _state.levels.Add(mutate(created));
    }

    // ---------- save pipeline ----------
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
        if(_storage == null || !_storage.IsAvailable)
            return;

        string json = JsonUtility.ToJson(_state, prettyPrint: false);
        _storage.Save(_slotId, json);
    }
}
