public readonly struct SessionStats
{
    public readonly int TotalShots;
    public readonly int TotalHits;
    public readonly float Accuracy;
    public readonly float AvgHitDistance;

    public SessionStats(int totalShots, int totalHits, float accuracy, float avgHitDistance)
    {
        TotalShots = totalShots;
        TotalHits = totalHits;
        Accuracy = accuracy;
        AvgHitDistance = avgHitDistance;
    }
}
