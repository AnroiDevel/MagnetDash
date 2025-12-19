using UnityEngine;

[DisallowMultipleComponent]
public sealed class PauseMenuActions : MonoBehaviour
{
    [SerializeField] private GameplayPauseView _pauseView; // для открытия настроек (опционально)

    public void Continue()
    {
        if(ServiceLocator.TryGet<PauseController>(out var pc))
            pc.OnPausePressed(); // toggle pause -> resume
    }

    public void RestartLevel()
    {
        // ВАЖНО: Reload вызываем пока State == Paused
        if(ServiceLocator.TryGet<ILevelFlow>(out var flow))
            flow.Reload();

        // После запуска загрузки выходим из паузы принудительно (иначе timeScale может остаться 0)
        if(ServiceLocator.TryGet<PauseController>(out var pc))
            pc.ForceResume();
    }

    public void OpenSettings()
    {
        if(_pauseView != null)
            _pauseView.OpenSettings();
    }

    public void ExitToMenu()
    {
        if(ServiceLocator.TryGet<ILevelFlow>(out var flow))
            flow.LoadMenu();

        if(ServiceLocator.TryGet<PauseController>(out var pc))
            pc.ForceResume();
    }
}
