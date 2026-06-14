using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public static class CreateSensitivityUI
{
    [MenuItem("Tools/Create Sensitivity UI")]
    public static void CreateUI()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        CreateLabel(canvas.transform, "SourceGameLabel", "Source Game:", new Vector2(0, 180));
        CreateDropdown(canvas.transform, "SourceGameDropdown", new Vector2(0, 140));

        CreateLabel(canvas.transform, "SensitivityLabel", "Sensitivity:", new Vector2(0, 90));
        CreateInput(canvas.transform, "SensitivityInput", "0.8", new Vector2(0, 50));

        CreateLabel(canvas.transform, "DPILabel", "DPI:", new Vector2(0, 0));
        CreateInput(canvas.transform, "DPIInput", "800", new Vector2(0, -40));

        CreateLabel(canvas.transform, "TargetGameLabel", "Target Game:", new Vector2(0, -90));
        CreateDropdown(canvas.transform, "TargetGameDropdown", new Vector2(0, -130));

        CreateButton(canvas.transform, "CalculateButton", "Calculate", new Vector2(0, -190));
        CreateLabel(canvas.transform, "ResultText", "(Result will appear here)", new Vector2(0, -250));

        Selection.activeGameObject = canvas.gameObject;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 35);
        rect.anchoredPosition = pos;

        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 24;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.black;

        return label;
    }

    static TMP_InputField CreateInput(Transform parent, string name, string placeholder, Vector2 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 40);
        rect.anchoredPosition = pos;

        Image image = obj.AddComponent<Image>();
        image.color = Color.white;

        TMP_InputField input = obj.AddComponent<TMP_InputField>();

        TextMeshProUGUI text = CreateLabel(obj.transform, "Text", "", Vector2.zero);
        text.alignment = TextAlignmentOptions.Left;
        text.margin = new Vector4(10, 0, 10, 0);

        TextMeshProUGUI placeholderText = CreateLabel(obj.transform, "Placeholder", placeholder, Vector2.zero);
        placeholderText.color = Color.gray;
        placeholderText.alignment = TextAlignmentOptions.Left;
        placeholderText.margin = new Vector4(10, 0, 10, 0);

        input.textComponent = text;
        input.placeholder = placeholderText;

        return input;
    }

    static TMP_Dropdown CreateDropdown(Transform parent, string name, Vector2 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 40);
        rect.anchoredPosition = pos;

        Image image = obj.AddComponent<Image>();
        image.color = Color.white;

        TMP_Dropdown dropdown = obj.AddComponent<TMP_Dropdown>();
        dropdown.options.Add(new TMP_Dropdown.OptionData("Select Game"));

        return dropdown;
    }

    static Button CreateButton(Transform parent, string name, string text, Vector2 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 45);
        rect.anchoredPosition = pos;

        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.85f, 0.85f, 0.85f);

        Button button = obj.AddComponent<Button>();

        TextMeshProUGUI buttonText = CreateLabel(obj.transform, "Text", text, Vector2.zero);
        buttonText.color = Color.black;

        return button;
    }
}