using System;
using UnityEngine;
using UnityEngine.UI;

// ArchitectUI – ResQ-MARL Incident Command HUD
// Builds a state-of-the-art incident command interface:
// - Top Bar: Operation branding, live phase chip, mm:ss remaining mission timer, energy budget
// - Left Tool Column: Themed tool buttons with [key] shortcuts, live costs, preset loader, and Deploy Team button
// - Right Triage Board: Live survivor tracking (Total, Located, Escorting, Evacuated), progress bar, active drone count
// - Mission Result Banner: Full-width alert upon mission completion or collapse
[RequireComponent(typeof(Canvas))]
public class ArchitectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ArchitectManager architectManager = null;
    [SerializeField] private GameController gameController = null;

    private Canvas canvas;
    private CanvasScaler scaler;

    // Containers
    private GameObject topBarPanel;
    private GameObject leftToolPanel;
    private GameObject rightTriagePanel;
    private GameObject bottomHintPanel;
    private GameObject missionResultPanel;
    private GameObject sidePanel;

    // Top Bar UI
    private Text titleText;
    private Text phaseChipText;
    private Image phaseChipBg;
    private Text timerText;
    private Text energyText;
    private Image energyBarFill;

    // Triage Board UI
    private Text triageTotalVal;
    private Text triageLocatedVal;
    private Text triageEscortingVal;
    private Text triageEvacuatedVal;
    private Text triageDronesVal;
    private Image triageProgressFill;
    private Text triageProgressText;

    // Left Tool Column UI
    private Button[] toolButtons;
    private Image[]  toolHighlights;
    private Text[]   toolButtonTexts;
    private Text     feedbackLabel;
    private Button   deployButton;
    private Button   presetButton;
    private Image    hiderSideImage;
    private Image    seekerSideImage;

    // Mission Result Banner UI
    private Text     resultBannerTitle;
    private Text     resultBannerSubtext;
    private Image    resultBannerBg;

    private static readonly ArchitectManager.Tool[] Tools = new ArchitectManager.Tool[]
    {
        ArchitectManager.Tool.SpawnWall,
        ArchitectManager.Tool.SpawnBox,
        ArchitectManager.Tool.SpawnPillar,
        ArchitectManager.Tool.SpawnRamp,
        ArchitectManager.Tool.SpawnRubble,
        ArchitectManager.Tool.Demolish,
        ArchitectManager.Tool.PreLockHider,
        ArchitectManager.Tool.PreLockSeeker
    };

    // Color Palette
    private static readonly Color DarkPanelColor  = new Color(0.08f, 0.09f, 0.12f, 0.88f); // #14181C
    private static readonly Color DarkHeaderColor = new Color(0.05f, 0.06f, 0.08f, 0.94f);
    private static readonly Color HazardOrange    = new Color(1.00f, 0.42f, 0.00f, 1.00f); // #FF6A00
    private static readonly Color SafeGreen       = new Color(0.18f, 0.80f, 0.44f, 1.00f); // #2ECC71
    private static readonly Color AlertRed        = new Color(0.90f, 0.22f, 0.21f, 1.00f); // #E53935
    private static readonly Color DistressAmber   = new Color(1.00f, 0.70f, 0.00f, 1.00f); // #FFB300
    private static readonly Color CyanAccent      = new Color(0.00f, 0.75f, 1.00f, 1.00f);
    private static readonly Color TextWhite       = new Color(0.94f, 0.95f, 0.96f, 1.00f);
    private static readonly Color TextDim         = new Color(0.60f, 0.65f, 0.70f, 1.00f);

    private ArchitectManager.Tool lastTool = (ArchitectManager.Tool)(-1);
    private int lastEnergy = -1;
    private bool isInArchitectPhase = false;

    private void Awake()
    {
        if (architectManager == null)
            architectManager = FindObjectOfType<ArchitectManager>();
        if (gameController == null)
            gameController = FindObjectOfType<GameController>();

        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        // Modern 1080p reference canvas scaler (1920x1080, match 0.5)
        scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        BuildHUD();
    }

    private void OnEnable()
    {
        if (architectManager != null)
        {
            architectManager.OnArchitectPhaseStarted += HandleArchitectPhaseStarted;
            architectManager.OnRoundStarted          += HandleRoundStarted;
            architectManager.OnBudgetChanged         += HandleBudgetChanged;
        }
    }

    private void OnDisable()
    {
        if (architectManager != null)
        {
            architectManager.OnArchitectPhaseStarted -= HandleArchitectPhaseStarted;
            architectManager.OnRoundStarted          -= HandleRoundStarted;
            architectManager.OnBudgetChanged         -= HandleBudgetChanged;
        }
    }

    private void Update()
    {
        UpdateTopBarData();
        UpdateTriageBoardData();
        UpdateMissionResultBanner();

        if (isInArchitectPhase && architectManager != null)
        {
            if (feedbackLabel != null)
                feedbackLabel.text = architectManager.FeedbackMessage;

            if (architectManager.CurrentTool != lastTool || architectManager.EnergyRemaining != lastEnergy)
            {
                lastTool = architectManager.CurrentTool;
                lastEnergy = architectManager.EnergyRemaining;
                RefreshToolHighlights();
            }

            RefreshSideHighlight();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleArchitectPhaseStarted()
    {
        isInArchitectPhase = true;
        SetArchitectControlsVisible(true);
        UpdateButtonLabels();
        RefreshToolHighlights();
    }

    private void HandleRoundStarted()
    {
        isInArchitectPhase = false;
        SetArchitectControlsVisible(false);
    }

    private void HandleBudgetChanged(int remaining)
    {
        lastEnergy = remaining;
    }

    private void SetArchitectControlsVisible(bool visible)
    {
        if (leftToolPanel != null) leftToolPanel.SetActive(visible);
        if (sidePanel != null) sidePanel.SetActive(visible);
        if (bottomHintPanel != null) bottomHintPanel.SetActive(visible);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI Building (uGUI)
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildHUD()
    {
        BuildTopBar();
        BuildLeftToolColumn();
        BuildRightTriageBoard();
        BuildBottomHintBar();
        BuildSidePanel();
        BuildMissionResultBanner();

        // Initial visibility
        SetArchitectControlsVisible(architectManager != null && architectManager.IsArchitectPhase);
        if (missionResultPanel != null) missionResultPanel.SetActive(false);
    }

    private void BuildTopBar()
    {
        // Top banner: Full width, height 60px
        topBarPanel = CreatePanel("TopBar",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 60f),
            DarkHeaderColor);

        // Accent line at bottom of top bar (Hazard Orange)
        GameObject accentLine = CreatePanel("TopBarAccent",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, 2f),
            HazardOrange);
        accentLine.transform.SetParent(topBarPanel.transform, false);

        // Left Branding
        titleText = CreateText(topBarPanel.transform, "TitleText",
            "RESQ-MARL // INCIDENT COMMAND",
            17, TextWhite, TextAnchor.MiddleLeft,
            new Vector2(0f, 0f), new Vector2(0.35f, 1f),
            new Vector2(24f, 0f), new Vector2(0f, 0f));
        titleText.fontStyle = FontStyle.Bold;

        // Center Phase Chip
        GameObject chip = CreatePanel("PhaseChip",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(280f, 36f),
            new Color(0.12f, 0.15f, 0.20f, 0.95f));
        chip.transform.SetParent(topBarPanel.transform, false);
        phaseChipBg = chip.GetComponent<Image>();

        phaseChipText = CreateText(chip.transform, "PhaseChipText",
            "SITE SURVEY PHASE",
            13, HazardOrange, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        phaseChipText.fontStyle = FontStyle.Bold;

        // Right Timer & Energy
        GameObject rightBox = CreatePanel("RightStatusBox",
            new Vector2(0.70f, 0f), new Vector2(1f, 1f),
            new Vector2(1f, 0.5f),
            new Vector2(-24f, 0f), new Vector2(400f, 50f),
            Color.clear);
        rightBox.transform.SetParent(topBarPanel.transform, false);

        timerText = CreateText(rightBox.transform, "TimerText",
            "TIME: --:--",
            15, DistressAmber, TextAnchor.MiddleRight,
            new Vector2(0f, 0f), new Vector2(0.48f, 1f),
            Vector2.zero, Vector2.zero);
        timerText.fontStyle = FontStyle.Bold;

        energyText = CreateText(rightBox.transform, "EnergyText",
            "ENERGY: 100/100",
            15, CyanAccent, TextAnchor.MiddleRight,
            new Vector2(0.50f, 0f), new Vector2(1f, 1f),
            Vector2.zero, Vector2.zero);
        energyText.fontStyle = FontStyle.Bold;
    }

    private void BuildLeftToolColumn()
    {
        // Left Column: Anchored top-left, width 260px, height 680px
        leftToolPanel = CreatePanel("ToolColumn",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(20f, -80f), new Vector2(260f, 680f),
            DarkPanelColor);

        // Header label
        Text header = CreateText(leftToolPanel.transform, "ToolHeader",
            "ARCHITECT TOOLKIT",
            14, HazardOrange, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16f, -34f), new Vector2(-16f, -6f));
        header.fontStyle = FontStyle.Bold;

        // Feedback / instructions label
        feedbackLabel = CreateText(leftToolPanel.transform, "FeedbackLabel",
            "Click floor to place debris.",
            12, DistressAmber, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16f, -62f), new Vector2(-16f, -36f));

        toolButtons     = new Button[Tools.Length];
        toolHighlights  = new Image[Tools.Length];
        toolButtonTexts = new Text[Tools.Length];

        // 8 Themed Tool Buttons (Amendment 3)
        for (int i = 0; i < Tools.Length; i++)
        {
            int idx = i;
            float yPos = -85f - (i * 48f);

            GameObject btnGo = CreateButton(leftToolPanel.transform, $"ToolBtn_{i}",
                GetToolButtonLabel(Tools[i], idx + 1),
                new Vector2(12f, yPos), new Vector2(236f, 42f),
                out Text btnText);

            toolButtons[i]     = btnGo.GetComponent<Button>();
            toolHighlights[i]  = btnGo.GetComponent<Image>();
            toolButtonTexts[i] = btnText;

            toolButtons[i].onClick.AddListener(() =>
            {
                if (architectManager != null)
                {
                    architectManager.SetTool(Tools[idx]);
                }
            });
        }

        // Preset Button: [L] Load Collapsed Building (Amendment 3)
        GameObject presetGo = CreateButton(leftToolPanel.transform, "PresetBtn",
            "[L] Load Collapsed Building",
            new Vector2(12f, -85f - (Tools.Length * 48f) - 6f), new Vector2(236f, 40f),
            out Text presetText);
        presetButton = presetGo.GetComponent<Button>();
        presetText.fontSize = 13;
        ColorBlock pcb = presetButton.colors;
        pcb.normalColor = new Color(0.18f, 0.28f, 0.45f, 0.95f);
        pcb.highlightedColor = new Color(0.25f, 0.40f, 0.65f, 1f);
        presetButton.colors = pcb;
        presetGo.GetComponent<Image>().color = pcb.normalColor;
        presetButton.onClick.AddListener(() => architectManager?.LoadDisasterPreset());

        // Deploy Team Button [SPACE] (Vibrant Green)
        GameObject deployGo = CreateButton(leftToolPanel.transform, "DeployBtn",
            "DEPLOY TEAM [SPACE]",
            new Vector2(12f, -85f - (Tools.Length * 48f) - 54f), new Vector2(236f, 48f),
            out Text deployText);
        deployButton = deployGo.GetComponent<Button>();
        deployText.fontSize = 15;
        deployText.color = TextWhite;
        ColorBlock dcb = deployButton.colors;
        dcb.normalColor      = new Color(0.12f, 0.58f, 0.28f, 0.95f);
        dcb.highlightedColor = new Color(0.18f, 0.78f, 0.38f, 1f);
        dcb.pressedColor     = new Color(0.08f, 0.42f, 0.20f, 1f);
        deployButton.colors = dcb;
        deployGo.GetComponent<Image>().color = dcb.normalColor;
        deployButton.onClick.AddListener(() => architectManager?.EndArchitectPhase());
    }

    private void BuildRightTriageBoard()
    {
        // Right Triage Board: Anchored top-right, width 280px, height 360px (stays visible during round)
        rightTriagePanel = CreatePanel("TriageBoard",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-20f, -80f), new Vector2(280f, 340f),
            DarkPanelColor);

        // Header
        Text header = CreateText(rightTriagePanel.transform, "TriageHeader",
            "INCIDENT TRIAGE STATUS",
            14, SafeGreen, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16f, -34f), new Vector2(-16f, -6f));
        header.fontStyle = FontStyle.Bold;

        // Stats Rows
        float startY = -48f;
        float rowHeight = 34f;

        CreateTriageRow(rightTriagePanel.transform, "TOTAL SURVIVORS", out triageTotalVal, startY - 0 * rowHeight, TextWhite);
        CreateTriageRow(rightTriagePanel.transform, "LOCATED", out triageLocatedVal, startY - 1 * rowHeight, DistressAmber);
        CreateTriageRow(rightTriagePanel.transform, "BEING ESCORTED", out triageEscortingVal, startY - 2 * rowHeight, new Color(1f, 0.9f, 0.2f));
        CreateTriageRow(rightTriagePanel.transform, "EVACUATED (SAFE)", out triageEvacuatedVal, startY - 3 * rowHeight, SafeGreen);
        CreateTriageRow(rightTriagePanel.transform, "RESCUE DRONES ACTIVE", out triageDronesVal, startY - 4 * rowHeight, HazardOrange);

        // Evacuation Progress Bar
        GameObject progBg = CreatePanel("ProgressBarBg",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(248f, 24f),
            new Color(0.12f, 0.14f, 0.18f, 0.95f));
        progBg.transform.SetParent(rightTriagePanel.transform, false);

        GameObject progFill = CreatePanel("ProgressBarFill",
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero, new Vector2(0f, 0f),
            SafeGreen);
        progFill.transform.SetParent(progBg.transform, false);
        triageProgressFill = progFill.GetComponent<Image>();

        triageProgressText = CreateText(progBg.transform, "ProgressText",
            "0% EVACUATED",
            11, TextWhite, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        triageProgressText.fontStyle = FontStyle.Bold;
    }

    private void CreateTriageRow(Transform parent, string label, out Text valueText, float yOffset, Color valColor)
    {
        CreateText(parent, label + "_Label", label,
            12, TextDim, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0.7f, 1f),
            new Vector2(16f, yOffset - 26f), new Vector2(0f, yOffset));

        valueText = CreateText(parent, label + "_Val", "0",
            14, valColor, TextAnchor.MiddleRight,
            new Vector2(0.7f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, yOffset - 26f), new Vector2(-16f, yOffset));
        valueText.fontStyle = FontStyle.Bold;
    }

    private void BuildSidePanel()
    {
        // Side Panel: Operator role selection (anchored right, below triage board)
        sidePanel = CreatePanel("SidePanel",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-20f, -440f), new Vector2(280f, 130f),
            DarkPanelColor);

        Text header = CreateText(sidePanel.transform, "SideHeader",
            "OPERATOR MANUAL OVERRIDE",
            13, TextWhite, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16f, -30f), new Vector2(-16f, -6f));
        header.fontStyle = FontStyle.Bold;

        GameObject droneBtn = CreateButton(sidePanel.transform, "DroneBtn", "Rescue Drone",
            new Vector2(12f, -40f), new Vector2(122f, 36f), out Text droneLabel);
        seekerSideImage = droneBtn.GetComponent<Image>();
        droneBtn.GetComponent<Button>().onClick.AddListener(() => FindObjectOfType<HumanPlayer>()?.SetSide(false));

        GameObject survBtn = CreateButton(sidePanel.transform, "SurvivorBtn", "Survivor",
            new Vector2(146f, -40f), new Vector2(122f, 36f), out Text survLabel);
        hiderSideImage = survBtn.GetComponent<Image>();
        survBtn.GetComponent<Button>().onClick.AddListener(() => FindObjectOfType<HumanPlayer>()?.SetSide(true));
    }

    private void BuildBottomHintBar()
    {
        // Bottom Hint Bar: Anchored bottom-center, width 720px, height 36px
        bottomHintPanel = CreatePanel("BottomHintBar",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 16f), new Vector2(760f, 38f),
            DarkPanelColor);

        Text hintText = CreateText(bottomHintPanel.transform, "HintText",
            "Click floor to place  |  [R] Rotate  |  [Z] Undo  |  [L] Load Preset  |  [Space] Deploy Team",
            13, TextWhite, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one,
            new Vector2(12f, 0f), new Vector2(-12f, 0f));
        hintText.fontStyle = FontStyle.Bold;
    }

    private void BuildMissionResultBanner()
    {
        // Full width mission settlement banner (Amendment 5)
        missionResultPanel = CreatePanel("MissionResultBanner",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 40f), new Vector2(800f, 150f),
            new Color(0.06f, 0.08f, 0.10f, 0.95f));

        resultBannerBg = missionResultPanel.GetComponent<Image>();

        resultBannerTitle = CreateText(missionResultPanel.transform, "ResultTitle",
            "MISSION COMPLETE",
            26, SafeGreen, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.5f), new Vector2(1f, 1f),
            new Vector2(16f, -8f), new Vector2(-16f, -12f));
        resultBannerTitle.fontStyle = FontStyle.Bold;

        resultBannerSubtext = CreateText(missionResultPanel.transform, "ResultSubtext",
            "All survivors safely evacuated to Triage Base.",
            16, TextWhite, TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(1f, 0.5f),
            new Vector2(24f, 8f), new Vector2(-24f, 0f));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Data Refresh
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateTopBarData()
    {
        if (gameController == null) gameController = FindObjectOfType<GameController>();
        if (gameController == null) return;

        // Phase Chip
        if (phaseChipText != null)
        {
            if (architectManager != null && architectManager.IsArchitectPhase)
            {
                phaseChipText.text = "SITE SURVEY // PREPARATION";
                phaseChipText.color = HazardOrange;
                if (phaseChipBg != null) phaseChipBg.color = new Color(0.20f, 0.12f, 0.05f, 0.95f);
            }
            else if (gameController.RoundSettled)
            {
                bool isSuccess = gameController.LastRoundSummary.Contains("SUCCESS");
                phaseChipText.text = isSuccess ? "MISSION COMPLETE" : "MISSION FAILED";
                phaseChipText.color = isSuccess ? SafeGreen : AlertRed;
                if (phaseChipBg != null) phaseChipBg.color = isSuccess ? new Color(0.05f, 0.22f, 0.10f, 0.95f) : new Color(0.25f, 0.06f, 0.06f, 0.95f);
            }
            else if (!gameController.GracePeriodEnded)
            {
                phaseChipText.text = "SHELTER PHASE // DISASTER RESPONSE";
                phaseChipText.color = DistressAmber;
                if (phaseChipBg != null) phaseChipBg.color = new Color(0.22f, 0.18f, 0.06f, 0.95f);
            }
            else
            {
                phaseChipText.text = "SEARCH & RESCUE ACTIVE";
                phaseChipText.color = CyanAccent;
                if (phaseChipBg != null) phaseChipBg.color = new Color(0.05f, 0.18f, 0.24f, 0.95f);
            }
        }

        // Timer: Convert steps to seconds via fixedDeltaTime, format as mm:ss (Amendment 6)
        if (timerText != null)
        {
            int remainingSteps = Mathf.Max(0, gameController.TotalEpisodeSteps - gameController.EpisodeTimer);
            float remainingSeconds = remainingSteps * Time.fixedDeltaTime;
            int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
            int seconds = Mathf.FloorToInt(remainingSeconds % 60f);
            timerText.text = $"TIME: {minutes:D2}:{seconds:D2}";
        }

        // Energy budget
        if (energyText != null && architectManager != null)
        {
            int cur = architectManager.EnergyRemaining;
            int max = architectManager.EnergyBudget;
            energyText.text = $"ENERGY: {cur}/{max}";
            energyText.color = cur > 20 ? CyanAccent : AlertRed;
        }
    }

    private void UpdateTriageBoardData()
    {
        if (gameController == null) return;

        int totalSurvivors = gameController.HiderCount;
        int evacuated = gameController.EvacuatedCount;
        int located = DisasterThemeManager.Instance != null
            ? DisasterThemeManager.Instance.GetLocatedSurvivorCount()
            : gameController.CapturedHiders;

        // Calculate currently escorting survivors
        int escorting = 0;
        for (int i = 0; i < totalSurvivors; i++)
        {
            AgentActions hider = gameController.GetHider(i);
            if (hider != null)
            {
                SurvivorRescueState sState = hider.GetComponent<SurvivorRescueState>();
                if (sState != null && sState.CurrentState == SurvivorRescueState.State.BeingEscorted)
                {
                    escorting++;
                }
            }
        }

        if (triageTotalVal != null) triageTotalVal.text = totalSurvivors.ToString();
        if (triageLocatedVal != null) triageLocatedVal.text = located.ToString();
        if (triageEscortingVal != null) triageEscortingVal.text = escorting.ToString();
        if (triageEvacuatedVal != null) triageEvacuatedVal.text = evacuated.ToString();
        if (triageDronesVal != null) triageDronesVal.text = gameController.SeekerCount.ToString();

        // Evacuation Progress Bar
        float progress = totalSurvivors > 0 ? (float)evacuated / totalSurvivors : 0f;
        if (triageProgressFill != null)
        {
            RectTransform rt = triageProgressFill.rectTransform;
            rt.anchorMax = new Vector2(progress, 1f);
        }
        if (triageProgressText != null)
        {
            triageProgressText.text = $"{(int)(progress * 100f)}% EVACUATED ({evacuated}/{totalSurvivors})";
        }
    }

    private void UpdateMissionResultBanner()
    {
        if (gameController == null || missionResultPanel == null) return;

        if (gameController.RoundSettled)
        {
            if (!missionResultPanel.activeSelf)
            {
                missionResultPanel.SetActive(true);
            }

            string summary = gameController.LastRoundSummary;
            bool isSuccess = summary.Contains("SUCCESS");

            resultBannerTitle.text = isSuccess ? "MISSION COMPLETE" : "MISSION FAILED";
            resultBannerTitle.color = isSuccess ? SafeGreen : AlertRed;
            resultBannerSubtext.text = !string.IsNullOrEmpty(summary) ? summary : (isSuccess ? "All survivors evacuated safely!" : "Structural collapse before full extraction.");

            if (resultBannerBg != null)
            {
                resultBannerBg.color = isSuccess
                    ? new Color(0.04f, 0.16f, 0.08f, 0.96f)
                    : new Color(0.20f, 0.05f, 0.05f, 0.96f);
            }
        }
        else
        {
            if (missionResultPanel.activeSelf)
            {
                missionResultPanel.SetActive(false);
            }
        }
    }

    private void RefreshToolHighlights()
    {
        if (architectManager == null || toolHighlights == null) return;

        ArchitectManager.Tool current = architectManager.CurrentTool;

        for (int i = 0; i < Tools.Length && i < toolHighlights.Length; i++)
        {
            if (toolHighlights[i] == null) continue;
            bool active = (Tools[i] == current ||
                (Tools[i] == ArchitectManager.Tool.PreLockHider && current == ArchitectManager.Tool.PreLockSeeker));

            toolHighlights[i].color = active
                ? HazardOrange                          // Selected tool – Hazard Orange accent
                : new Color(0.14f, 0.16f, 0.20f, 0.92f); // Normal – Dark slate panel
        }
    }

    private void RefreshSideHighlight()
    {
        HumanPlayer player = FindObjectOfType<HumanPlayer>();
        bool asHider = player == null || player.PlayingAsHider;
        if (hiderSideImage != null)
        {
            hiderSideImage.color = asHider
                ? CyanAccent
                : new Color(0.14f, 0.16f, 0.20f, 0.9f);
        }
        if (seekerSideImage != null)
        {
            seekerSideImage.color = asHider
                ? new Color(0.14f, 0.16f, 0.20f, 0.9f)
                : HazardOrange;
        }
    }

    private void UpdateButtonLabels()
    {
        if (toolButtonTexts == null || architectManager == null) return;
        for (int i = 0; i < Tools.Length && i < toolButtonTexts.Length; i++)
        {
            if (toolButtonTexts[i] != null)
            {
                toolButtonTexts[i].text = GetToolButtonLabel(Tools[i], i + 1);
            }
        }
    }

    private string GetToolButtonLabel(ArchitectManager.Tool tool, int keyIndex)
    {
        int cost = architectManager != null ? architectManager.GetToolCost(tool) : 0;
        string name = GetThemedToolName(tool);
        string shortcut = keyIndex <= 7 ? $"[{keyIndex}]" : "";
        return $"{shortcut} {name}  ({cost})".TrimStart();
    }

    // Amendment 3 Themed Tool Label Mapping
    private string GetThemedToolName(ArchitectManager.Tool tool)
    {
        switch (tool)
        {
            case ArchitectManager.Tool.SpawnWall:     return "Barricade";
            case ArchitectManager.Tool.SpawnBox:      return "Debris";
            case ArchitectManager.Tool.SpawnPillar:   return "Collapsed Pillar";
            case ArchitectManager.Tool.SpawnRamp:     return "Tilted Slab";
            case ArchitectManager.Tool.SpawnRubble:   return "Rubble Mound";
            case ArchitectManager.Tool.Demolish:      return "Clear Debris";
            case ArchitectManager.Tool.PreLockHider:  return "Secure (Survivors)";
            case ArchitectManager.Tool.PreLockSeeker: return "Secure (Drones)";
            default:                                  return tool.ToString();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI Helper Methods
    // ─────────────────────────────────────────────────────────────────────────

    private static Font GetSafeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
        {
            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
            if (fonts != null && fonts.Length > 0) font = fonts[0];
        }
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        return font;
    }

    private GameObject CreatePanel(string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos, Vector2 size,
        Color bgColor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;
        return go;
    }

    private Text CreateText(Transform parent, string name, string content,
        int fontSize, Color color, TextAnchor anchor,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text t = go.AddComponent<Text>();
        t.text                 = content;
        t.font                 = GetSafeFont();
        t.fontSize             = fontSize;
        t.color                = color;
        t.alignment            = anchor;
        t.horizontalOverflow   = HorizontalWrapMode.Overflow;
        t.verticalOverflow     = VerticalWrapMode.Overflow;
        t.resizeTextForBestFit = false;
        t.raycastTarget        = false;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return t;
    }

    private GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 size, out Text labelText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.14f, 0.16f, 0.20f, 0.92f);

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = new Color(0.14f, 0.16f, 0.20f, 0.92f);
        cb.highlightedColor = new Color(0.24f, 0.32f, 0.44f, 1.0f);
        cb.pressedColor     = new Color(0.10f, 0.18f, 0.28f, 1.0f);
        btn.colors          = cb;
        btn.targetGraphic   = img;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;

        // Label child
        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);

        labelText = labelGo.AddComponent<Text>();
        labelText.text                 = label;
        labelText.font                 = GetSafeFont();
        labelText.fontSize             = 13;
        labelText.fontStyle            = FontStyle.Bold;
        labelText.color                = TextWhite;
        labelText.alignment            = TextAnchor.MiddleLeft;
        labelText.horizontalOverflow   = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow     = VerticalWrapMode.Overflow;
        labelText.resizeTextForBestFit = false;
        labelText.raycastTarget        = false;

        Outline labelOutline = labelGo.AddComponent<Outline>();
        labelOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        labelOutline.effectDistance = new Vector2(1f, -1f);

        RectTransform lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(14f, 0f);
        lrt.offsetMax = new Vector2(-10f, 0f);

        return go;
    }
}
