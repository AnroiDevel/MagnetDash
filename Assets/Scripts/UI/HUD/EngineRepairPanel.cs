using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class EngineRepairPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _power;
    [SerializeField] private Button _repairButton;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Slider _powerSlider;

    private ILevelFlow _levelFlow;

    private bool _waitingAd;

    private void Awake()
    {
        _repairButton.onClick.AddListener(OnRepairClicked);
        _continueButton.onClick.AddListener(OnContinueClicked);

        ServiceLocator.WhenAvailable<ILevelFlow>(flow => _levelFlow = flow);

        gameObject.SetActive(false);
    }

    public void Show(int power)
    {
        _waitingAd = false;

        if(_power != null)
            _power.text = $"{power}%";

        if(_powerSlider != null)
            _powerSlider.value = power;

        // Пауза именно при показе панели (это “модалка”)
        _levelFlow?.Pause();

        SetButtons(waitingAd: false);

        gameObject.SetActive(true);

        // Прогрев рекламы (если это VkAdService)
        if(ServiceLocator.TryGet<IAdService>(out var ad) && ad is VkAdService vkAd)
            vkAd.PreloadRewarded();
    }

    private void OnRepairClicked()
    {
        if(_waitingAd)
            return;

        ServiceLocator.TryGet<IProgressService>(out var progress);
        ServiceLocator.TryGet<IAdService>(out var ad);

        void RepairNow()
        {
            progress?.RepairEngineFull();
            CloseAndResume();
        }

        // Если рекламы нет — чиним сразу
        if(ad == null || !ad.IsAvailable)
        {
            RepairNow();
            return;
        }

        _waitingAd = true;
        SetButtons(waitingAd: true); // Continue выключаем

        ad.ShowRewarded(
            onSuccess: RepairNow,
            onFail: () =>
            {
                // Рекламу не дали — просто закрываем и продолжаем
                CloseAndResume();
            }
        );
    }

    private void OnContinueClicked()
    {
        if(_waitingAd)
            return; // важно: пока ждём рекламу, продолжать нельзя

        CloseAndResume();
    }

    private void SetButtons(bool waitingAd)
    {
        if(_continueButton != null)
            _continueButton.interactable = !waitingAd;

        if(_repairButton != null)
            _repairButton.interactable = !waitingAd;
    }

    private void CloseAndResume()
    {
        _waitingAd = false;
        gameObject.SetActive(false);
        _levelFlow?.Resume();
    }
}
