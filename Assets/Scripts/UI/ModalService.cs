using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ModalService : MonoBehaviour, IModalService
{
    private PauseController _pause;
    private Action _onHide;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        ServiceLocator.Register<IModalService>(this);
        ServiceLocator.WhenAvailable<PauseController>(p => _pause = p);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IModalService>(this);
        // Unsubscribe не делаю: зависит от реализации ServiceLocator.
    }

    public void Show(Action onShow, Action onHide = null)
    {
        if(IsOpen)
            Close();

        IsOpen = true;
        _onHide = onHide;

        _pause?.BeginModalFreeze();
        onShow?.Invoke();
    }

    public void Close()
    {
        if(!IsOpen)
            return;

        IsOpen = false;

        var hide = _onHide;
        _onHide = null;
        hide?.Invoke();

        _pause?.EndModalFreeze();
    }
}
