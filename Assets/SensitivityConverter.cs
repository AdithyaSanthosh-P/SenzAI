using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class SensitivityConverter : MonoBehaviour
{
    [Header("Dropdowns")]
    [SerializeField] private TMP_Dropdown sourceGameDropdown;
    [SerializeField] private TMP_Dropdown targetGameDropdown;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField sensitivityInput;
    [SerializeField] private TMP_InputField dpiInput;

    [Header("UI")]
    [SerializeField] private Button calculateButton;
    [SerializeField] private TextMeshProUGUI resultText;

    private struct GameData
    {
        public float yaw;
        public GameData(float yaw)
        {
            this.yaw = yaw;
        }
    }

    private readonly Dictionary<string, GameData> gameDatabase = new()
    {
        { "Valorant",      new GameData(0.07f) },
        { "CS2",           new GameData(0.022f) },
        { "Apex Legends",  new GameData(0.022f) },
        { "Overwatch",     new GameData(0.0066f) }
    };

    private void Start()
    {
        PopulateDropdowns();
        if (calculateButton != null)
            calculateButton.onClick.AddListener(OnCalculateClicked);

        if (resultText != null)
            resultText.text = "";
    }

    private void PopulateDropdowns()
    {
        List<string> gameNames = new(gameDatabase.Keys);

        sourceGameDropdown.ClearOptions();
        targetGameDropdown.ClearOptions();

        sourceGameDropdown.AddOptions(gameNames);
        targetGameDropdown.AddOptions(gameNames);

        sourceGameDropdown.value = 0;
        targetGameDropdown.value = 1;

        sourceGameDropdown.RefreshShownValue();
        targetGameDropdown.RefreshShownValue();
    }

    private void OnCalculateClicked()
    {

        string sourceGame = sourceGameDropdown.options[sourceGameDropdown.value].text;
        string targetGame = targetGameDropdown.options[targetGameDropdown.value].text;
        if (!float.TryParse(sensitivityInput.text, out float sourceSensitivity))
        {
            resultText.text = "Enter a valid sensitivity.";
            return;
        }

        if (!float.TryParse(dpiInput.text, out float dpi))
        {
            resultText.text = "Enter a valid DPI.";
            return;
        }

        GameData sourceData = gameDatabase[sourceGame];
        GameData targetData = gameDatabase[targetGame];

        float convertedSensitivity = sourceSensitivity * (sourceData.yaw / targetData.yaw);
        float eDPI = sourceSensitivity * dpi;
        float convertedEDPI = convertedSensitivity * dpi;


        resultText.text =
            $"Source: {sourceGame}\n" +
            $"Sensitivity: {sourceSensitivity:F3}\n" +
            $"DPI: {dpi:F0}\n\n" +
            $"Target: {targetGame}\n" +
            $"Converted Sens: {convertedSensitivity:F3}\n" +
            $"eDPI: {eDPI:F0}\n" +
            $"Target eDPI: {convertedEDPI:F0}";
    }
}