using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PauseController : MonoBehaviour
{
    public bool IsPaused { get; private set; }
    public event Action<bool> PausedChanged;

    /// <summary>
    /// UI может подписаться и вернуть true, если обработал Back (например, закрыл настройки).
    /// </summary>
    public event Func<bool> TryConsumeBack;

    private IInputService _input;

    // модальная “заморозка” (ремонт/магазин/прочее) — без GameState и без UI
    private int _modalFreezeCount;

    private void OnEnable()
    {
        ServiceLocator.Register(this);
        ServiceLocator.WhenAvailable<IInputService>(svc =>
        {
            _input = svc;
            _input.Back += OnBack;
            _input.Pause += OnPause;
        });
    }

    private void OnDisable()
    {
        if(_input == null)
            return;

        _input.Back -= OnBack;
        _input.Pause -= OnPause;
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister(this);
    }



    private void OnPause()
    {
        OnPausePressed();
    }

    public void OnPausePressed()
    {
        if(!ServiceLocator.TryGet<LevelManager>(out var lm))
            return;

        switch(lm.State)
        {
            case GameState.Playing:
                lm.Pause();
                PauseCore();
                break;

            case GameState.Paused:
                lm.Resume();
                ResumeCore();
                break;
        }
    }

    private void OnBack()
    {
        // 1) Всегда даём шанс модалкам/настройкам закрыться
        var handler = TryConsumeBack;
        if(handler != null)
        {
            foreach(Func<bool> h in handler.GetInvocationList())
            {
                if(h.Invoke())
                    return;
            }
        }

        // 2) Если никто не обработал Back — это "пауза/резюм"
        OnPausePressed();
    }

    private void PauseCore()
    {
        if(IsPaused)
            return;

        IsPaused = true;

        // не трогаем modalFreezeCount — это отдельный слой
        Time.timeScale = 0f;
        _input?.EnableGameplay(false);

        PausedChanged?.Invoke(true);
    }
    public void ForceResume()
    {
        if(!IsPaused)
            return;

        IsPaused = false;

        Time.timeScale = _modalFreezeCount > 0 ? 0f : 1f;
        _input?.EnableGameplay(_modalFreezeCount == 0);

        PausedChanged?.Invoke(false);
    }

    private void ResumeCore()
    {
        if(!IsPaused)
            return;

        IsPaused = false;

        Time.timeScale = _modalFreezeCount > 0 ? 0f : 1f;
        _input?.EnableGameplay(_modalFreezeCount == 0);

        PausedChanged?.Invoke(false);
    }

    // --------- Modal freeze API (для ModalService) ---------

    public void BeginModalFreeze()
    {
        _modalFreezeCount++;
        if(_modalFreezeCount > 1)
            return;

        Time.timeScale = 0f;
        _input?.EnableGameplay(false);
        _input?.PushModal();
    }

    public void EndModalFreeze()
    {
        if(_modalFreezeCount <= 0)
            return;

        _modalFreezeCount--;
        if(_modalFreezeCount > 0)
            return;

        _input?.PopModal();

        Time.timeScale = IsPaused ? 0f : 1f;
        _input?.EnableGameplay(!IsPaused);
    }
}
