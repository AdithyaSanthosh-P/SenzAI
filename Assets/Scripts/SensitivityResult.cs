public struct SensitivityResult
{
    public bool hasData;
    public float recommendedSensitivity;
    public float confidence;
    public string reason;

    public float ciLow;
    public float ciHigh;

    public float convergenceProgress;
    public string phase;

    public int shotsSampled;
    public int sessionsRead;
    public int binsExplored;
    public int totalBins;
}