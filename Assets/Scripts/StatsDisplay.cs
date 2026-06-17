using TMPro;
using UnityEngine;

public class StatsDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI accuracyText;
    [SerializeField] private TextMeshProUGUI shotsText;
    [SerializeField] private TextMeshProUGUI timerText;

    private float sessionStart;

    void Start()
    {
        sessionStart = Time.time;
    }

    void Update()
    {
        // thisll skip if SessionLogger isn't in the scene yet
        if (SessionLogger.Instance == null)
            return;

        SessionStats stats = SessionLogger.Instance.GetCurrentStats();

        if (accuracyText != null)
            accuracyText.text = $"Accuracy: {stats.Accuracy:F1}%";

        if (shotsText != null)
            shotsText.text = $"Shots: {stats.TotalHits}/{stats.TotalShots}";

        float elapsed = Time.time - sessionStart;
        if (timerText != null)
            timerText.text = $"Time: {elapsed:F0}s";
    }
}
