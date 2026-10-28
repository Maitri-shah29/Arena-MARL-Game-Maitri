using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Barracuda;
using Unity.MLAgents;
using UnityEngine;

public class GameController : MonoBehaviour
{
    [SerializeField] private int episodeSteps = 240;
    [SerializeField] private float gracePeriodFraction = 0.4f;
    [SerializeField] private int playEpisodeSteps = 2500;
    [SerializeField] private float playGracePeriodFraction = 0.12f;
    [SerializeField] private float coneAngle = 0.375f * 180f;

    [SerializeField] private MapGenerator mapGenerator = null;


    [Serializable]
    public struct RewardInfo
    {
        public enum Type { VisibilityIndividual, VisibilityTeam, Capture, OobPenalty };
        public Type type;
        public float weight;
    };

    public enum WinCondition { None, LineOfSight, Capture, Evacuation };

    [Header("Game Rules")]
    [SerializeField] private List<RewardInfo> rewards = null;
    [SerializeField] private WinCondition winCondition = WinCondition.None;
    [SerializeField] private float winConditionRewardMultiplier = 1.0f;
    [SerializeField] private float arenaSize = 20f;
    [SerializeField] private bool allowCapture = false;
    [SerializeField] private float captureDistance = 1.2f;
    [SerializeField] private int seekersCaptureGoal = 2;
    [SerializeField] private SafeTriageZone safeTriageZone = null;
    [SerializeField] private bool rescueTraining = false;
    [SerializeField] private float rescueScanReward = 0.15f;
    [SerializeField] private float rescueEscortReward = 0.25f;
    [SerializeField] private float rescueEvacuationReward = 2f;
    [SerializeField] private float rescueUnrescuedStepPenalty = 0.00002f;
    [Tooltip("Outside training, steer a drone straight to a trapped survivor it has in sight.")]
    [SerializeField] private bool droneApproachAssist = true;

    [Header("Coplay")]
    [SerializeField] private bool useCoplay = false;
    [SerializeField] private int numberOfCoplayAgents = 1;
    [SerializeField] private float selfPlayRatio = 0.5f;

    [Header("Inference")]
    [SerializeField] private bool inferenceMode = false;
    [SerializeField] private NNModel hidersCheckpoint = null;
    [SerializeField] private NNModel seekersCheckpoint = null;

    [Header("Debug")]
    [SerializeField] private bool debugDrawBoxHold = true;
    [SerializeField] private bool debugDrawVisibility = true;
    [SerializeField] private bool debugDrawIndividualReward = true;
    [SerializeField] private bool debugDrawPlayAreaBounds = true;
    [SerializeField] private bool debugLogPlatformParams = true;
    [SerializeField] private bool debugLogMatchResult = false;
    [SerializeField] private bool debugLogCoplay = false;

    [Header("Architect")]
    [Tooltip("Assign the ArchitectManager on this platform. If null or architectEnabled=false, behavior is unchanged.")]
    [SerializeField] private ArchitectManager architectManager = null;


    private int episodeTimer = 0;
    private List<AgentActions> hiders;
    private List<AgentActions> seekers;
    private List<AgentActions> hiderInstances;
    private List<AgentActions> seekerInstances;
    private SimpleMultiAgentGroup hidersGroup;
    private SimpleMultiAgentGroup seekersGroup;
    private List<BoxHolding> holdObjects;

    private bool[,] visibilityMatrix;
    private bool[] visibilityHiders;
    private bool[] visibilitySeekers;
    private bool allHidden;

    private int stepsHidden = 0;
    private int hidersCaptured = 0;
    private int survivorsLocated = 0;
    private int survivorsEvacuated = 0;
    private bool[] rescueScanDetected;
    private List<float> rescueTimes = new List<float>();
    private int completedRescueMissions;
    public int EvacuatedCount => survivorsEvacuated;
    public int LocatedCount => survivorsLocated;
    public bool RescueMode => winCondition == WinCondition.Evacuation;
    public bool RescueTraining => RescueMode && rescueTraining;
    public float ArenaSize => arenaSize;
    private bool hidersPerfectGame = true;
    private bool roundOver = false;
    private StatsRecorder statsRecorder = null;

    private int ActiveEpisodeSteps => inferenceMode ? playEpisodeSteps : episodeSteps;
    private float ActiveGraceFraction => inferenceMode ? playGracePeriodFraction : gracePeriodFraction;

    public bool GracePeriodEnded
    {
        get { return episodeTimer >= ActiveEpisodeSteps * ActiveGraceFraction; }
    }

    public bool RoundSettled => roundOver;

    public int EpisodeTimer => episodeTimer;
    public int TotalEpisodeSteps => ActiveEpisodeSteps;
    public float GraceFraction => ActiveGraceFraction;
    public float ConeAngle => coneAngle;
    public int SeekerCount => seekers == null ? 0 : seekers.Count;
    public AgentActions GetSeeker(int index) => (seekers != null && index >= 0 && index < seekers.Count) ? seekers[index] : null;
    public AgentActions GetHider(int index) => (hiders != null && index >= 0 && index < hiders.Count) ? hiders[index] : null;
    public bool GetSeekerSeesHider(int seekerIdx, int hiderIdx)
    {
        if (visibilityMatrix == null || hiders == null || seekers == null) return false;
        if (hiderIdx < 0 || hiderIdx >= hiders.Count || seekerIdx < 0 || seekerIdx >= seekers.Count) return false;
        return visibilityMatrix[hiderIdx, seekerIdx];
    }

    public bool DebugDrawBoxHold => debugDrawBoxHold;
    public bool DebugDrawIndividualReward => debugDrawIndividualReward;
    public bool CaptureEnabled => allowCapture;
    public int CapturedHiders => hidersCaptured;
    public int HiderCount => hiders == null ? 0 : hiders.Count;
    public string LastRoundSummary { get; private set; } = "";

    public event System.Action OnEpisodeReset;
    public event Action<RescueMissionResult> OnRescueMissionCompleted;

    public float HiddenPercent
    {
        get
        {
            if (!GracePeriodEnded)
            {
                return 100f;
            }

            float activeSteps = Mathf.Max(1f, episodeTimer - ActiveEpisodeSteps * ActiveGraceFraction);
            return 100f * stepsHidden / activeSteps;
        }
    }


    private void Awake()
    {
        if (SystemArgs.GameParamsPath != null)
        {
            Debug.Log("Game controller configuration file: " + SystemArgs.GameParamsPath);
            string content = File.ReadAllText(SystemArgs.GameParamsPath);
            JsonUtility.FromJsonOverwrite(content, this);
        }
        if (RescueMode) EnsureSafeTriageZone();
    }

    private void Start()
    {
        if (debugLogPlatformParams)
        {
            Console.WriteLine(gameObject.name);
            Console.WriteLine(JsonUtility.ToJson(this, true));
            if (mapGenerator != null)
            {
                Console.WriteLine(JsonUtility.ToJson(mapGenerator, true));
            }
            Console.WriteLine();
        }

        mapGenerator.Initialize();
        hiderInstances = mapGenerator.GetInstantiatedHiders();
        seekerInstances = mapGenerator.GetInstantiatedSeekers();
        hiderInstances.ForEach(hider => hider.GameController = this);
        seekerInstances.ForEach(seeker => seeker.GameController = this);

        if (inferenceMode)
        {
            hiderInstances.ForEach(hider => hider.SwitchToInference(hidersCheckpoint));
            seekerInstances.ForEach(hider => hider.SwitchToInference(seekersCheckpoint));
            foreach (AgentActions agent in hiderInstances.Concat(seekerInstances))
            {
                if (agent.HideAndSeekAgent != null)
                {
                    agent.HideAndSeekAgent.MaxStep = 0;
                }
            }
        }
        else if (RescueTraining)
        {
            // Survivors are pinned while trapped and steered while escorted, so they need no policy.
            hiderInstances.ForEach(hider => hider.SwitchToHeuristic());
            seekerInstances.ForEach(seeker => seeker.SwitchToTraining());
            foreach (AgentActions agent in hiderInstances.Concat(seekerInstances))
                agent.HideAndSeekAgent.MaxStep = 0;
        }

        hidersGroup = new SimpleMultiAgentGroup();
        seekersGroup = new SimpleMultiAgentGroup();

        holdObjects = FindObjectsOfType<BoxHolding>().ToList();

        statsRecorder = Academy.Instance.StatsRecorder;

        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.GetComponent<TacticalCameraController>() == null)
        {
            mainCam.gameObject.AddComponent<TacticalCameraController>();
        }

        if (RescueMode && !RescueTraining && GetComponent<DisasterThemeManager>() == null)
        {
            gameObject.AddComponent<DisasterThemeManager>();
        }
        if (RescueMode)
        {
            if (GetComponent<RescuePathfinder>() == null)
                gameObject.AddComponent<RescuePathfinder>();
            if (GetComponent<SpatialThermalHeatmap>() == null)
                gameObject.AddComponent<SpatialThermalHeatmap>();
            if (!RescueTraining)
            {
                if (GetComponent<RescueBatchRunner>() == null)
                    gameObject.AddComponent<RescueBatchRunner>();
                if (GetComponent<MissionTelemetryExporter>() == null)
                    gameObject.AddComponent<MissionTelemetryExporter>();
                if (GetComponent<HandTrackingReceiver>() == null)
                    gameObject.AddComponent<HandTrackingReceiver>();
            }
        }

        ResetScene();

        GenerationComparer comparer = null;
        if (inferenceMode && !RescueMode)
        {
            comparer = gameObject.AddComponent<GenerationComparer>();
            comparer.Setup(hiderInstances, hidersCheckpoint, seekersCheckpoint);
        }

        if (inferenceMode)
        {
            if (architectManager == null)
            {
                architectManager = GetComponent<ArchitectManager>();
            }

            HumanPlayer player = GetComponent<HumanPlayer>();
            if (player == null)
            {
                player = gameObject.AddComponent<HumanPlayer>();
            }
            NNModel playModel = comparer != null && comparer.CurrentModel != null
                ? comparer.CurrentModel
                : hidersCheckpoint;
            player.Setup(this, architectManager, playModel, seekersCheckpoint);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            ResetScene();
        }
    }

    private void FixedUpdate()
    {
        // While the Architect phase is active (timeScale == 0 and architect is
        // editing the arena) we must not advance the episode timer or compute
        // rewards. BeginArchitectPhase sets timeScale=0 so FixedUpdate normally
        // won't fire, but we guard here as a belt-and-suspenders safety measure.
        if (roundOver)
            return;

        if (architectManager != null && architectManager.architectEnabled && architectManager.IsArchitectPhase)
            return;

        episodeTimer++;

        Vector3 seekersMeanPosition = Vector3.zero;
        foreach (AgentActions seeker in seekers)
        {
            seekersMeanPosition += seeker.transform.position - transform.position;
        }
        seekersMeanPosition /= seekers.Count;
        statsRecorder.Add("Environment/SeekersMeanX", seekersMeanPosition.x);
        statsRecorder.Add("Environment/SeekersMeanZ", seekersMeanPosition.z);

        Vector3 hidersMeanPosition = Vector3.zero;
        foreach (AgentActions hider in hiders)
        {
            hidersMeanPosition += hider.transform.position - transform.position;
        }
        hidersMeanPosition /= hiders.Count;
        statsRecorder.Add("Environment/HidersMeanX", hidersMeanPosition.x);
        statsRecorder.Add("Environment/HidersMeanZ", hidersMeanPosition.z);

        if (RescueMode)
        {
            if (GracePeriodEnded)
            {
                FillVisibilityMatrix();
                if (RescueTraining)
                {
                    for (int h = 0; h < hiders.Count; h++)
                    {
                        if (rescueScanDetected[h] || !visibilityHiders[h]) continue;
                        rescueScanDetected[h] = true;
                        seekersGroup.AddGroupReward(rescueScanReward);
                    }
                }
                foreach (AgentActions seeker in seekers)
                {
                    if (seeker.HasRescueDestination) continue;
                    foreach (AgentActions hider in hiders)
                    {
                        SurvivorRescueState survivor = hider.GetComponent<SurvivorRescueState>();
                        if (survivor == null || survivor.CurrentState != SurvivorRescueState.State.Trapped) continue;
                        if (Vector3.Distance(seeker.transform.position, hider.transform.position) >= captureDistance) continue;
                        if (survivor.OnLocatedByDrone(seeker, safeTriageZone))
                        {
                            survivorsLocated++;
                            if (RescueTraining) seekersGroup.AddGroupReward(rescueEscortReward);
                            break;
                        }
                    }
                }
                if (droneApproachAssist && !RescueTraining)
                    AssignApproachTargets();
            }
            if (RescueTraining)
                seekersGroup.AddGroupReward(-rescueUnrescuedStepPenalty * (hiders.Count - survivorsEvacuated));
            if (episodeTimer >= ActiveEpisodeSteps || (hiders.Count > 0 && survivorsEvacuated >= hiders.Count))
                EndEpisode();
            return;
        }

        if (GracePeriodEnded)
        {
            FillVisibilityMatrix();
            stepsHidden += allHidden ? 1 : 0;
            if (!allHidden)
            {
                hidersPerfectGame = false;
            }

            if (allowCapture)
            {
                for (int i = 0; i < seekers.Count; i++)
                {
                    for (int j = 0; j < hiders.Count; j++)
                    {
                        if (!hiders[j].WasCaptured && Vector3.Distance(seekers[i].transform.position, hiders[j].transform.position) < captureDistance)
                        {
                            hidersCaptured++;
                            hiders[j].WasCaptured = true;
                            foreach (RewardInfo rewardInfo in rewards)
                            {
                                if (rewardInfo.type == RewardInfo.Type.Capture)
                                {
                                    seekers[i].HideAndSeekAgent.AddReward(rewardInfo.weight);
                                    hiders[j].HideAndSeekAgent.AddReward(-rewardInfo.weight);
                                }
                            }

                            if (hidersCaptured < hiders.Count)
                                hiders[j].gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        // Visibility rewards must use the positions and line of sight from this step.
        UpdateRewards();

        if (episodeTimer >= ActiveEpisodeSteps || (allowCapture && hidersCaptured >= hiders.Count))
        {
            EndEpisode();
        }
    }


    public IEnumerable<AgentActions> GetHiders()
    {
        if (hiders == null)
        {
            yield break;
        }

        foreach (AgentActions agent in hiders)
        {
            yield return agent;
        }
    }

    public IEnumerable<AgentActions> GetSeekers()
    {
        if (seekers == null)
        {
            yield break;
        }

        foreach (AgentActions agent in seekers)
        {
            yield return agent;
        }
    }


    private void EndEpisode()
    {
        if (roundOver)
        {
            return;
        }

        roundOver = true;
        float timeHidden = GracePeriodEnded ? stepsHidden / Mathf.Ceil(episodeTimer - ActiveEpisodeSteps * ActiveGraceFraction) : 0.0f;
        statsRecorder.Add("Environment/TimeHidden", timeHidden);

        if (allowCapture)
        {
            statsRecorder.Add("Environment/HidersCaptured", (float)hidersCaptured / hiders.Count());
        }

        if (RescueMode)
        {
            bool success = hiders.Count > 0 && survivorsEvacuated == hiders.Count;
            LastRoundSummary = success
                ? string.Format("RESCUE SUCCESS: {0}/{1} survivors evacuated.", survivorsEvacuated, hiders.Count)
                : string.Format("MISSION TIMEOUT: {0}/{1} survivors evacuated.", survivorsEvacuated, hiders.Count);
            statsRecorder.Add("Environment/RescueSuccessRatio", success ? 1f : 0f);
            statsRecorder.Add("Environment/SurvivorsLocated", survivorsLocated);
            statsRecorder.Add("Environment/SurvivorsEvacuated", survivorsEvacuated);
            SpatialThermalHeatmap heatmap = GetComponent<SpatialThermalHeatmap>();
            float coverage = heatmap != null ? heatmap.CoveragePercent : 0f;
            statsRecorder.Add("Environment/ThermalCoveragePercent", coverage);
            if (RescueTraining)
                seekersGroup.AddGroupReward(success ? winConditionRewardMultiplier : -winConditionRewardMultiplier);
            completedRescueMissions++;
            float duration = episodeTimer * Time.fixedDeltaTime;
            RescueMissionResult result = new RescueMissionResult
            {
                MissionNumber = completedRescueMissions,
                LayoutName = architectManager != null ? architectManager.CurrentLayoutName : "Random Arena",
                DurationSeconds = duration,
                MeanRescueSeconds = rescueTimes.Count > 0 ? rescueTimes.Average() : 0f,
                CoveragePercent = coverage,
                SweepEfficiency = coverage / Mathf.Max(1f, duration),
                TotalSurvivors = hiders.Count,
                LocatedSurvivors = survivorsLocated,
                EvacuatedSurvivors = survivorsEvacuated,
                Success = success
            };
            bool batchWasRunning = GetComponent<RescueBatchRunner>() != null && GetComponent<RescueBatchRunner>().IsRunning;
            OnRescueMissionCompleted?.Invoke(result);
            if (debugLogMatchResult) Debug.Log(LastRoundSummary);
            if (batchWasRunning)
            {
                CloseEpisode();
                return;
            }
        }
        else
        {
            bool hidersWon = (winCondition == WinCondition.LineOfSight && hidersPerfectGame)
                || (winCondition == WinCondition.Capture && hidersCaptured < seekersCaptureGoal);
            if (inferenceMode)
                GenerationComparer.Instance?.RecordRound(hidersWon, timeHidden, hidersCaptured, hiders.Count);
            if (winCondition != WinCondition.None)
            {
                hidersGroup.AddGroupReward(hidersWon ? winConditionRewardMultiplier : -winConditionRewardMultiplier);
                seekersGroup.AddGroupReward(hidersWon ? -winConditionRewardMultiplier : winConditionRewardMultiplier);
                statsRecorder.Add("Environment/HiderWinRatio", hidersWon ? 1f : 0f);
                LastRoundSummary = allowCapture
                    ? string.Format("{0} won. Captured {1}/{2}.", hidersWon ? "Hiders" : "Seekers", hidersCaptured, hiders.Count)
                    : string.Format("{0} won. Hiders stayed hidden {1:0}% of the round.", hidersWon ? "Hiders" : "Seekers", timeHidden * 100f);
                if (debugLogMatchResult) Debug.Log(LastRoundSummary);
            }
        }

        if (inferenceMode)
        {
            Time.timeScale = 0f;
            StartCoroutine(ShowResultThenReset());
            return;
        }

        CloseEpisode();
    }

    private IEnumerator ShowResultThenReset()
    {
        yield return new WaitForSecondsRealtime(3f);
        Time.timeScale = 1f;
        CloseEpisode();
    }

    private void CloseEpisode()
    {
        hidersGroup.EndGroupEpisode();
        seekersGroup.EndGroupEpisode();
        roundOver = false;
        ResetScene();
    }

    public void RestartRescueRound()
    {
        if (!RescueMode || hidersGroup == null || seekersGroup == null) return;
        CloseEpisode();
    }

    private void ResetScene()
    {
        stepsHidden = 0;
        hidersPerfectGame = true;
        hidersCaptured = 0;
        survivorsLocated = 0;
        survivorsEvacuated = 0;
        rescueTimes.Clear();
        episodeTimer = 0;

        bool keepPlayerLayout = architectManager != null && architectManager.architectEnabled;
        if (keepPlayerLayout)
        {
            mapGenerator.SetPersistentLayout(true);
        }

        if (!mapGenerator.InstantiatesBoxesOnReset())
        {
            foreach (BoxHolding holdObject in holdObjects)
            {
                holdObject.Reset();
            }
        }

        mapGenerator.Generate();
        hiders = hiderInstances.Take(mapGenerator.NumHiders).ToList();
        seekers = seekerInstances.Take(mapGenerator.NumSeekers).ToList();
        foreach (AgentActions hider in hiders)
        {
            if (RescueMode)
            {
                SurvivorRescueState sState = hider.GetComponent<SurvivorRescueState>();
                if (sState == null) sState = hider.gameObject.AddComponent<SurvivorRescueState>();
                sState.ResetState();
            }
            hider.ResetAgent();
            hidersGroup.RegisterAgent(hider.HideAndSeekAgent);
        }
        foreach (AgentActions seeker in seekers)
        {
            seeker.ResetAgent();
            seekersGroup.RegisterAgent(seeker.HideAndSeekAgent);
        }

        for (int i = 0; i < hiderInstances.Count; i++)
        {
            hiderInstances[i].gameObject.SetActive(i < hiders.Count);
        }
        for (int i = 0; i < seekerInstances.Count; i++)
        {
            seekerInstances[i].gameObject.SetActive(i < seekers.Count);
        }

        if (mapGenerator.UsesPersistentLayout)
        {
            foreach (ArchitectObject piece in FindObjectsOfType<ArchitectObject>())
            {
                BoxHolding box = piece != null ? piece.GetComponent<BoxHolding>() : null;
                if (box != null)
                {
                    box.RestorePlacedPose();
                }
            }
        }

        visibilityMatrix = new bool[hiders.Count, seekers.Count];
        visibilityHiders = new bool[hiders.Count];
        visibilitySeekers = new bool[seekers.Count];
        rescueScanDetected = new bool[hiders.Count];
        allHidden = false;

        if (!inferenceMode && useCoplay)
        {
            CoplayManager.Instance.Rescan();
            foreach (AgentActions agent in hiders)
            {
                agent.SwitchToTraining();
            }
            foreach (AgentActions agent in seekers)
            {
                agent.SwitchToTraining();
            }

            if (UnityEngine.Random.Range(0f, 1f) < selfPlayRatio)
            {
                int maxBound = 0;
                if (CoplayManager.TrainedTeamID == 0)
                {
                    maxBound = hiders.Count;
                }
                else
                {
                    maxBound = seekers.Count;
                }
                var numbers = Enumerable.Range(0, maxBound).ToList();
                //shuffle
                for (int i = numbers.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    int tmp = numbers[i];
                    numbers[i] = numbers[j];
                    numbers[j] = tmp;
                }
                var randomPos = numbers.Take(numberOfCoplayAgents).ToList();
                for (int i = 0; i < numberOfCoplayAgents; i++)
                {
                    NNModel model = CoplayManager.Instance.GetRandomModel();
                    if (model != null)
                    {
                        (CoplayManager.TrainedTeamID == 0 ? hiders[randomPos[i]] : seekers[randomPos[i]]).SwitchToInference(model);
                        if (debugLogCoplay)
                        {
                            Debug.LogFormat("{0}, team {1}, agent {2}, set model = {3}",
                                             gameObject.name, CoplayManager.TrainedTeamID, randomPos[i],
                                             model.name.Split("/").Last());
                        }
                    }
                }
            }
        }
        // Notify ArchitectManager that a new round is ready for editing.
        // This must be the LAST call in ResetScene so the arena is fully generated
        // and agents are placed before the architect phase begins.
        // When architectEnabled == false, BeginArchitectPhase returns immediately.
        if (architectManager != null
            && !RescueTraining
            && !(GetComponent<RescueBatchRunner>() != null && GetComponent<RescueBatchRunner>().IsRunning))
            architectManager.BeginArchitectPhase();

        OnEpisodeReset?.Invoke();
    }


    private bool AgentSeesAgent(AgentActions agent1, AgentActions agent2, out RaycastHit hit)
    {
        Vector3 direction = agent2.transform.position - agent1.transform.position;
        if (Vector3.Angle(direction, agent1.transform.forward) > coneAngle)
        {
            hit = new RaycastHit();
            return false;
        }

        Ray ray = new Ray(agent1.transform.position, direction);
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject == agent2.gameObject)
            {
                return true;
            }
        }
        return false;
    }

    // Each free drone heads for the nearest trapped survivor in its sensor cone; one drone per survivor.
    // Drones without a sighting, or piloted by the player, keep their own control.
    private void AssignApproachTargets()
    {
        bool[] claimed = new bool[hiders.Count];
        for (int s = 0; s < seekers.Count; s++)
        {
            AgentActions drone = seekers[s];
            if (drone.HasRescueDestination) continue;
            bool piloted = drone.HideAndSeekAgent != null && !drone.HideAndSeekAgent.enabled;
            int target = -1;
            float nearest = float.MaxValue;
            for (int h = 0; h < hiders.Count && !piloted; h++)
            {
                if (claimed[h] || !visibilityMatrix[h, s]) continue;
                SurvivorRescueState survivor = hiders[h].GetComponent<SurvivorRescueState>();
                if (survivor == null || survivor.CurrentState != SurvivorRescueState.State.Trapped) continue;
                float distance = (hiders[h].transform.position - drone.transform.position).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    target = h;
                }
            }
            if (target >= 0)
            {
                claimed[target] = true;
                drone.SetApproachDestination(hiders[target].transform.position, captureDistance * 0.5f);
            }
            else if (drone.IsApproaching)
            {
                drone.ClearRescueDestination();
            }
        }
    }

    private void FillVisibilityMatrix()
    {
        allHidden = true;
        Array.Fill(visibilityHiders, false);
        Array.Fill(visibilitySeekers, false);

        for (int i = 0; i < hiders.Count; i++)
        {
            for (int j = 0; j < seekers.Count; j++)
            {
                if (AgentSeesAgent(seekers[j], hiders[i], out RaycastHit hit))
                {
                    visibilityMatrix[i, j] = true;
                    visibilityHiders[i] = true;
                    visibilitySeekers[j] = true;
                    allHidden = false;
                    if (debugDrawVisibility)
                    {
                        Debug.DrawLine(seekers[j].transform.position, hit.point, new Color(1f, 0.55f, 0f));
                    }
                }
                else
                {
                    visibilityMatrix[i, j] = false;
                }
            }
        }
    }

    private void UpdateRewards()
    {
        foreach (RewardInfo rewardInfo in rewards)
        {
            switch (rewardInfo.type)
            {
                case RewardInfo.Type.VisibilityIndividual:
                    if (!GracePeriodEnded) break;
                    for (int i = 0; i < hiders.Count(); i++)
                    {
                        float reward = visibilityHiders[i] ? -rewardInfo.weight : rewardInfo.weight;
                        if (allowCapture && hiders[i].WasCaptured)
                        {
                            reward = 0f;
                        }
                        hiders[i].HideAndSeekAgent.AddReward(reward);
                    }
                    for (int i = 0; i < seekers.Count(); i++)
                    {
                        float reward = visibilitySeekers[i] ? rewardInfo.weight : -rewardInfo.weight;
                        if (allowCapture && hidersCaptured == hiders.Count())
                        {
                            reward = 0f;
                        }
                        seekers[i].HideAndSeekAgent.AddReward(reward);
                    }
                    break;

                case RewardInfo.Type.VisibilityTeam:
                    if (!GracePeriodEnded) break;
                    float hidersReward = allHidden ? rewardInfo.weight : -rewardInfo.weight;
                    hiders.ForEach((AgentActions hider) => hider.HideAndSeekAgent.AddReward(hidersReward));
                    seekers.ForEach((AgentActions seeker) => seeker.HideAndSeekAgent.AddReward(-hidersReward));
                    break;

                case RewardInfo.Type.OobPenalty:
                    foreach (AgentActions hider in hiders.Where((AgentActions agent) => IsOoB(agent)))
                    {
                        hider.HideAndSeekAgent.AddReward(-rewardInfo.weight);
                    }
                    foreach (AgentActions seeker in seekers.Where((AgentActions agent) => IsOoB(agent)))
                    {
                        seeker.HideAndSeekAgent.AddReward(-rewardInfo.weight);
                    }
                    break;
            }
        }
    }

    private bool IsOoB(AgentActions agent)
    {
        return Mathf.Max(Mathf.Abs(agent.transform.position.x - transform.position.x),
                         Mathf.Abs(agent.transform.position.z - transform.position.z)) > arenaSize * 0.5f;
    }


    private void OnDrawGizmos()
    {
        if (debugDrawPlayAreaBounds)
        {
            Vector3 center = transform.position;
            Color c = Gizmos.color;
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.5f);
            Gizmos.DrawCube(center + new Vector3(-arenaSize * 0.5f, 1f, 0f), new Vector3(0.25f, 2f, arenaSize));
            Gizmos.DrawCube(center + new Vector3(+arenaSize * 0.5f, 1f, 0f), new Vector3(0.25f, 2f, arenaSize));
            Gizmos.DrawCube(center + new Vector3(0f, 1f, -arenaSize * 0.5f), new Vector3(arenaSize, 2f, 0.25f));
            Gizmos.DrawCube(center + new Vector3(0f, 1f, +arenaSize * 0.5f), new Vector3(arenaSize, 2f, 0.25f));
            Gizmos.color = c;
        }
    }

    private void EnsureSafeTriageZone()
    {
        if (safeTriageZone == null)
        {
            GameObject triageObj = new GameObject("SafeTriageZone");
            triageObj.transform.SetParent(transform, false);
            triageObj.transform.localPosition = new Vector3(arenaSize * 0.35f, 0f, arenaSize * 0.35f);
            safeTriageZone = triageObj.AddComponent<SafeTriageZone>();
        }
        safeTriageZone.OnSurvivorEvacuated += HandleSurvivorEvacuated;
    }

    private void OnDestroy()
    {
        if (safeTriageZone != null)
            safeTriageZone.OnSurvivorEvacuated -= HandleSurvivorEvacuated;
    }

    private void HandleSurvivorEvacuated(AgentActions agent)
    {
        if (!RescueMode || hiders == null || !hiders.Contains(agent)) return;
        survivorsEvacuated++;
        rescueTimes.Add(episodeTimer * Time.fixedDeltaTime);
        if (RescueTraining) seekersGroup.AddGroupReward(rescueEvacuationReward);
    }

}
