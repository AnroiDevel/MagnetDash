using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class Portal : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform _spiral;      // ссылка на Spiral
    [SerializeField] private SpriteRenderer _ring;   // кольцо
    [SerializeField] private SpriteRenderer _halo;   // свечение
    [SerializeField] private AudioSource _nearLoop;  // тихий тон (loop)

    [Header("Spin")]
    [SerializeField] private float _spinSpeed = 180f;   // deg/sec

    [Header("Proximity FX")]
    [SerializeField] private float _nearRadiusMul = 1.6f;   // зона реакции по близости (в радиусах триггера)
    [SerializeField] private float _haloMinAlpha = 0.10f;
    [SerializeField] private float _haloMaxAlpha = 0.40f;
    [SerializeField] private float _ringMinMul = 0.85f;     // умножитель яркости/альфы кольца
    [SerializeField] private float _ringMaxMul = 1.15f;     // при близости
    [SerializeField] private PortalSfx _sfx;

    private CircleCollider2D _trigger;
    private Transform _player;
    private Color _ringBase;
    private Color _haloBase;
    private bool _won;
    private float _nearRadius;   // world-радиус реагирования

    private void Awake()
    {
        _trigger = GetComponent<CircleCollider2D>();
        _ringBase = _ring.color;
        _haloBase = _halo.color;
    }

    private void Start()
    {
        // Поиск игрока один раз (можешь заменить на ссылку из LevelManager)
        var pm = FindAnyObjectByType<PlayerMagnet>();
        _player = pm ? pm.transform : null;

        _nearRadius = _trigger.radius * _nearRadiusMul;
    }

    private void Update()
    {
        // Вращаем спираль
        if(_spiral)
            _spiral.Rotate(0f, 0f, -_spinSpeed * Time.deltaTime);

        if(_won)
            return;


        // Проксимити-эффекты
        if(_player)
        {
            float d = Vector2.Distance(_player.position, transform.position);
            float k = 1f - Mathf.Clamp01(d / _nearRadius); // 0 далеко → 1 рядом
            UpdateVisualsByProximity(k);
            UpdateNearAudio(k);
        }
    }

    private void UpdateVisualsByProximity(float k)
    {
        // Halo alpha
        var hc = _haloBase;
        hc.a = Mathf.Lerp(_haloMinAlpha, _haloMaxAlpha, k);
        _halo.color = hc;

        // Ring intensity (через альфу/яркость — без HDR)
        var rc = _ringBase;
        rc.a *= Mathf.Lerp(_ringMinMul, _ringMaxMul, k);
        _ring.color = rc;
    }

    private void UpdateNearAudio(float k)
    {
        if(!_nearLoop)
            return;

        // Порог включения/выключения
        if(k > 0.05f && !_nearLoop.isPlaying)
            _nearLoop.Play();
        if(k <= 0.01f && _nearLoop.isPlaying)
            _nearLoop.Stop();

        // Громкость по близости
        _nearLoop.volume = Mathf.Lerp(0.0f, 0.18f, k);
        // Можно ещё крутить pitch: _nearLoop.pitch = Mathf.Lerp(0.9f, 1.1f, k);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(_won)
            return;
        if(!other.TryGetComponent<PlayerMagnet>(out var player))
            return;

        // Дополнительная защита: реагируем только когда игра действительно в геймплее
        if(ServiceLocator.TryGet<LevelManager>(out var lm) &&
            lm.State != GameState.Playing)
        {
            return;
        }

        _won = true;

        if(_nearLoop && _nearLoop.isPlaying)
            _nearLoop.Stop();

        _sfx.OnAbsorb(transform.position);
        player.AbsorbIntoPortal(transform.position, 1.2f);
        StartCoroutine(WinFlash());
    }


    private IEnumerator WinFlash()
    {
        if(ServiceLocator.TryGet<IGameEvents>(out var ev))
            ev.FirePortalReached();

        float t = 0f;
        const float dur = 0.18f;
        while(t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - (t / dur);
            var hc = _halo.color;
            hc.a = Mathf.Lerp(_halo.color.a, _haloMaxAlpha + 0.25f, k);
            _halo.color = hc;
            yield return null;
        }
    }
}
