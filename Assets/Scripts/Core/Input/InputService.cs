using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class InputService : MonoBehaviour, IInputService
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset _asset;
    private string _gameplayMap = "Player";
    private string _globalMap = "UI";

    [Header("Action names (внутри карт)")]
    private string _actMove = "Move";
    private string _actTogglePolarity = "Attack";
    private string _actBack = "Cancel";

    // Events
    public event Action<Vector2> Move;
    public event Action TogglePolarity;
    public event Action Pause;
    public event Action Back;

    private InputActionMap _mapGameplay;
    private InputActionMap _mapGlobal;
    private InputAction _aMove, _aToggle, _aPause;

    private int _modalDepth;

    public bool GameplayEnabled => _mapGameplay?.enabled ?? false;
    public bool IsModalOpen => _modalDepth > 0;

    private void Awake()
    {
        // Клонируем asset, чтобы не мутировать оригинал в рантайме
        if(_asset)
            _asset = Instantiate(_asset);

        _mapGameplay = _asset.FindActionMap(_gameplayMap, throwIfNotFound: true);
        _mapGlobal = _asset.FindActionMap(_globalMap, throwIfNotFound: true);

        _aMove = _mapGameplay.FindAction(_actMove, throwIfNotFound: false);
        _aToggle = _mapGameplay.FindAction(_actTogglePolarity, throwIfNotFound: true);
        _aPause = _mapGlobal.FindAction(_actBack, throwIfNotFound: true);

        // Подписки
        if(_aMove != null)
        {
            _aMove.performed += ctx => Move?.Invoke(ctx.ReadValue<Vector2>());
            _aMove.canceled += ctx => Move?.Invoke(Vector2.zero);
        }

        _aToggle.performed += _ => TogglePolarity?.Invoke();

        // Одна кнопка Cancel, внутри решаем: Back или Pause
        _aPause.performed += _ => OnPausePressed();

        // Включаем карты
        _mapGameplay.Enable();
        _mapGlobal.Enable();

        // Регистрируем сервис
        ServiceLocator.Register<IInputService>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IInputService>(this);
    }

    public void EnableGameplay(bool on)
    {
        if(on)
            _mapGameplay.Enable();
        else
            _mapGameplay.Disable();
    }

    public void PushModal() => _modalDepth++;
    public void PopModal()
    {
        if(_modalDepth > 0)
            _modalDepth--;
    }

    private void OnPausePressed()
    {
        // Если открыт модальный — трактуем как «назад» (закрыть верхний модал)
        if(IsModalOpen)
        {
            Back?.Invoke();
        }
        else
        {
            Pause?.Invoke();
        }
    }
}
