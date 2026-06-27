using TMPro;
using UnityEngine;

public class StatsDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI accuracyText;
    [SerializeField] private TextMeshProUGUI shotsText;
    [SerializeField] private TextMeshProUGUI timerText;

    private float elapsed;   // accumulated play-time; pauses when SensPanel is open

    void Start()
    {
        elapsed = 0f;
    }

    void Update()
    {
        // Freeze everything while the ESC panel is open
        if (SensitivityUI.PanelOpen) return;

        elapsed += Time.deltaTime;

        if (SessionLogger.Instance == null)
            return;

        SessionStats stats = SessionLogger.Instance.GetCurrentStats();

        if (accuracyText != null)
            accuracyText.text = $"Accuracy: {stats.Accuracy:F1}%";

        if (shotsText != null)
            shotsText.text = $"Shots: {stats.TotalHits}/{stats.TotalShots}";

        if (timerText != null)
            timerText.text = $"Time: {elapsed:F0}s";
    }
}
