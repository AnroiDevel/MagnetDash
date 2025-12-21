using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProgressUiBinder : MonoBehaviour
{
    private IProgressService _progress;
    private IUIService _ui;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IProgressService>(BindProgress);
        ServiceLocator.WhenAvailable<IUIService>(BindUi);
    }

    private void OnDisable()
    {
        ServiceLocator.Unsubscribe<IProgressService>(BindProgress);
        ServiceLocator.Unsubscribe<IUIService>(BindUi);

        UnbindProgress();
        _ui = null;
    }

    private void BindProgress(IProgressService progress)
    {
        if(ReferenceEquals(_progress, progress))
            return;

        UnbindProgress();
        _progress = progress;

        _progress.Loaded += Refresh;
        _progress.EngineDurabilityChanged += OnEngineChanged;

        Refresh();
    }

    private void BindUi(IUIService ui)
    {
        _ui = ui;
        Refresh();
    }

    private void UnbindProgress()
    {
        if(_progress == null)
            return;

        _progress.Loaded -= Refresh;
        _progress.EngineDurabilityChanged -= OnEngineChanged;
        _progress = null;
    }

    private void OnEngineChanged(int _) => Refresh();

    private void Refresh()
    {
        if(_progress == null || _ui == null)
            return;

        if(!_progress.IsLoaded)
            return;

        _ui.UpdateEngineDangerIndicator(_progress.EngineDurability);
    }
}
