using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.IO;

public class SensitivityUI : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject sensPanel;

    [Header("Text Fields")]
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text confidenceText;
    [SerializeField] private TMP_Text sessionCountText;
    [SerializeField] private TMP_Text currentSensText;   // always-on live sensitivity display

    [Header("Buttons")]
    [SerializeField] private UnityEngine.UI.Button analyzeBtn;
    [SerializeField] private UnityEngine.UI.Button applyBtn;
    [SerializeField] private UnityEngine.UI.Button closeBtn;
    [SerializeField] private UnityEngine.UI.Button resetBtn;
    [Tooltip("Deletes all session files from the save folder. Does NOT touch the optimizer model.")]
    [SerializeField] private UnityEngine.UI.Button clearSessionsBtn;

    [Header("Sensitivity Slider (optional)")]
    [SerializeField] private UnityEngine.UI.Slider sensSlider;
    [SerializeField] private TMP_Text sliderValueText;

    [Header("Game Conversion Output (optional)")]
    [Tooltip("Text element that shows the recommended sensitivity converted to each real game.")]
    [SerializeField] private TMP_Text gameConversionsText;

    [Header("Cross-Game Conversion Scale")]
    [Tooltip("Correction factor for Windows display scaling.\n" +
             "Unity Input System reports mouse delta in logical pixels, not raw HID counts.\n" +
             "Raw-input games (Valorant, CS2) bypass Windows scaling entirely.\n\n" +
             "Set this to match your Windows Display Scaling setting:\n" +
             "  100% scaling  →  1.0\n" +
             "  125% scaling  →  0.8\n" +
             "  150% scaling  →  0.67\n" +
             "  200% scaling  →  0.5  (default, common on 1440p/4K)")]
    [SerializeField] [Range(0.1f, 2.0f)] private float calibrationScale = 0.5f;

    // Game yaw constants — same source as SensitivityConverter
    private static readonly Dictionary<string, float> GameYaws = new()
    {
        { "Valorant",    0.07f   },
        { "CS2",         0.022f  },
        { "Apex Legends",0.022f  },
        { "Overwatch 2", 0.0066f }
    };

    public static bool PanelOpen { get; private set; }

    // ── Private state ─────────────────────────────────────────────────────────
    private MouseLook mouseLook;
    private float pendingRecommendation = -1f;
    private bool panelOpen = false;
    private float savedSensitivity = -1f;
    private bool appliedThisOpen = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        mouseLook = FindObjectOfType<MouseLook>();

        if (sensPanel != null)
        {
            sensPanel.SetActive(true);  // keep active so this script can run
            CanvasGroup cg = sensPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = sensPanel.AddComponent<CanvasGroup>();
            cg.alpha          = 0f;
            cg.interactable   = false;
            cg.blocksRaycasts = false;
        }

        if (analyzeBtn       != null) analyzeBtn      .onClick.AddListener(OnAnalyzeClicked);
        if (applyBtn         != null) applyBtn        .onClick.AddListener(OnApplyClicked);
        if (closeBtn         != null) closeBtn        .onClick.AddListener(OnCloseClicked);
        if (resetBtn         != null) resetBtn        .onClick.AddListener(OnResetClicked);
        if (clearSessionsBtn != null) clearSessionsBtn.onClick.AddListener(OnClearSessionsClicked);

        if (sensSlider != null)
        {
            sensSlider.minValue = 0.01f;
            sensSlider.maxValue = 0.5f;
            sensSlider.value    = mouseLook != null ? mouseLook.CurrentSensitivity : 0.15f;
            sensSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        if (applyBtn != null) applyBtn.interactable = false;
    }

    private void Update()
    {
        // Toggle panel on ESC
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (!panelOpen) OpenPanel();
            else            ClosePanel();
        }

        // Always show live sensitivity (visible during gameplay, not just in panel)
        if (mouseLook != null && currentSensText != null)
            currentSensText.text = $"Sensitivity: {mouseLook.CurrentSensitivity:F3}";
    }

    // ── Panel control ─────────────────────────────────────────────────────────

    private void OpenPanel()
    {
        panelOpen       = true;
        PanelOpen       = true;
        appliedThisOpen = false;
        savedSensitivity = mouseLook != null ? mouseLook.CurrentSensitivity : -1f;

        // Guarantee cursor is free — do not rely on MouseLook's ESC handler
        // which may execute in a different frame order.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        mouseLook?.ForceUnlockCursor();  // sync MouseLook's internal cursorLocked state

        if (sensPanel != null)
        {
            sensPanel.SetActive(true);   // force-active in case SensitivityCalibrator deactivated it
            CanvasGroup cg = sensPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = sensPanel.AddComponent<CanvasGroup>();
            cg.alpha          = 1f;
            cg.interactable   = true;
            cg.blocksRaycasts = true;
        }

        RefreshSlider();

        if (resultText          != null) resultText.text          = "Press 'Analyze' to get your recommendation.";
        if (confidenceText      != null) confidenceText.text      = "";
        if (sessionCountText    != null) sessionCountText.text    = "";
        if (gameConversionsText != null) gameConversionsText.text = "";
        if (applyBtn            != null) applyBtn.interactable    = false;
    }

    private void ClosePanel()
    {
        panelOpen = false;
        PanelOpen = false;

        if (sensPanel != null)
        {
            CanvasGroup cg = sensPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha          = 0f;
                cg.interactable   = false;
                cg.blocksRaycasts = false;
            }
        }

        // Restore sensitivity if user never hit Apply
        if (!appliedThisOpen && savedSensitivity > 0f && mouseLook != null)
            mouseLook.SetSensitivity(savedSensitivity);
    }

    private void RefreshSlider()
    {
        if (sensSlider != null && mouseLook != null)
            sensSlider.SetValueWithoutNotify(
                Mathf.Clamp(mouseLook.CurrentSensitivity, sensSlider.minValue, sensSlider.maxValue));
    }

    // ── Button callbacks ──────────────────────────────────────────────────────

    private void OnAnalyzeClicked()
    {
        if (SensitivityOptimizer.Instance == null)
        {
            if (resultText != null)
                resultText.text = "SensitivityOptimizer not found in scene.";
            return;
        }

        SensitivityResult res = SensitivityOptimizer.Instance.GetRecommendation();

        if (sessionCountText != null)
            sessionCountText.text = $"Bins: {res.binsExplored}/{res.totalBins}  |  Shots: {res.shotsSampled}  |  {res.phase}";

        if (!res.hasData)
        {
            if (resultText   != null) resultText.text   = res.reason;
            if (confidenceText!= null) confidenceText.text = "";
            if (applyBtn     != null) applyBtn.interactable = false;
            pendingRecommendation = -1f;
            return;
        }

        pendingRecommendation = res.recommendedSensitivity;

        if (resultText != null) resultText.text = res.reason;
        if (confidenceText != null)
        {
            int pct = Mathf.RoundToInt(res.confidence * 100f);
            confidenceText.text = $"Confidence: {pct}%  |  CI: [{res.ciLow:F3} \u2013 {res.ciHigh:F3}]  |  Conv: {res.convergenceProgress * 100:F0}%";
        }

        BuildGameConversions(res.recommendedSensitivity);

        if (applyBtn != null) applyBtn.interactable = true;
    }

    private void OnApplyClicked()
    {
        if (pendingRecommendation < 0f || mouseLook == null) return;

        mouseLook.SetSensitivity(pendingRecommendation);
        savedSensitivity = pendingRecommendation;  // prevent ClosePanel from undoing it
        appliedThisOpen  = true;
        RefreshSlider();

        if (resultText != null)
            resultText.text += $"\n\u2713 Applied! Sensitivity set to {pendingRecommendation:F2}";

        if (applyBtn != null) applyBtn.interactable = false;
    }

    // ── Game conversion breakdown ─────────────────────────────────────────────

    /// <summary>
    /// Converts the recommended SenzAI sensitivity to every supported game using cm/360.
    ///
    /// WHY cm/360:
    ///   SenzAI's sensitivity is in "degrees per screen-pixel" (Unity Input System delta).
    ///   Games like Valorant/CS2 use Raw Input (degrees per HID count).
    ///   These two scales are NOT directly comparable, so we cannot just ratio yaw values.
    ///   cm/360 is the physical distance your hand moves for a full rotation — DPI-independent
    ///   within a single input system, so it is the correct cross-game bridge.
    ///
    /// Formula:
    ///   cm360 = (2.54 * 360) / (DPI * senzaiSens)
    ///          -- MouseLook: rotation = delta_pixels * sensitivity  →  1 deg/pixel at sens=1
    ///          -- so effective degrees/inch = DPI * senzaiSens
    ///
    ///   targetSens = (2.54 * 360) / (DPI * targetYaw * cm360)
    ///              = senzaiSens / targetYaw          (DPI cancels here)
    ///
    ///   BUT DPI only cancels when both games share the same input pipeline.
    ///   Since they don't, we keep DPI explicit so future calibration can adjust it.
    /// </summary>
    private void BuildGameConversions(float senzaiSens)
    {
        if (gameConversionsText == null) return;

        // calibrationScale corrects for the mismatch between Unity's logical-pixel mouse delta
        // and raw-input games (Valorant, CS2, Apex) that bypass Windows display scaling.
        // At 200% Windows scaling: Unity reads half the physical counts → scale = 0.5
        // At 100% Windows scaling: Unity reads counts 1:1 → scale = 1.0
        float correctedSens = senzaiSens * calibrationScale;

        // cm/360 using the corrected sensitivity (at 800 DPI as reference)
        const float kRefDpi = 800f;
        float cm360 = (2.54f * 360f) / (kRefDpi * correctedSens);

        var sb = new System.Text.StringBuilder();
        sb.Append($"cm/360 ≈{cm360:F1} cm  |  ");

        foreach (var kvp in GameYaws)
        {
            // targetSens = correctedSens / targetYaw
            // (same as cm/360 approach — DPI cancels when both use raw input equivalents)
            float converted = correctedSens / kvp.Value;
            sb.Append($"{kvp.Key}: {converted:F2}  ");
        }

        gameConversionsText.text = sb.ToString().TrimEnd();
    }

    private void OnCloseClicked() => ClosePanel();

    private void OnResetClicked()
    {
        SensitivityOptimizer.Instance?.ResetModel();
        FindObjectOfType<SensitivityCalibrator>()?.RestartCalibration();

        if (resultText          != null) resultText.text          = "\u2713 Training data cleared. Calibration restarted.";
        if (confidenceText      != null) confidenceText.text      = "";
        if (sessionCountText    != null) sessionCountText.text    = "";
        if (gameConversionsText != null) gameConversionsText.text = "";
        if (applyBtn            != null) applyBtn.interactable    = false;

        pendingRecommendation = -1f;
    }

    // ── Clear session files ───────────────────────────────────────────────────

    private void OnClearSessionsClicked()
    {
        string dir = Application.persistentDataPath;
        string[] files = Directory.GetFiles(dir, "session_*.json");

        int deleted = 0;
        var errors  = new System.Text.StringBuilder();

        foreach (string f in files)
        {
            try   { File.Delete(f); deleted++; }
            catch (System.Exception e) { errors.AppendLine(Path.GetFileName(f) + ": " + e.Message); }
        }

        string msg = deleted > 0
            ? $"\u2713 Deleted {deleted} session file{(deleted == 1 ? "" : "s")}.\nOptimizer model kept intact."
            : "No session files found to delete.";

        if (errors.Length > 0)
            msg += $"\n\nFailed to delete:\n{errors}";

        if (resultText != null) resultText.text = msg;

        Debug.Log($"[SensitivityUI] Cleared {deleted} session files from {dir}");
    }

    private void OnSliderChanged(float val)
    {
        if (sliderValueText != null)
            sliderValueText.text = $"Preview: {val:F3}";

        if (mouseLook != null)
            mouseLook.SetSensitivity(val);
    }
}
