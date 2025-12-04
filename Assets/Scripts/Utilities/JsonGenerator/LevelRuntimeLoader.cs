using UnityEngine;

public sealed class LevelRuntimeLoader : MonoBehaviour, IRuntimeLevelsConfig
{
    [Header("Config")]
    [SerializeField] private TextAsset _levelsJson;
    [SerializeField] private int _levelIndex = 0; // индекс в списке levels для прямого запуска из редактора

    [Header("Prefabs")]
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private GameObject _portalPrefab;
    [SerializeField] private GameObject _positiveNodePrefab;
    [SerializeField] private GameObject _negativeNodePrefab;

    [Header("Collectible Stars")]
    [SerializeField] private GameObject _collectibleStarPrefab;

    [Header("Camera")]
    [SerializeField] private CameraFollow2D _cameraFollow;

    [Header("Parents")]
    [SerializeField] private Transform _levelRoot;

    private LevelJsonRoot _config;

    // ===== IRuntimeLevelsConfig =====
    public int LevelsCount => _config?.levels != null ? _config.levels.Count : 0;

    private void Awake()
    {
        if(_levelsJson == null)
        {
            Debug.LogError("[LevelRuntimeLoader] _levelsJson is null. Assign levels.json in inspector.");
            return;
        }

        _config = JsonUtility.FromJson<LevelJsonRoot>(_levelsJson.text);
        if(_config == null || _config.levels == null || _config.levels.Count == 0)
        {
            Debug.LogError("[LevelRuntimeLoader] Failed to parse levels.json or no levels found.");
            return;
        }

        // Регистрируем себя как источник информации о количестве runtime уровней
        ServiceLocator.Register<IRuntimeLevelsConfig>(this);

        // Какой индекс уровня реально грузить
        int indexToLoad = Mathf.Clamp(_levelIndex, 0, _config.levels.Count - 1);

        if(ServiceLocator.TryGet<LevelManager>(out var lm))
        {
            // 1) Если LevelManager уже знает активный jsonIndex (зашли через меню) — используем его
            if(lm.TryGetCurrentRuntimeJsonIndex(out var idx) &&
               idx >= 0 &&
               idx < _config.levels.Count)
            {
                indexToLoad = idx;
            }
            else
            {
                // 2) Иначе (запустили сцену напрямую) —
                //    считаем, что это runtime-уровень с индексом _levelIndex
                lm.RegisterRuntimeJsonIndex(indexToLoad);
            }
        }

        LoadLevel(indexToLoad);
    }

    private void OnDestroy()
    {
        // Снимаем регистрацию, чтобы не висеть в сервис-локаторе
        ServiceLocator.Unregister<IRuntimeLevelsConfig>(this);
    }

    public void LoadLevel(int index)
    {
        ClearLevel();

        if(_config == null || _config.levels == null)
        {
            Debug.LogError("[LevelRuntimeLoader] Config is null, cannot load level.");
            return;
        }

        if(index < 0 || index >= _config.levels.Count)
        {
            Debug.LogError($"[LevelRuntimeLoader] invalid level index {index}");
            return;
        }

        LevelJsonData data = _config.levels[index];

        // --- Player ---
        Vector2 pPos = data.player.ToVector2();
        GameObject player = Instantiate(_playerPrefab, pPos, Quaternion.identity, _levelRoot);

        var playerMagnet = player.GetComponent<PlayerMagnet>();

        if(_cameraFollow != null)
            _cameraFollow.SetTarget(player.transform);

        // --- Portal ---
        Vector2 portalPos = data.portal.ToVector2();
        GameObject portal = Instantiate(_portalPrefab, portalPos, Quaternion.identity, _levelRoot);

        if(playerMagnet != null)
            playerMagnet.SetPortalTarget(portal.transform);

        // --- Nodes ---
        MagneticNode spawnNode = null;

        for(int i = 0; i < data.nodes.Count; i++)
        {
            NodeJsonData n = data.nodes[i];
            Vector2 pos = n.position.ToVector2();

            GameObject prefab = n.isPositive ? _positiveNodePrefab : _negativeNodePrefab;
            var nodeGo = Instantiate(prefab, pos, Quaternion.identity, _levelRoot);

            var magneticNode = nodeGo.GetComponent<MagneticNode>();
            if(magneticNode != null && spawnNode == null && n.isPositive)
                spawnNode = magneticNode;
        }

        // Передаём spawn-ноду игроку (один раз, вне цикла)
        if(playerMagnet != null && spawnNode != null)
            playerMagnet.RegisterSpawnNode(spawnNode);

        // --- Stars ---
        if(_collectibleStarPrefab != null && data.starsToCollect != null)
        {
            for(int i = 0; i < data.starsToCollect.Count; i++)
            {
                Vector2 pos = data.starsToCollect[i].ToVector2();
                Instantiate(_collectibleStarPrefab, pos, Quaternion.identity, _levelRoot);
            }
        }
    }

    private void ClearLevel()
    {
        if(_levelRoot == null)
            return;

        for(int i = _levelRoot.childCount - 1; i >= 0; i--)
            Destroy(_levelRoot.GetChild(i).gameObject);
    }
}
