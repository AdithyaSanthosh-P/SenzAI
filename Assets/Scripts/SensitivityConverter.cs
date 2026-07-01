using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

// SensitivityConverter.cs
// Converts sensitivities between different games using their yaw values
// The formula is just: targetSens = sourceSens * (sourceYaw / targetYaw)
// which gives the same cm/360 in both games

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

    // yaw values for each game - this is the degrees turned per unit of sensitivity
    // found these by looking at mouse sensitivity spreadsheets online
    private struct GameData
    {
        public float yaw;
        public GameData(float yaw) { this.yaw = yaw; }
    }

    private readonly Dictionary<string, GameData> gameDatabase = new()
    {
        { "Valorant",      new GameData(0.07f)   },
        { "CS2",           new GameData(0.022f)  },
        { "Apex Legends",  new GameData(0.022f)  },
        { "Overwatch 2",   new GameData(0.0066f) }
    };

    // convert sensitivity between two games
    public static float Convert(float sourceSens, float sourceYaw, float targetYaw)
        => sourceSens * (sourceYaw / targetYaw);

    // calculate cm per 360 degrees of rotation
    public static float Cm360(float sens, float dpi, float yaw)
        => (2.54f * 360f) / (dpi * sens * yaw);

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

        if (sourceGame == targetGame)
        {
            resultText.text = "Source and target games are the same!\n";
            return;
        }

        if (!float.TryParse(sensitivityInput.text, out float sourceSens) || sourceSens <= 0f)
        {
            resultText.text = "Enter a valid sensitivity";
            return;
        }

        if (!float.TryParse(dpiInput.text, out float dpi) || dpi <= 0f)
        {
            resultText.text = "Enter a valid DPI";
            return;
        }

        GameData src = gameDatabase[sourceGame];
        GameData tgt = gameDatabase[targetGame];

        float convertedSens = Convert(sourceSens, src.yaw, tgt.yaw);
        float cm360 = Cm360(sourceSens, dpi, src.yaw);

        resultText.text =
            $"{sourceGame}\n" +
            $"Sensitivity : {sourceSens:F3}\n" +
            $"DPI         : {dpi:F0}\n\n" +
            $"{targetGame}\n" +
            $"Sensitivity : {convertedSens:F3}\n\n" +
            $"Feel\n" +
            $"cm / 360°   : {cm360:F1} cm";
    }
}