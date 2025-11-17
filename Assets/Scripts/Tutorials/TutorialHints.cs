using System.Collections;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public sealed class TutorialHints : MonoBehaviour
{
    [SerializeField] private CanvasGroup _cg;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _fade = 0.18f;
    [SerializeField] private float _autoHide = 2.0f;

    private ISettingsService _settings;
    private IGameEvents _events;
    private Coroutine _flow;
    private bool _waitPolarity, _waitPortal;

    private void Awake()
    {
        if(_cg == null)
            _cg = GetComponentInChildren<CanvasGroup>(true);
        _cg.alpha = 0f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<ISettingsService>(s =>
        {
            _settings = s;
            if(!_settings.HintsEnabled)
            { gameObject.SetActive(false); return; }
            _settings.HintsChanged += OnHintsChanged;

            ServiceLocator.WhenAvailable<IGameEvents>(e =>
            {
                _events = e;
                _events.PolaritySwitched += OnPolarity;
                _events.PortalReached += OnPortal;
                _flow = StartCoroutine(CoRun());
            });
        });
    }

    private void OnDisable()
    {
        if(_events != null)
        { _events.PolaritySwitched -= OnPolarity; _events.PortalReached -= OnPortal; }
        if(_settings != null)
            _settings.HintsChanged -= OnHintsChanged;
        if(_flow != null)
        { StopCoroutine(_flow); _flow = null; }
        _cg.alpha = 0f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;
    }

    private void OnHintsChanged(bool on)
    {
        if(!on)
            StartCoroutine(Hide());
    }

    private IEnumerator CoRun()
    {
        yield return Show("Нажми, чтобы сменить полярность (плюс/минус)");
        _waitPolarity = true;
        yield return new WaitUntil(() => !_waitPolarity);
        yield return Hide();

        yield return Show("Подсказки можно отключить в «Настройках»");
        yield return new WaitForSecondsRealtime(_autoHide);
        yield return Hide();

        yield return Show("Голубые ноды притягивают, розовые — отталкивают");
        yield return new WaitForSecondsRealtime(_autoHide);
        yield return Hide();

        yield return Show("Доберись до портала");
        _waitPortal = true;
        yield return new WaitUntil(() => !_waitPortal);
        yield return Hide();
    }

    private void OnPolarity() { _waitPolarity = false; }
    private void OnPortal() { _waitPortal = false; }

    private IEnumerator Show(string msg)
    {
        _text.SetText(msg);
        _cg.blocksRaycasts = true;
        _cg.interactable = true;
        float t = 0f;
        while(t < _fade)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.SmoothStep(0f, 1f, t / _fade);
            yield return null;
        }
        _cg.alpha = 1f;
    }

    private IEnumerator Hide()
    {
        float t = 0f;
        while(t < _fade)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.SmoothStep(1f, 0f, t / _fade);
            yield return null;
        }
        _cg.alpha = 0f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;
    }
}
