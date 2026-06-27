public readonly struct SessionStats
{
    public readonly int TotalShots;
    public readonly int TotalHits;
    public readonly float Accuracy;
    public readonly float AvgHitDistance;
    public readonly float AvgReactionTime;   // -1 means no valid samples yet
    public readonly float AvgAimDeviation;   // degrees, -1 means no valid samples yet

    public SessionStats(int totalShots, int totalHits, float accuracy, float avgHitDistance,
                        float avgReactionTime = -1f, float avgAimDeviation = -1f)
    {
        TotalShots       = totalShots;
        TotalHits        = totalHits;
        Accuracy         = accuracy;
        AvgHitDistance   = avgHitDistance;
        AvgReactionTime  = avgReactionTime;
        AvgAimDeviation  = avgAimDeviation;
    }
}
