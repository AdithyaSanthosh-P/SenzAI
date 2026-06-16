using System;
using UnityEngine;

[Serializable]
public class ShotData
{
    public bool hit;
    public float distance;
    public float timestamp;
    public string targetName;

    public ShotData(bool hit, float distance, float timestamp, string targetName)
    {
        this.hit = hit;
        this.distance = distance;
        this.timestamp = timestamp;
        this.targetName = targetName;
    }
}
