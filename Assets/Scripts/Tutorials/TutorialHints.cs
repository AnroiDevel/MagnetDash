using System.Collections;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public sealed class TutorialHints : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup _cg;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _fade = 0.18f;
    private readonly float _autoHide = 3.0f;

    private ISettingsService _settings;
    private IGameEvents _events;

    private Coroutine _flow;
    private bool _waitPolarity;
    private bool _waitPortal;

    private void Awake()
    {
        if(_cg == null)
            _cg = GetComponentInChildren<CanvasGroup>(true);

        HideImmediate();
    }

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<ISettingsService>(s =>
        {
            _settings = s;
            _settings.HintsChanged += OnHintsChanged;

            if(_settings.HintsEnabled)
                TryStartFlow();
            else
                HideImmediate();
        });

        ServiceLocator.WhenAvailable<IGameEvents>(e =>
        {
            if(!isActiveAndEnabled)
                return;

            _events = e;
            _events.PolarityChanged += OnPolarityChanged;
            _events.PortalReached += OnPortalReached;

            if(_settings != null && _settings.HintsEnabled)
                TryStartFlow();
        });
    }

    private void OnDisable()
    {
        if(_events != null)
        {
            _events.PolarityChanged -= OnPolarityChanged;
            _events.PortalReached -= OnPortalReached;
            _events = null;
        }

        if(_settings != null)
        {
            _settings.HintsChanged -= OnHintsChanged;
            _settings = null;
        }

        if(_flow != null)
        {
            StopCoroutine(_flow);
            _flow = null;
        }

        HideImmediate();
    }

    private void OnHintsChanged(bool on)
    {
        if(on)
        {
            TryStartFlow();
        }
        else
        {
            if(_flow != null)
            {
                StopCoroutine(_flow);
                _flow = null;
            }
            HideImmediate();
        }
    }

    private void TryStartFlow()
    {
        if(_flow != null)
            return;
        if(_events == null)
            return;
        if(!isActiveAndEnabled)
            return;

        _flow = StartCoroutine(CoRun());
    }

    private IEnumerator CoRun()
    {
        // 1. Полярность
        yield return Show("Нажми, чтобы сменить полярность (плюс/минус)");
        _waitPolarity = true;
        yield return new WaitUntil(() => !_waitPolarity);
        yield return Hide();

        // 2. Подсказка про настройки
        yield return Show("Подсказки можно отключить в «Настройках»");
        yield return new WaitForSecondsRealtime(_autoHide);
        yield return Hide();

        // 3. Объяснение нод
        yield return Show("Одинаковые заряды отталкивают");
        yield return new WaitForSecondsRealtime(_autoHide);
        yield return Hide();

        yield return Show("Разные заряды притягивают");
        yield return new WaitForSecondsRealtime(_autoHide);
        yield return Hide();

        // 4. Цель
        yield return Show("Доберись до портала");
        _waitPortal = true;
        yield return new WaitUntil(() => !_waitPortal);
        yield return Hide();

        _flow = null;
    }

    private void OnPolarityChanged(int _)
    {
        _waitPolarity = false;
    }

    private void OnPortalReached()
    {
        _waitPortal = false;
    }

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

    private void HideImmediate()
    {
        _cg.alpha = 0f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;
    }
}
