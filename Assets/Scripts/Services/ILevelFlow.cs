public interface ILevelFlow
{
    void CompleteLevel();
    void KillPlayer();

    void Reload();
    void LoadNext();

    void LoadLevel(int buildIndex);
    void LoadMenu();
    void LoadByLogicalIndex(int logicalIndex);

    void Pause();
    void Resume();
}
