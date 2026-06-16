using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SessionData
{
    public string sessionDate;
    public float sessionDuration;
    public int totalShots;
    public int totalHits;
    public float accuracy;
    public float avgHitDistance;
    public List<ShotData> shots;
}
