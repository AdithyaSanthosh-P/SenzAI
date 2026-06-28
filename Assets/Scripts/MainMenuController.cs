
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("The root panel containing the two main menu buttons.")]
    [SerializeField] private GameObject mainMenuPanel;

    [Tooltip("The sensitivity converter UI panel.")]
    [SerializeField] private GameObject converterPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private UnityEngine.UI.Button trainBtn;
    [SerializeField] private UnityEngine.UI.Button converterBtn;

    [Header("Converter Panel")]
    [SerializeField] private UnityEngine.UI.Button backBtn;

    [Header("Scene Settings")]
    [SerializeField] private string trainerSceneName = "SampleScene";

    private void Start()
    {
        if (trainBtn     != null) trainBtn    .onClick.AddListener(OnTrainClicked);
        if (converterBtn != null) converterBtn.onClick.AddListener(OnConverterClicked);
        if (backBtn      != null) backBtn     .onClick.AddListener(OnBackClicked);

        ShowMainMenu();
    }

    private void OnTrainClicked()
    {
        SceneManager.LoadScene(trainerSceneName);
    }

    private void OnConverterClicked()
    {
        ShowConverter();
    }

    private void OnBackClicked()
    {
        ShowMainMenu();
    }

    private void ShowMainMenu()
    {
        if (mainMenuPanel  != null) mainMenuPanel .SetActive(true);
        if (converterPanel != null) converterPanel.SetActive(false);
    }

    private void ShowConverter()
    {
        if (mainMenuPanel  != null) mainMenuPanel .SetActive(false);
        if (converterPanel != null) converterPanel.SetActive(true);
    }
}
