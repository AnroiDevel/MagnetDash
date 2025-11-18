public interface ILevelFlow
{
    void CompleteLevel();
    void KillPlayer();

    void Reload();
    void LoadNext();

    void LoadLevel(int buildIndex);
    void LoadMenu();
}
