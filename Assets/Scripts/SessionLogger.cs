using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SessionLogger : MonoBehaviour
{
    public static SessionLogger Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private bool timestampedFilenames = true;

    private readonly List<ShotData> shots = new List<ShotData>();
    private float sessionStartTime;
    private DateTime sessionStartDate;
    private bool saved = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        sessionStartTime = Time.time;
        sessionStartDate = DateTime.Now;
    }

    public void LogShot(bool hit, float distance, string targetName,
                        float sensitivity, float aimDeviation, float reactionTime, float mouseSpeed,
                        float crosshairOffsetX, float crosshairOffsetY,
                        float timeSinceLastShot, int shotIndex, float targetAngularSize)
    {
        float timestamp = Time.time - sessionStartTime;
        shots.Add(new ShotData(
            hit, distance, timestamp, targetName,
            sensitivity, aimDeviation, reactionTime, mouseSpeed,
            crosshairOffsetX, crosshairOffsetY,
            timeSinceLastShot, shotIndex, targetAngularSize
        ));
    }

    public SessionStats GetCurrentStats()
    {
        int total = shots.Count, hits = 0;
        float totalHitDist = 0f, totalReact = 0f, totalDev = 0f;
        int reactSamples = 0, devSamples = 0;

        foreach (ShotData s in shots)
        {
            if (s.hit)
            {
                hits++;
                totalHitDist += s.distance;
            }
            if (s.reactionTime >= 0f) { totalReact += s.reactionTime; reactSamples++; }
            if (s.aimDeviation >= 0f) { totalDev   += s.aimDeviation; devSamples++;   }
        }

        float acc      = total > 0 ? (float)hits / total * 100f : 0f;
        float avgDist  = hits > 0 ? totalHitDist / hits : 0f;
        float avgReact = reactSamples > 0 ? totalReact / reactSamples : -1f;
        float avgDev   = devSamples   > 0 ? totalDev   / devSamples   : -1f;

        return new SessionStats(total, hits, acc, avgDist, avgReact, avgDev);
    }

    private void OnApplicationQuit() => SaveSession();
    private void OnDestroy() { if (!saved) SaveSession(); }

    public void SaveSession()
    {
        if (saved || shots.Count == 0) return;

        SessionStats stats = GetCurrentStats();

        float minSens = float.MaxValue, maxSens = float.MinValue, sumSens = 0f;
        int sensSamples = 0;
        foreach (ShotData s in shots)
        {
            if (s.sensitivity > 0f)
            {
                minSens = Mathf.Min(minSens, s.sensitivity);
                maxSens = Mathf.Max(maxSens, s.sensitivity);
                sumSens += s.sensitivity;
                sensSamples++;
            }
        }

        SessionData data = new SessionData
        {
            sessionDate     = sessionStartDate.ToString("yyyy-MM-ddTHH:mm:ss"),
            sessionDuration = Time.time - sessionStartTime,
            totalShots      = stats.TotalShots,
            totalHits       = stats.TotalHits,
            accuracy        = stats.Accuracy,
            avgHitDistance  = stats.AvgHitDistance,
            minSensitivity  = sensSamples > 0 ? minSens : 0f,
            maxSensitivity  = sensSamples > 0 ? maxSens : 0f,
            avgSensitivity  = sensSamples > 0 ? sumSens / sensSamples : 0f,
            shots           = shots
        };

        string json     = JsonUtility.ToJson(data, prettyPrint: true);
        string filename = timestampedFilenames
            ? $"session_{sessionStartDate:yyyy-MM-dd_HH-mm-ss}.json"
            : "session.json";
        string path = Path.Combine(Application.persistentDataPath, filename);

        try
        {
            File.WriteAllText(path, json);
            saved = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SessionLogger] Save failed: {e.Message}");
        }
    }
}
