using UnityEngine;

public abstract class PlayerSkinView : MonoBehaviour, IPlayerSkin
{
    public virtual void OnSpeedChanged(float speed01) { }
    public virtual void OnPolarityChanged(int polarity) { }
    public virtual void OnStarCollected() { }
    public virtual void OnHit() { }
    public virtual void OnDeath() { }
}
