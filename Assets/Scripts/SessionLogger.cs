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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        sessionStartTime = Time.time;
        sessionStartDate = DateTime.Now;
        Debug.Log($"[SessionLogger] Session started — {sessionStartDate:yyyy-MM-dd HH:mm:ss}");
    }

    public void LogShot(bool hit, float distance, string targetName)
    {
        float timestamp = Time.time - sessionStartTime;
        shots.Add(new ShotData(hit, distance, timestamp, targetName));
        Debug.Log($"[SessionLogger] {(hit ? "HIT" : "MISS")} | {targetName} | {distance:F1}m | t={timestamp:F2}s");
    }

    // Called by the HUD every frame — no allocation because SessionStats is a struct
    public SessionStats GetCurrentStats()
    {
        int total = shots.Count;
        int hits = 0;
        float totalHitDist = 0f;

        foreach (ShotData s in shots)
        {
            if (s.hit)
            {
                hits++;
                totalHitDist += s.distance;
            }
        }

        float accuracy = total > 0 ? (float)hits / total * 100f : 0f;
        float avgDist = hits > 0 ? totalHitDist / hits : 0f;

        return new SessionStats(total, hits, accuracy, avgDist);
    }

    // Covers actual builds — fires before the process exits
    private void OnApplicationQuit()
    {
        SaveSession();
    }

    // Covers hitting Stop in the editor
    private void OnDestroy()
    {
        if (!saved)
            SaveSession();
    }

    public void SaveSession()
    {
        if (saved) return;

        if (shots.Count == 0)
        {
            Debug.LogWarning("[SessionLogger] No shots recorded — nothing to save.");
            return;
        }

        SessionStats stats = GetCurrentStats();

        SessionData data = new SessionData
        {
            sessionDate = sessionStartDate.ToString("yyyy-MM-ddTHH:mm:ss"),
            sessionDuration = Time.time - sessionStartTime,
            totalShots = stats.TotalShots,
            totalHits = stats.TotalHits,
            accuracy = stats.Accuracy,
            avgHitDistance = stats.AvgHitDistance,
            shots = shots
        };

        string json = JsonUtility.ToJson(data, prettyPrint: true);

        string filename = timestampedFilenames
            ? $"session_{sessionStartDate:yyyy-MM-dd_HH-mm-ss}.json"
            : "session.json";

        string path = Path.Combine(Application.persistentDataPath, filename);

        try
        {
            File.WriteAllText(path, json);
            saved = true;
            Debug.Log($"[SessionLogger] Saved → {path}");
            Debug.Log($"[SessionLogger] {stats.TotalShots} shots | {stats.TotalHits} hits | {stats.Accuracy:F1}% accuracy | avg dist {stats.AvgHitDistance:F1}m");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SessionLogger] Save failed: {e.Message}");
        }
    }
}
