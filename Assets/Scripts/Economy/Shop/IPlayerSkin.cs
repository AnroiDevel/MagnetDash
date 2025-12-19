public interface IPlayerSkin
{
    // Фактическая скорость (модуль v), а не 0..1:
    void OnSpeedChanged(float speedWorld);
    void OnPolarityChanged(int polarity);   // -1 / 0 / +1
    void OnStarCollected();
    void OnHit();
    void OnDeath();
}
