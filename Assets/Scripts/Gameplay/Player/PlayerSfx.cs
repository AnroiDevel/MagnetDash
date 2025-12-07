using UnityEngine;

public sealed class PlayerSfx : MonoBehaviour
{
    [Header("One-shot SFX")]
    [SerializeField] private SfxEvent _switchPolarity;
    [SerializeField] private SfxEvent _magnetImpulse;
    [SerializeField] private SfxEvent _hitLight;
    [SerializeField] private SfxEvent _hitHeavy;
    [SerializeField] private SfxEvent _starCollect;

    [Header("Slide Loop")]
    [SerializeField] private SfxEvent _slideLoop;

    [Header("Engine Loops")]
    [SerializeField] private SfxEvent _engineLowLoop;     // низкий гул
    [SerializeField] private SfxEvent _engineHighLoop;    // верхняя «турбина»

    [SerializeField] private float _engineLowMinVolume = 0.03f;
    [SerializeField] private float _engineLowMaxVolume = 0.20f;
    [SerializeField] private float _engineHighMaxVolume = 0.25f;

    [SerializeField] private float _engineVolumeChangeSpeed = 4f;

    private IAudioHandle _slideHandle;

    private IAudioHandle _engineLowHandle;
    private IAudioHandle _engineHighHandle;

    private float _engineLowTarget;
    private float _engineHighTarget;
    private float _engineLowCurrent;
    private float _engineHighCurrent;

    private void Update()
    {
        UpdateEngineLoops();
    }

    // ===== One-shot =====

    public void OnSwitchPolarity()
    {
        Audio.Play(_switchPolarity, key: "switch");
    }

    public void OnMagnetImpulse()
    {
        Audio.Play(_magnetImpulse, key: "impulse");
    }

    public void OnStarCollected()
    {
        Audio.Play(_starCollect, key :"starCollect");
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        float impact = col.relativeVelocity.magnitude;
        var hitPoint = (Vector3)col.GetContact(0).point;

        Audio.PlayAt(_hitHeavy, hitPoint, spatial: 1f, key: "hit");
    }

    // ===== Slide =====

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

    // ===== ENGINE =====

    /// <summary>
    /// speed01 = 0..1 от фактической скорости (PlayerMagnet уже нормализует).
    /// </summary>
    public void UpdateEngine(float speed01, Vector3 worldPos)
    {
        speed01 = Mathf.Clamp01(speed01);

        // Кривая для НИЗКОГО слоя — включается почти сразу
        float lowK = Mathf.SmoothStep(0f, 1f, speed01 * 1.2f);
        _engineLowTarget = Mathf.Lerp(_engineLowMinVolume, _engineLowMaxVolume, lowK);

        // Кривая для ВЕРХНЕГО слоя — появляется после ~0.3 скорости
        float highStart = 0.3f;
        float highNorm = Mathf.InverseLerp(highStart, 1f, speed01);
        float highK = Mathf.SmoothStep(0f, 1f, highNorm);
        _engineHighTarget = _engineHighMaxVolume * highK;

        // Лупы лениво создаём по мере необходимости
        if(_engineLowTarget > 0.001f && (_engineLowHandle == null || !_engineLowHandle.IsValid))
        {
            _engineLowHandle = Audio.PlayLoop(_engineLowLoop, worldPos, spatial: 0f);
            _engineLowCurrent = 0f;
            _engineLowHandle?.SetVolume(0f, 0f);
        }

        if(_engineHighTarget > 0.001f && (_engineHighHandle == null || !_engineHighHandle.IsValid))
        {
            _engineHighHandle = Audio.PlayLoop(_engineHighLoop, worldPos, spatial: 0f);
            _engineHighCurrent = 0f;
            _engineHighHandle?.SetVolume(0f, 0f);
        }
    }

    /// <summary>Полное заглушение двигателя (портал / смерть / выключение объекта).</summary>
    public void StopEngine()
    {
        _engineLowTarget = 0f;
        _engineHighTarget = 0f;
        _engineLowCurrent = 0f;
        _engineHighCurrent = 0f;

        if(_engineLowHandle != null && _engineLowHandle.IsValid)
            _engineLowHandle.Stop(0.1f);
        if(_engineHighHandle != null && _engineHighHandle.IsValid)
            _engineHighHandle.Stop(0.1f);

        _engineLowHandle = null;
        _engineHighHandle = null;
    }

    private void UpdateEngineLoops()
    {
        float dt = Time.deltaTime;

        // LOW
        if(_engineLowHandle != null && _engineLowHandle.IsValid)
        {
            _engineLowCurrent = Mathf.MoveTowards(
                _engineLowCurrent,
                _engineLowTarget,
                _engineVolumeChangeSpeed * dt);

            _engineLowHandle.SetVolume(_engineLowCurrent, 0.05f);

            if(_engineLowCurrent <= 0.001f && _engineLowTarget <= 0.001f)
            {
                _engineLowHandle.Stop(0.1f);
                _engineLowHandle = null;
            }
        }

        // HIGH
        if(_engineHighHandle != null && _engineHighHandle.IsValid)
        {
            _engineHighCurrent = Mathf.MoveTowards(
                _engineHighCurrent,
                _engineHighTarget,
                _engineVolumeChangeSpeed * dt);

            _engineHighHandle.SetVolume(_engineHighCurrent, 0.05f);

            if(_engineHighCurrent <= 0.001f && _engineHighTarget <= 0.001f)
            {
                _engineHighHandle.Stop(0.1f);
                _engineHighHandle = null;
            }
        }
    }

    private void OnDisable()
    {
        if(_slideHandle != null && _slideHandle.IsValid)
            _slideHandle.Stop(0.05f);
        _slideHandle = null;

        StopEngine();
    }
}
