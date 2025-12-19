using UnityEngine;

public sealed class NeonThrusterSkin : PlayerSkinView
{
    [SerializeField] private SpriteRenderer _body;
    [SerializeField] private SpriteRenderer _core;
    [SerializeField] private ParticleSystem _thruster;

    private Material _bodyMat;
    private Material _coreMat;

    private float _glowIntensity;

    private void Awake()
    {
        if(_body != null)
            _bodyMat = _body.material;

        if(_core != null)
            _coreMat = _core.material;
    }

    public override void OnSpeedChanged(float s)
    {
        s = Mathf.Clamp01(s);

        // 1 Ч свечение тела
        if(_bodyMat != null)
            _bodyMat.SetFloat("_Glow", Mathf.Lerp(0.1f, 1.5f, s));

        // 2 Ч пульсаци€ €дра
        if(_coreMat != null)
            _coreMat.SetFloat("_Intensity", Mathf.Lerp(0.5f, 2f, s));

        // 3 Ч ускорение частиц
        if(_thruster != null)
        {
            var main = _thruster.main;
            main.startSpeed = Mathf.Lerp(1f, 8f, s);
        }
    }

    public override void OnHit()
    {
        // краткий всплеск
        if(_coreMat != null)
            _coreMat.SetFloat("_Intensity", 3f);
    }
}
