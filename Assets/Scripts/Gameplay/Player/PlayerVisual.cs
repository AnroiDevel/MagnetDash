using UnityEngine;

public sealed class PlayerVisual : MonoBehaviour, IPlayerSkin
{
    private IShopService _shop;
    private ISkinDatabase _db;
    private IGameEvents _events;

    private IPlayerSkin _currentSkin;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IShopService>(s =>
        {
            _shop = s;
            _shop.CurrentSkinChanged += OnSkinChanged;
            TryApplySkin();
        });

        ServiceLocator.WhenAvailable<ISkinDatabase>(d =>
        {
            _db = d;
            TryApplySkin();
        });

        ServiceLocator.WhenAvailable<IGameEvents>(OnGameEventsAvailable);
    }

    private void OnDisable()
    {
        if(_shop != null)
            _shop.CurrentSkinChanged -= OnSkinChanged;

        if(_events != null)
        {
            _events.PolarityChanged -= HandlePolarityChanged;
            _events.SpeedChanged -= HandleSpeedChanged;
            // если нужно Ц здесь же можно отписатьс€ и от других событий
        }
    }

    // ---------------- SKINS ----------------

    private void OnSkinChanged(string id)
    {
        TryApplySkin();
    }

    private void TryApplySkin()
    {
        if(_shop == null || _db == null)
            return;

        var def = _db.GetById(_shop.CurrentSkinId);
        if(def == null || def.prefab == null)
            return;

        // удалить старый скин
        if(transform.childCount > 0)
            Destroy(transform.GetChild(0).gameObject);

        // создать новый
        var instance = Instantiate(def.prefab, transform);

        // prefab должен содержать компонент, реализующий IPlayerSkin (обычно PlayerSkinView)
        _currentSkin = instance as IPlayerSkin;
        if(_currentSkin == null)
            _currentSkin = instance.GetComponentInChildren<IPlayerSkin>();

        if(_currentSkin == null)
            return;

        // --- ”—“јЌј¬Ћ»¬ј≈ћ Ќј„јЋ№Ќќ≈ —ќ—“ќяЌ»≈ ---

        // ЅерЄм текущую пол€рность и скорость у PlayerMagnet
        var magnet = GetComponentInParent<PlayerMagnet>();
        if(magnet != null)
        {
            _currentSkin.OnPolarityChanged(magnet.Polarity);

            var rb = magnet.GetComponent<Rigidbody2D>();
            if(rb != null)
                _currentSkin.OnSpeedChanged(rb.linearVelocity.magnitude);
        }
    }

    // ---------------- EVENTS ----------------

    private void OnGameEventsAvailable(IGameEvents e)
    {
        // на случай повторных WhenAvailable
        if(_events != null)
        {
            _events.PolarityChanged -= HandlePolarityChanged;
            _events.SpeedChanged -= HandleSpeedChanged;
        }

        _events = e;

        if(_events == null)
            return;

        _events.PolarityChanged += HandlePolarityChanged;
        _events.SpeedChanged += HandleSpeedChanged;
    }

    private void HandlePolarityChanged(int sign)
    {
        _currentSkin?.OnPolarityChanged(sign);
    }

    private void HandleSpeedChanged(float speed)
    {
        _currentSkin?.OnSpeedChanged(speed);
    }

    // ---------------- IPlayerSkin (дл€ пр€мых вызовов, если где-то остались) ----------------

    public void OnSpeedChanged(float speed)
    {
        _currentSkin?.OnSpeedChanged(speed);
    }

    public void OnPolarityChanged(int polarity)
    {
        _currentSkin?.OnPolarityChanged(polarity);
    }

    public void OnStarCollected()
    {
        _currentSkin?.OnStarCollected();
    }

    public void OnHit()
    {
        _currentSkin?.OnHit();
    }

    public void OnDeath()
    {
        _currentSkin?.OnDeath();
    }

    // старые методы-обЄртки на случай, если кто-то ещЄ их вызывает
    public void ApplySpeed(float speed01) => OnSpeedChanged(speed01);
    public void ApplyPolarity(int p) => OnPolarityChanged(p);
    public void ApplyHit() => OnHit();
}
