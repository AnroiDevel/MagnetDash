using System;
using UnityEngine;

public interface IInputService
{
    // События геймплея
    event Action<Vector2> Move;          
    event Action TogglePolarity;

    // Глобальные
    event Action Back;                   // системная «назад» для модалок

    // Управление режимом
    void EnableGameplay(bool on);        // в паузе off, при резюме on
    void PushModal();                    // когда открыт модальный экран (настройки и т.п.)
    void PopModal();
    bool IsModalOpen { get; }

    // Текущее состояние (по желанию)
    bool GameplayEnabled { get; }
}
