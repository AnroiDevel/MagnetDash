public interface ILevelCompletionService
{
    bool WasCompleted(int progressKey);
    bool MarkCompleted(int progressKey); // true только при первом завершении
}
