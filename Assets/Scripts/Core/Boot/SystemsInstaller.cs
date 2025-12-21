using UnityEngine;

[DisallowMultipleComponent]
public sealed class SystemsInstaller : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private ProgressService _progress; // теперь он же и shop-state

    [Header("Shop")]
    [SerializeField] private SkinDatabase _skinDatabase;
    [SerializeField] private string _defaultSkinId = "default";

    [Header("Storage")]
    [SerializeField] private VkCloudStorage _vkCloud;  // optional
    [SerializeField] private string _localPrefix = "save_";

    private ISaveStorage _storage;
    private SkinDatabaseService _db;
    private ShopService _shop;

    private bool _installed;

    private void Awake()
    {
        if(_installed)
            return;

        if(_progress == null)
        {
            Debug.LogError("[SystemsInstaller] ProgressService is not assigned.", this);
            enabled = false;
            return;
        }

        if(_skinDatabase == null)
        {
            Debug.LogError("[SystemsInstaller] SkinDatabase is not assigned.", this);
            enabled = false;
            return;
        }

        _storage = BuildStorage();

        // прогресс
        ServiceLocator.Register<IProgressService>(_progress);
        ServiceLocator.Register<ICurrencyService>(_progress);
        ServiceLocator.Register<ILevelCompletionService>(_progress);

        // shop state (из прогресса)
        ServiceLocator.Register<IShopState>(_progress);

        _progress.Boot(_storage);

        // db
        _db = new SkinDatabaseService(_skinDatabase);
        ServiceLocator.Register<ISkinDatabase>(_db);

        // shop service
        _shop = new ShopService(_progress, _db, _progress, _defaultSkinId);
        ServiceLocator.Register<IShopService>(_shop);

        _installed = true;
    }

    private ISaveStorage BuildStorage()
    {
        if(_vkCloud != null && _vkCloud.IsAvailable)
            return _vkCloud;

        return new LocalFileStorage(Application.persistentDataPath, _localPrefix);
    }

    private void OnDestroy()
    {
        if(_shop != null)
            ServiceLocator.Unregister<IShopService>(_shop);

        if(_db != null)
            ServiceLocator.Unregister<ISkinDatabase>(_db);

        if(_progress != null)
        {
            ServiceLocator.Unregister<IShopState>(_progress);
            ServiceLocator.Unregister<ILevelCompletionService>(_progress);
            ServiceLocator.Unregister<ICurrencyService>(_progress);
            ServiceLocator.Unregister<IProgressService>(_progress);
        }
    }
}
