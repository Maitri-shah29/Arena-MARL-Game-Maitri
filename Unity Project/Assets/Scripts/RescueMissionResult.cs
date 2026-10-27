using System;

[Serializable]
public struct RescueMissionResult
{
    public int MissionNumber;
    public string LayoutName;
    public float DurationSeconds;
    public float MeanRescueSeconds;
    public float CoveragePercent;
    public float SweepEfficiency;
    public int TotalSurvivors;
    public int LocatedSurvivors;
    public int EvacuatedSurvivors;
    public bool Success;

    public float ExtractionRate => TotalSurvivors > 0
        ? 100f * EvacuatedSurvivors / TotalSurvivors : 0f;
}
