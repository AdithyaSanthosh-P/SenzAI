using UnityEngine;
using TMPro;

public enum CalibrationMode { Exploring, Refining, Converged }

public class SensitivityCalibrator : MonoBehaviour
{
    [Header("Calibration Settings")]
    [Tooltip("Number of shots per exploration round before switching sensitivity")]
    [SerializeField] private int shotsPerRound = 15;

    [Tooltip("Size of sensitivity perturbation in Refining mode")]
    [SerializeField] private float refinePerturbation = 0.015f;

    [Tooltip("Duration (seconds) of smooth sensitivity transitions")]
    [SerializeField] private float transitionDuration = 1.0f;

    [Header("Auto-Transition Thresholds")]
    [Tooltip("Convergence progress threshold to move from Exploring → Refining")]
    [SerializeField] private float convergenceForRefining = 0.40f;

    [Tooltip("Number of stable rounds to move from Refining → Converged")]
    [SerializeField] private int stableRoundsNeeded = 3;

    [Tooltip("Recommendation must change by less than this to count as 'stable'")]
    [SerializeField] private float stabilityEpsilon = 0.005f;

    [Header("Behavior")]
    [Tooltip("Auto-start calibration on play? If false, must be started from UI.")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("Keep calibrating across sessions (re-enters Exploring if not converged)")]
    [SerializeField] private bool persistMode = true;

    [Header("UI (optional — leave unassigned if not used)")]
    [SerializeField] private TMP_Text modeLabel;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text sensLabel;
    [SerializeField] private GameObject calPanel;

    public CalibrationMode CurrentMode { get; private set; } = CalibrationMode.Exploring;
    public bool IsCalibrating   => CurrentMode != CalibrationMode.Converged;
    public bool IsTransitioning => isLerping;
    public int  ShotsThisRound  => shotsThisRound;
    public int  ShotsPerRound   => shotsPerRound;

    private MouseLook mouseLook;
    private int   shotsThisRound;
    private int   totalRounds;
    private float lastRecommendation = -1f;
    private int   stableRoundCount;


    private bool  isLerping;
    private float lerpTimer;
    private float lerpFrom;
    private float lerpTo;

    private void Start()
    {
        mouseLook = FindObjectOfType<MouseLook>();

        if (!autoStart)
        {
            CurrentMode = CalibrationMode.Converged;
            UpdateUI();
            return;
        }


        var opt = SensitivityOptimizer.Instance;
        if (opt != null && opt.HasConverged && persistMode)
        {
            CurrentMode = CalibrationMode.Converged;
            var rec = opt.GetRecommendation();
            if (rec.hasData)
                SetSensitivityImmediate(rec.recommendedSensitivity);
            Debug.Log("[Calibrator] Optimizer already converged. Starting in Converged mode.");
        }
        else
        {
            CurrentMode = CalibrationMode.Exploring;
            StartNextRound();
            Debug.Log("[Calibrator] Starting in Exploring mode.");
        }

        UpdateUI();
    }

    private void Update()
    {
        // Handle smooth sensitivity transitions
        if (isLerping)
        {
            lerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(lerpTimer / transitionDuration);
            t = t * t * (3f - 2f * t); // smoothstep easing

            float sens = Mathf.Lerp(lerpFrom, lerpTo, t);
            if (mouseLook != null)
                mouseLook.SetSensitivity(sens);

            if (t >= 1f)
                isLerping = false;
        }

        UpdateUI();
    }



    public void OnShotFired()
    {
        if (CurrentMode == CalibrationMode.Converged) return;

        shotsThisRound++;

        if (shotsThisRound >= shotsPerRound)
        {
            totalRounds++;
            EvaluateTransitions();
            StartNextRound();
        }
    }

    private void StartNextRound()
    {
        shotsThisRound = 0;
        var opt = SensitivityOptimizer.Instance;
        if (opt == null) return;

        float targetSens;

        switch (CurrentMode)
        {
            case CalibrationMode.Exploring:
                targetSens = opt.GetExplorationSensitivity();
                break;

            case CalibrationMode.Refining:
                var rec = opt.GetRecommendation();
                float baseSens = rec.hasData ? rec.recommendedSensitivity : 0.15f;
                targetSens = baseSens + Random.Range(-refinePerturbation, refinePerturbation);
                break;

            default:
                return;
        }

        targetSens = Mathf.Clamp(targetSens, opt.MinSens, opt.MaxSens);
        LerpToSensitivity(targetSens);

        Debug.Log($"[Calibrator] Round {totalRounds + 1} ({CurrentMode}) — sensitivity {targetSens:F3}");
    }

    private void EvaluateTransitions()
    {
        var opt = SensitivityOptimizer.Instance;
        if (opt == null) return;

        var rec = opt.GetRecommendation();

        switch (CurrentMode)
        {
            case CalibrationMode.Exploring:
                if (rec.hasData && rec.convergenceProgress >= convergenceForRefining)
                {
                    CurrentMode        = CalibrationMode.Refining;
                    lastRecommendation = rec.recommendedSensitivity;
                    stableRoundCount   = 0;
                    Debug.Log($"[Calibrator] Transitioning to Refining. Convergence: {rec.convergenceProgress:P0}");
                }
                break;

            case CalibrationMode.Refining:
                if (rec.hasData)
                {
                    if (Mathf.Abs(rec.recommendedSensitivity - lastRecommendation) < stabilityEpsilon)
                        stableRoundCount++;
                    else
                        stableRoundCount = 0;

                    lastRecommendation = rec.recommendedSensitivity;

                    if (stableRoundCount >= stableRoundsNeeded)
                    {
                        CurrentMode = CalibrationMode.Converged;
                        LerpToSensitivity(rec.recommendedSensitivity);
                        opt.SaveModel();
                        Debug.Log($"[Calibrator] Converged! Optimal sensitivity: {rec.recommendedSensitivity:F3}");
                    }
                }
                break;
        }
    }

    // Sensitivity Control 

    private void LerpToSensitivity(float target)
    {
        lerpFrom  = mouseLook != null ? mouseLook.CurrentSensitivity : target;
        lerpTo    = target;
        lerpTimer = 0f;
        isLerping = true;
    }

    private void SetSensitivityImmediate(float value)
    {
        if (mouseLook != null)
            mouseLook.SetSensitivity(value);
        isLerping = false;
    }

    public void RestartCalibration()
    {
        CurrentMode      = CalibrationMode.Exploring;
        totalRounds      = 0;
        stableRoundCount = 0;
        shotsThisRound   = 0;
        StartNextRound();
        Debug.Log("[Calibrator] Calibration restarted.");
    }


    public void StopCalibration()
    {
        CurrentMode = CalibrationMode.Converged;
        isLerping   = false;

        var rec = SensitivityOptimizer.Instance?.GetRecommendation();
        if (rec.HasValue && rec.Value.hasData)
            SetSensitivityImmediate(rec.Value.recommendedSensitivity);

        SensitivityOptimizer.Instance?.SaveModel();
        Debug.Log("[Calibrator] Calibration stopped.");
    }


    private void UpdateUI()
    {
        if (calPanel != null && !calPanel.activeSelf && IsCalibrating)
            calPanel.SetActive(true);
        if (calPanel != null && calPanel.activeSelf && !IsCalibrating)
            calPanel.SetActive(false);

        if (modeLabel != null)
        {
            string modeStr = CurrentMode switch
            {
                CalibrationMode.Exploring => "<color=#3B82F6>Exploring</color>",
                CalibrationMode.Refining  => "<color=#F59E0B>Refining</color>",
                CalibrationMode.Converged => "<color=#10B981>Converged</color>",
                _                         => ""
            };
            modeLabel.text = $"Mode: {modeStr}";
        }

        if (progressLabel != null && IsCalibrating)
        {
            var rec = SensitivityOptimizer.Instance?.GetRecommendation();
            float conv = rec.HasValue ? rec.Value.convergenceProgress : 0f;
            progressLabel.text = $"Round {totalRounds + 1}  |  Shot {shotsThisRound}/{shotsPerRound}  |  Conv: {conv:P0}";
        }

        if (sensLabel != null && mouseLook != null)
            sensLabel.text = $"Sensitivity: {mouseLook.CurrentSensitivity:F3}";
    }
}
