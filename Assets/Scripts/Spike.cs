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
            Debug.LogError("[Spike] ILevelFlow service not found. " +
                           "Ensure LevelManager is present in the Systems scene and registered.");
        }
    }
}
