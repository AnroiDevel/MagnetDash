using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class StarPickup : MonoBehaviour
{
    private bool _taken;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(_taken || !other.CompareTag("Player"))
            return;

        _taken = true;

        if(other.TryGetComponent<PlayerMagnet>(out var player))
            player.OnStarPickup(transform.position);

        gameObject.SetActive(false);
    }
}
