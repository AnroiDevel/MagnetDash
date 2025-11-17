using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class Spike : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!other.attachedRigidbody)
            return;
        if(!other.TryGetComponent<PlayerMagnet>(out _))
            return;

        if(ServiceLocator.TryGet<ILevelFlow>(out var flow))
        {
            flow.KillPlayer();
        }
        else
        {
            var lm = FindFirstObjectByType<LevelManager>(FindObjectsInactive.Exclude);
            lm.KillPlayer();
        }
    }
}
