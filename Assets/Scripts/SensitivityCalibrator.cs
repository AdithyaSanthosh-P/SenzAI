using UnityEngine;
using TMPro;

// CalibrationMode tracks what phase the calibrator is in
public enum CalibrationMode { Exploring, Refining, Converged }

// SensitivityCalibrator.cs
// This script manages the actual calibration loop - it picks a sensitivity for the player
// to try, waits for some shots, then evaluates whether to switch to a different one.
// Works alongside SensitivityOptimizer which does the actual math.

public class SensitivityCalibrator : MonoBehaviour
{
    [Header("Calibration Settings")]
    [Tooltip("Number of shots per round before we switch to a new sensitivity")]
    [SerializeField] private int shotsPerRound = 15;

    [Tooltip("How much to vary sensitivity during the refining stage")]
    [SerializeField] private float refinePerturbation = 0.015f;

    [Tooltip("How long sensitivity transitions take (seconds)")]
    [SerializeField] private float transitionDuration = 1.0f;

    [Header("Transition Thresholds")]
    [SerializeField] private float convergenceForRefining = 0.40f; // how converged before we switch to refining
    [SerializeField] private int stableRoundsNeeded = 3; // how many stable rounds before we declare converged
    [SerializeField] private float stabilityEpsilon = 0.005f; // recommendation must change by less than this to count as stable

    [Header("Behavior")]
    [SerializeField] private bool autoStart = true; // start calibrating automatically on play?
    [SerializeField] private bool persistMode = true; // remember calibration state between sessions

    [Header("UI (leave unassigned if not using the calibration panel)")]
    [SerializeField] private TMP_Text modeLabel;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text sensLabel;
    [SerializeField] private GameObject calPanel;

    public CalibrationMode CurrentMode { get; private set; } = CalibrationMode.Exploring;
    public bool IsCalibrating => CurrentMode != CalibrationMode.Converged;
    public bool IsTransitioning => isLerping;
    public int ShotsThisRound => shotsThisRound;
    public int ShotsPerRound => shotsPerRound;

    private MouseLook mouseLook;
    private int shotsThisRound;
    private int totalRounds;
    private float lastRecommendation = -1f;
    private int stableRoundCount;

    // lerp state for smooth sensitivity transitions
    private bool isLerping;
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
            // already converged from a previous session - just apply the saved recommendation
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
        // smooth lerp between sensitivity values
        if (isLerping)
        {
            lerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(lerpTimer / transitionDuration);
            t = t * t * (3f - 2f * t); // smoothstep

            float sens = Mathf.Lerp(lerpFrom, lerpTo, t);
            if (mouseLook != null)
                mouseLook.SetSensitivity(sens);

            if (t >= 1f)
                isLerping = false;
        }

        UpdateUI();
    }

    // called by ShootingController each time a shot is fired
    public void OnShotFired()
    {
        if (CurrentMode == CalibrationMode.Converged) return;

        shotsThisRound++;

        if (shotsThisRound >= shotsPerRound)
        {
            totalRounds++;
            EvaluateTransitions(); // check if we should move to a different mode
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
                // let the optimizer pick the next sensitivity to try
                targetSens = opt.GetExplorationSensitivity();
                break;

            case CalibrationMode.Refining:
                // stay near the current recommendation but add a small perturbation
                var rec = opt.GetRecommendation();
                float baseSens = rec.hasData ? rec.recommendedSensitivity : 0.15f;
                targetSens = baseSens + Random.Range(-refinePerturbation, refinePerturbation);
                break;

            default:
                return;
        }

        targetSens = Mathf.Clamp(targetSens, opt.MinSens, opt.MaxSens);
        LerpToSensitivity(targetSens);

        Debug.Log($"[Calibrator] Round {totalRounds + 1} ({CurrentMode}) - sensitivity {targetSens:F3}");
    }

    private void EvaluateTransitions()
    {
        var opt = SensitivityOptimizer.Instance;
        if (opt == null) return;

        var rec = opt.GetRecommendation();

        switch (CurrentMode)
        {
            case CalibrationMode.Exploring:
                // once convergence crosses the threshold, move to refining
                if (rec.hasData && rec.convergenceProgress >= convergenceForRefining)
                {
                    CurrentMode = CalibrationMode.Refining;
                    lastRecommendation = rec.recommendedSensitivity;
                    stableRoundCount = 0;
                    Debug.Log($"[Calibrator] Transitioning to Refining. Convergence: {rec.convergenceProgress:P0}");
                }
                break;

            case CalibrationMode.Refining:
                if (rec.hasData)
                {
                    // if the recommendation hasn't changed much, increment stable counter
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

    private void LerpToSensitivity(float target)
    {
        lerpFrom = mouseLook != null ? mouseLook.CurrentSensitivity : target;
        lerpTo = target;
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
        CurrentMode = CalibrationMode.Exploring;
        totalRounds = 0;
        stableRoundCount = 0;
        shotsThisRound = 0;
        StartNextRound();
        Debug.Log("[Calibrator] Calibration restarted.");
    }

    public void StopCalibration()
    {
        CurrentMode = CalibrationMode.Converged;
        isLerping = false;

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
