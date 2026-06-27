using System;
using UnityEngine;

[Serializable]
public class ShotData
{
    public bool   hit;
    public float  distance;
    public float  timestamp;
    public string targetName;

    // sensitivity telemetry
    public float sensitivity;
    public float aimDeviation;       // total angular error (degrees) — -1 for misses
    public float reactionTime;       // seconds from target spawn to shot — -1 if unknown
    public float mouseSpeed;         // mouse delta magnitude at fire time

    // new: richer aim telemetry
    public float crosshairOffsetX;   // signed horizontal error (degrees) — positive = aim too right
    public float crosshairOffsetY;   // signed vertical error (degrees) — positive = aim too high
    public float timeSinceLastShot;  // seconds since previous shot in this session (-1 = first shot)
    public int   shotIndex;          // 0-based shot number in session (warm-up detection)
    public float targetAngularSize;  // apparent target size in degrees (-1 = unknown)

    public ShotData(bool hit, float distance, float timestamp, string targetName,
                    float sensitivity, float aimDeviation, float reactionTime, float mouseSpeed,
                    float crosshairOffsetX, float crosshairOffsetY,
                    float timeSinceLastShot, int shotIndex, float targetAngularSize)
    {
        this.hit               = hit;
        this.distance          = distance;
        this.timestamp         = timestamp;
        this.targetName        = targetName;
        this.sensitivity       = sensitivity;
        this.aimDeviation      = aimDeviation;
        this.reactionTime      = reactionTime;
        this.mouseSpeed        = mouseSpeed;
        this.crosshairOffsetX  = crosshairOffsetX;
        this.crosshairOffsetY  = crosshairOffsetY;
        this.timeSinceLastShot = timeSinceLastShot;
        this.shotIndex         = shotIndex;
        this.targetAngularSize = targetAngularSize;
    }
}
