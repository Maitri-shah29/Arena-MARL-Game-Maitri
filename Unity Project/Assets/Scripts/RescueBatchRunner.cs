using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameController))]
public class RescueBatchRunner : MonoBehaviour
{
    private GameController controller;
    private ArchitectManager architect;
    private float previousTimeScale = 1f;
    private int targetCount;
    private readonly List<RescueMissionResult> results = new List<RescueMissionResult>();

    public bool IsRunning { get; private set; }
    public int CompletedCount => results.Count;
    public int TargetCount => targetCount;
    public string LatestSummary { get; private set; } = "";
    public float MeanTimeToRescueSeconds { get; private set; }
    public float ExtractionRatePercent { get; private set; }
    public float MeanCoveragePercent { get; private set; }
    public float SweepEfficiency { get; private set; }

    private void Awake()
    {
        controller = GetComponent<GameController>();
        architect = GetComponent<ArchitectManager>();
    }

    private void OnEnable()
    {
        if (controller != null) controller.OnRescueMissionCompleted += RecordMission;
    }

    private void OnDisable()
    {
        if (controller != null) controller.OnRescueMissionCompleted -= RecordMission;
        if (IsRunning) Time.timeScale = previousTimeScale;
        IsRunning = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B)) RunBatchMission(10);
    }

    public bool RunBatchMission(int count)
    {
        if (IsRunning || controller == null || !controller.RescueMode
            || controller.RescueTraining || controller.RoundSettled)
            return false;
        if (architect != null && architect.IsArchitectPhase) architect.EndArchitectPhase();
        previousTimeScale = Time.timeScale;
        targetCount = Mathf.Max(1, count);
        results.Clear();
        MeanTimeToRescueSeconds = 0f;
        ExtractionRatePercent = 0f;
        MeanCoveragePercent = 0f;
        SweepEfficiency = 0f;
        LatestSummary = "Batch 0/" + targetCount;
        IsRunning = true;
        controller.RestartRescueRound();
        Time.timeScale = 3f;
        return true;
    }

    private void RecordMission(RescueMissionResult result)
    {
        if (!IsRunning) return;
        results.Add(result);
        int evacuated = 0;
        int total = 0;
        float rescueSeconds = 0f;
        float coverage = 0f;
        float sweepEfficiency = 0f;
        foreach (RescueMissionResult sample in results)
        {
            evacuated += sample.EvacuatedSurvivors;
            total += sample.TotalSurvivors;
            rescueSeconds += sample.MeanRescueSeconds * sample.EvacuatedSurvivors;
            coverage += sample.CoveragePercent;
            sweepEfficiency += sample.SweepEfficiency;
        }
        MeanTimeToRescueSeconds = evacuated > 0 ? rescueSeconds / evacuated : 0f;
        ExtractionRatePercent = total > 0 ? 100f * evacuated / total : 0f;
        MeanCoveragePercent = coverage / results.Count;
        SweepEfficiency = sweepEfficiency / results.Count;
        LatestSummary = string.Format(
            "Batch {0}/{1} | MTTR {2:0.0}s | Extraction {3:0}% | Sweep {4:0.00}%/s",
            results.Count, targetCount, MeanTimeToRescueSeconds,
            ExtractionRatePercent, SweepEfficiency);
        if (results.Count >= targetCount)
        {
            IsRunning = false;
            Time.timeScale = previousTimeScale;
            Debug.Log("Rescue benchmark complete: " + LatestSummary);
        }
    }
}

