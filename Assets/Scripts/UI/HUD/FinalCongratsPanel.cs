using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FinalCongratsPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup _cg;
    [SerializeField] private TMP_Text _tTitle;
    [SerializeField] private TMP_Text _tBody;

    [Header("Reward")]
    [SerializeField] private Image _rewardImage;      // красивая медаль/значок
    [SerializeField] private TMP_Text _tReward;       // строка с званием

    [Header("Buttons")]
    [SerializeField] private Button _btnMenu;

    [Header("FX")]
    [SerializeField] private float _fadeDuration = 0.25f;
    [SerializeField] private float _rewardPopScale = 1.15f;

    [SerializeField] private MusicTrack _musicTrack;

     private MusicManager  _musicManager;

    private ILevelFlow _flow;
    private IAudioService _audio; // опционально, если хочешь звук
    private Coroutine _fadeRoutine;

    private const string _kChapter1Completed = "chapter1.completed";

    private void Awake()
    {
        _musicManager = FindAnyObjectByType<MusicManager>();

        if(_cg == null)
            _cg = GetComponentInChildren<CanvasGroup>(true);

        _cg.alpha = 0f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;

        if(_tTitle != null)
            _tTitle.SetText("Глава 1 пройдена");

        if(_tBody != null)
            _tBody.SetText(
                "Получено новое достижение"
            );

        if(_tReward != null)
            _tReward.SetText("Звание: Магнитный Пионер");
    }

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<ILevelFlow>(f => _flow = f);
        ServiceLocator.WhenAvailable<IAudioService>(a => _audio = a);

        if(_btnMenu != null)
            _btnMenu.onClick.AddListener(OnMenuClicked);

        // Флажок «глава пройдена»
        PlayerPrefs.SetInt(_kChapter1Completed, 1);
        PlayerPrefs.Save();

        if(_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeInAndReward());
    }

    private void OnDisable()
    {
        if(_btnMenu != null)
            _btnMenu.onClick.RemoveListener(OnMenuClicked);

        if(_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }

    private void OnMenuClicked()
    {
        if(_flow != null)
            _flow.LoadMenu();
    }

    private IEnumerator FadeInAndReward()
    {
        // плавно показываем всю панель
        _cg.blocksRaycasts = true;
        _cg.interactable = true;

        float t = 0f;
        while(t < _fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.SmoothStep(0f, 1f, t / _fadeDuration);
            yield return null;
        }
        _cg.alpha = 1f;

        // звук награды
        _musicManager.Play(_musicTrack); 

        // небольшой «поп» у иконки награды
        if(_rewardImage != null)
        {
            Transform tr = _rewardImage.transform;
            Vector3 baseScale = tr.localScale;
            float popT = 0f;
            const float popDur = 0.18f;

            while(popT < popDur)
            {
                popT += Time.unscaledDeltaTime;
                float k = popT / popDur;
                float s = Mathf.Lerp(1f, _rewardPopScale, Mathf.SmoothStep(0f, 1f, k));
                tr.localScale = baseScale * s;
                yield return null;
            }

            tr.localScale = baseScale;
        }

        _fadeRoutine = null;
    }
}
