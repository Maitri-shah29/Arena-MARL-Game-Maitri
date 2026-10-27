using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(GameController))]
public class MissionTelemetryExporter : MonoBehaviour
{
    private GameController controller;
    private readonly List<RescueMissionResult> results = new List<RescueMissionResult>();
    private int exportedCount;

    public string LastExportMessage { get; private set; } = "";
    public int PendingMissionCount => results.Count - exportedCount;

    private void Awake()
    {
        controller = GetComponent<GameController>();
    }

    private void OnEnable()
    {
        if (controller != null) controller.OnRescueMissionCompleted += Record;
    }

    private void OnDisable()
    {
        if (controller != null) controller.OnRescueMissionCompleted -= Record;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.X)) ExportAudit();
    }

    private void Record(RescueMissionResult result)
    {
        results.Add(result);
    }

    public string ExportAudit()
    {
        if (PendingMissionCount <= 0)
        {
            LastExportMessage = "No new completed missions to export.";
            return null;
        }

        string directory = Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "DataSet"))
            : Path.Combine(Application.persistentDataPath, "DataSet");
        string path = Path.Combine(directory, "resq_mission_telemetry.csv");
        try
        {
            Directory.CreateDirectory(directory);
            bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            StringBuilder csv = new StringBuilder();
            if (writeHeader)
                csv.AppendLine("exported_utc,mission_number,layout_name,success,duration_seconds,total_survivors,located_survivors,evacuated_survivors,extraction_rate_percent,mttr_seconds,thermal_coverage_percent,sweep_efficiency_percent_per_second");
            for (int i = exportedCount; i < results.Count; i++)
            {
                RescueMissionResult result = results[i];
                csv.Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append(',')
                   .Append(result.MissionNumber).Append(',')
                   .Append(Escape(result.LayoutName)).Append(',')
                   .Append(result.Success ? "true" : "false").Append(',')
                   .Append(Format(result.DurationSeconds)).Append(',')
                   .Append(result.TotalSurvivors).Append(',')
                   .Append(result.LocatedSurvivors).Append(',')
                   .Append(result.EvacuatedSurvivors).Append(',')
                   .Append(Format(result.ExtractionRate)).Append(',')
                   .Append(Format(result.MeanRescueSeconds)).Append(',')
                   .Append(Format(result.CoveragePercent)).Append(',')
                   .Append(Format(result.SweepEfficiency)).AppendLine();
            }
            File.AppendAllText(path, csv.ToString(), Encoding.UTF8);
            exportedCount = results.Count;
            LastExportMessage = "Mission audit saved: " + path;
            Debug.Log(LastExportMessage);
            return path;
        }
        catch (Exception error)
        {
            LastExportMessage = "Mission audit export failed: " + error.Message;
            Debug.LogError(LastExportMessage);
            return null;
        }
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        value = value ?? "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
