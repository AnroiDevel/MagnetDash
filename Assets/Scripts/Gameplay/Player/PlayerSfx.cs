using UnityEngine;

public sealed class PlayerSfx : MonoBehaviour
{
    [SerializeField] private SfxEvent _switchPolarity;
    [SerializeField] private SfxEvent _magnetImpulse;
    [SerializeField] private SfxEvent _hitLight;
    [SerializeField] private SfxEvent _hitHeavy;
    [SerializeField] private SfxEvent _slideLoop;

    private IAudioHandle _slideHandle; // null до старта лупа

    public void OnSwitchPolarity()
    {
        Audio.Play(_switchPolarity, key: "switch"); // cooldown в SfxEvent
    }

    public void OnMagnetImpulse()
    {
        Audio.Play(_magnetImpulse, key: "impulse");
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        float impact = col.relativeVelocity.magnitude;
        var hitPoint = (Vector3)col.GetContact(0).point;

        //if(impact < 2.5f)
        //    Audio.PlayAt(_hitLight, hitPoint, spatial: 1f, key: "hit");
        //else
            Audio.PlayAt(_hitHeavy, hitPoint, spatial: 1f, key: "hit");
    }

    public void OnSlideStart(Vector3 worldPos)
    {
        if(_slideHandle == null || !_slideHandle.IsValid)
        {
            _slideHandle = Audio.PlayLoop(_slideLoop, worldPos, spatial: 0.3f);
            if(_slideHandle != null && _slideHandle.IsValid)
                _slideHandle.SetVolume(0.18f, 0.08f);
        }
    }

    public void OnSlideUpdate(Vector3 worldPos, float intensity01)
    {
        if(_slideHandle != null && _slideHandle.IsValid)
        {
            float v = Mathf.Lerp(0.08f, 0.22f, Mathf.Clamp01(intensity01));
            _slideHandle.SetVolume(v, 0.05f);
        }
    }

    public void OnSlideStop()
    {
        if(_slideHandle != null && _slideHandle.IsValid)
            _slideHandle.Stop(0.12f);
        _slideHandle = null;
    }

    private void OnDisable()
    {
        if(_slideHandle != null && _slideHandle.IsValid)
            _slideHandle.Stop(0.05f);
        _slideHandle = null;
    }
}
