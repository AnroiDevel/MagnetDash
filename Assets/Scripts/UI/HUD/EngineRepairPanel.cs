using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class EngineRepairPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _durabilityText;
    [SerializeField] private Button _repairButton;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Slider _powerSlider;

    private ILevelFlow _levelFlow;

    private void Awake()
    {
        _repairButton.onClick.AddListener(OnRepairClicked);
        _continueButton.onClick.AddListener(OnContinueClicked);

        ServiceLocator.WhenAvailable<ILevelFlow>(flow => _levelFlow = flow);

        gameObject.SetActive(false);
    }

    public void Show(int power)
    {
        if(_durabilityText != null)
            _durabilityText.text = $"{power}%";

        _powerSlider.value = power;
        gameObject.SetActive(true);
    }

    private void OnRepairClicked()
    {
        if(!ServiceLocator.TryGet<IAdService>(out var ad) || !ad.IsAvailable)
        {
            CloseAndResume();
            return;
        }

        ad.ShowRewarded(
            onSuccess: () =>
            {
                if(ServiceLocator.TryGet<IProgressService>(out var progress))
                    progress.RepairEngineFull();

                CloseAndResume();
            },
            onFail: CloseAndResume);
    }

    private void OnContinueClicked()
    {
        CloseAndResume();
    }

    private void CloseAndResume()
    {
        gameObject.SetActive(false);
        _levelFlow?.Resume();
    }
}
