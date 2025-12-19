using UnityEngine;

public sealed class ShopBootstrap : MonoBehaviour
{
    [SerializeField] private SkinDatabase _skinDatabase;
    [SerializeField] private string _defaultSkinId = "default";

    private SkinDatabaseService _dbService;
    private ShopService _shopService;

    private void Awake()
    {
        // Ждём пока загрузится прогресс → появится ICurrencyService
        ServiceLocator.WhenAvailable<ICurrencyService>(OnCurrencyReady);
    }

    private void OnCurrencyReady(ICurrencyService currency)
    {
        if(_shopService != null)
            return;

        // 1. Создаём runtime-базу скинов
        _dbService = new SkinDatabaseService(_skinDatabase);
        ServiceLocator.Register<ISkinDatabase>(_dbService);

        // 2. Создаём магазин
        _shopService = new ShopService(currency, _dbService, _defaultSkinId);
        ServiceLocator.Register<IShopService>(_shopService);
    }

    private void OnDestroy()
    {
        if(_shopService != null)
            ServiceLocator.Unregister<IShopService>(_shopService);

        if(_dbService != null)
            ServiceLocator.Unregister<ISkinDatabase>(_dbService);
    }
}
