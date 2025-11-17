using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class StarPickup : MonoBehaviour
{
    [SerializeField] private bool _destroyOnPick;
    private bool _taken;
    private LevelManager _level;

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if(c)
            c.isTrigger = true;
    }

    private void Awake()
    {
        _level = FindFirstObjectByType<LevelManager>(FindObjectsInactive.Exclude);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(_taken || !other.CompareTag("Player"))
            return;
        _taken = true;

        _level.CollectStar();
        if(_destroyOnPick)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}
