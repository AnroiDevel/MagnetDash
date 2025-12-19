using System;

public interface IModalService
{
    bool IsOpen { get; }

    /// <summary>Открыть модалку: заморозить игру (без смены GameState), открыть UI.</summary>
    void Show(Action onShow, Action onHide = null);

    /// <summary>Закрыть текущую модалку и разморозить игру.</summary>
    void Close();
}
