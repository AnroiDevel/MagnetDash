using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class StarPickup : MonoBehaviour
{
    private bool _taken;

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if(c)
            c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(_taken || !other.CompareTag("Player"))
            return;

        _taken = true;

        if(ServiceLocator.TryGet<LevelManager>(out var levelManager))
        {
            levelManager.CollectStar();
        }
        else
        {
            Debug.LogError("[StarPickup] LevelManager service not found. " +
                           "Ensure LevelManager is present in the Systems scene and registered.");
        }

        gameObject.SetActive(false);
    }
}
