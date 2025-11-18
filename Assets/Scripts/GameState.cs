public enum GameState
{
    Boot,          // игра только запустилась, Systems грузятся
    MainMenu,      // главное меню
    LevelSelect,   // экран выбора уровня
    LoadingLevel,  // идёт загрузка сцены уровня
    Playing,       // активный геймплей
    Paused,        // пауза
    LevelCompleted,// игрок дошёл до портала, показываем результат
    LevelFailed    // игрок погиб, показываем результат
}
