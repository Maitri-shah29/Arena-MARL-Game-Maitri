using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// ArchitectUI – minimal uGUI HUD active only during the Architect phase.
//
// Attach to a Canvas GameObject in the scene.
// The script builds its own UI elements at runtime (no prefab needed).
//
// Keyboard shortcuts:
//   1-4  → select tool (Wall, Box, Demolish, Pre-Lock)
//   R    → rotate wall
//   Z    → undo
//   Space/Enter → start round

[RequireComponent(typeof(Canvas))]
public class ArchitectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ArchitectManager architectManager = null;

    // ─── Runtime UI elements ──────────────────────────────────────────────────
    private Canvas   canvas;
    private Text     energyLabel;
    private Text     feedbackLabel;
    private Button[] toolButtons;
    private Image[]  toolHighlights;
    private Text[]   toolButtonTexts;
    private Button   startRoundButton;
    private Image    hiderSideImage;
    private Image    seekerSideImage;

    private static readonly ArchitectManager.Tool[] toolEnum = new ArchitectManager.Tool[]
    {
        ArchitectManager.Tool.SpawnWall,
        ArchitectManager.Tool.SpawnBox,
        ArchitectManager.Tool.Demolish,
        ArchitectManager.Tool.PreLockHider
    };

    private ArchitectManager.Tool lastTool = (ArchitectManager.Tool)(-1);
    private int                   lastEnergy = -1;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (architectManager == null)
            architectManager = FindObjectOfType<ArchitectManager>();

        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        // Ensure CanvasScaler + GraphicRaycaster exist
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        float fit = Mathf.Min(Screen.width / 1100f, Screen.height / 700f);
        scaler.scaleFactor = Mathf.Clamp(fit, 0.7f, 1f);

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        // Ensure EventSystem exists in scene
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        BuildUI();
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (architectManager == null) return;
        architectManager.OnArchitectPhaseStarted += OnPhaseStarted;
        architectManager.OnRoundStarted          += OnRoundStarted;
        architectManager.OnBudgetChanged         += OnBudgetChanged;
    }

    private void OnDisable()
    {
        if (architectManager == null) return;
        architectManager.OnArchitectPhaseStarted -= OnPhaseStarted;
        architectManager.OnRoundStarted          -= OnRoundStarted;
        architectManager.OnBudgetChanged         -= OnBudgetChanged;
    }

    private void Update()
    {
        if (architectManager != null && architectManager.IsArchitectPhase)
        {
            if (feedbackLabel != null)
                feedbackLabel.text = architectManager.FeedbackMessage;

            if (architectManager.CurrentTool != lastTool || architectManager.EnergyRemaining != lastEnergy)
            {
                lastTool = architectManager.CurrentTool;
                lastEnergy = architectManager.EnergyRemaining;
                UpdateTopBar(lastEnergy);
                RefreshToolHighlights();
            }

            RefreshSideHighlight();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Event handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnPhaseStarted()
    {
        SetVisible(true);
        UpdateButtonLabels();
        int energy = architectManager != null ? architectManager.EnergyRemaining : 100;
        lastEnergy = energy;
        lastTool   = architectManager != null ? architectManager.CurrentTool : ArchitectManager.Tool.SpawnWall;
        UpdateTopBar(energy);
        RefreshToolHighlights();
    }

    private void OnRoundStarted() => SetVisible(false);

    private void OnBudgetChanged(int remaining)
    {
        lastEnergy = remaining;
        UpdateTopBar(remaining);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Cost & Reflection Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private int GetCostWall()     => GetManagerIntField("costSpawnWall", 40);
    private int GetCostBox()      => GetManagerIntField("costSpawnBox", 25);
    private int GetCostDemolish() => GetManagerIntField("costDemolish", 35);
    private int GetCostPreLock()  => GetManagerIntField("costPreLock", 20);
    private int GetMaxEnergy()    => GetManagerIntField("energyBudgetPerRound", 100);

    private int GetManagerIntField(string fieldName, int fallback)
    {
        if (architectManager == null) return fallback;
        try
        {
            FieldInfo field = typeof(ArchitectManager).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
                return (int)field.GetValue(architectManager);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArchitectUI] Failed to read {fieldName}: {ex.Message}");
        }
        return fallback;
    }

    private string GetToolDisplayName(ArchitectManager.Tool tool)
    {
        switch (tool)
        {
            case ArchitectManager.Tool.SpawnWall:     return "Wall";
            case ArchitectManager.Tool.SpawnBox:      return "Box";
            case ArchitectManager.Tool.Demolish:      return "Demolish";
            case ArchitectManager.Tool.PreLockHider:  return "Pre-Lock";
            case ArchitectManager.Tool.PreLockSeeker: return "Pre-Lock";
            default:                                  return tool.ToString();
        }
    }

    private void UpdateTopBar(int currentEnergy)
    {
        if (energyLabel == null) return;
        int maxEnergy = GetMaxEnergy();
        string toolName = architectManager != null
            ? GetToolDisplayName(architectManager.CurrentTool)
            : "Wall";

        energyLabel.text = $"{currentEnergy}/{maxEnergy}  {toolName}";
    }

    private void UpdateButtonLabels()
    {
        if (toolButtonTexts == null || toolButtonTexts.Length < 5) return;
        toolButtonTexts[0].text = $"1  Wall ({GetCostWall()})";
        toolButtonTexts[1].text = $"2  Box ({GetCostBox()})";
        toolButtonTexts[2].text = $"3  Demolish ({GetCostDemolish()})";
        toolButtonTexts[3].text = $"4  Pre-Lock ({GetCostPreLock()})";
        toolButtonTexts[4].text = "Start Round  [Space]";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI construction (runtime uGUI)
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        GameObject toolbar = CreatePanel("Toolbar",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(8f, -8f), new Vector2(210f, 360f));

        energyLabel = CreateTextChild(toolbar.transform, "EnergyLabel",
            $"Energy {GetMaxEnergy()}/{GetMaxEnergy()}",
            16, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.88f), new Vector2(1f, 1f),
            new Vector2(6f, 0f), new Vector2(-6f, -2f));
        energyLabel.fontStyle = FontStyle.Bold;

        feedbackLabel = CreateTextChild(toolbar.transform, "FeedbackLabel",
            "Click the floor to place",
            13, new Color(1f, 0.95f, 0.7f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0f, 0.74f), new Vector2(1f, 0.88f),
            new Vector2(6f, 0f), new Vector2(-6f, 0f));

        toolButtons     = new Button[5];
        toolHighlights  = new Image[5];
        toolButtonTexts = new Text[5];

        string[] initialLabels = new string[]
        {
            $"1  Wall ({GetCostWall()})",
            $"2  Box ({GetCostBox()})",
            $"3  Demolish ({GetCostDemolish()})",
            $"4  Pre-Lock ({GetCostPreLock()})",
            "Start  [Space]"
        };

        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            float yOffset = 28f - i * 46f;

            GameObject btnGo = CreateButton(toolbar.transform, $"Btn_{i}", initialLabels[i],
                new Vector2(0f, yOffset), new Vector2(190f, 40f), out Text btnText);

            toolButtons[i]     = btnGo.GetComponent<Button>();
            toolHighlights[i]  = btnGo.GetComponent<Image>();
            toolButtonTexts[i] = btnText;

            if (i < 4)
            {
                toolButtons[i].onClick.AddListener(() =>
                {
                    if (architectManager != null)
                        architectManager.SetTool(toolEnum[idx]);
                });
            }
            else
            {
                // 5th button: Start Round [Space]
                startRoundButton = toolButtons[i];
                ColorBlock cb = startRoundButton.colors;
                cb.normalColor      = new Color(0.12f, 0.55f, 0.25f, 0.95f);
                cb.highlightedColor = new Color(0.18f, 0.75f, 0.35f, 1f);
                cb.pressedColor     = new Color(0.1f, 0.45f, 0.2f, 1f);
                startRoundButton.colors = cb;
                toolHighlights[i].color = cb.normalColor;

                startRoundButton.onClick.AddListener(() =>
                {
                    architectManager?.EndArchitectPhase();
                });
            }
        }

        // ── Bottom hint bar ──────────────────────────────────────────────────
        GameObject hintPanel = CreatePanel("BottomHintPanel",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 8f), new Vector2(560f, 32f));

        GameObject sidePanel = CreatePanel("SidePanel",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-8f, -8f), new Vector2(170f, 168f));

        CreateTextChild(sidePanel.transform, "SideLabel",
            "Play as",
            16, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.72f), new Vector2(1f, 1f),
            new Vector2(6f, 0f), new Vector2(-6f, -4f)).fontStyle = FontStyle.Bold;

        GameObject hiderButton = CreateButton(sidePanel.transform, "PlayHider", "Hider",
            new Vector2(0f, 10f), new Vector2(150f, 36f), out Text hiderLabel);
        hiderLabel.text = "Hider";
        hiderSideImage = hiderButton.GetComponent<Image>();
        hiderButton.GetComponent<Button>().onClick.AddListener(() => FindObjectOfType<HumanPlayer>()?.SetSide(true));

        GameObject seekerButton = CreateButton(sidePanel.transform, "PlaySeeker", "Seeker",
            new Vector2(0f, -34f), new Vector2(150f, 36f), out Text seekerLabel);
        seekerLabel.text = "Seeker";
        seekerSideImage = seekerButton.GetComponent<Image>();
        seekerButton.GetComponent<Button>().onClick.AddListener(() => FindObjectOfType<HumanPlayer>()?.SetSide(false));

        CreateTextChild(hintPanel.transform, "HintLabel",
            "Click the floor    R rotate    Z undo    Space starts",
            14, Color.white, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one,
            new Vector2(10f, 0f), new Vector2(-10f, 0f)).fontStyle = FontStyle.Bold;
    }

    private void RefreshToolHighlights()
    {
        if (architectManager == null || toolHighlights == null) return;

        ArchitectManager.Tool current = architectManager.CurrentTool;

        for (int i = 0; i < 4; i++)
        {
            if (toolHighlights[i] == null) continue;
            bool active = (i < toolEnum.Length &&
                (toolEnum[i] == current ||
                 (toolEnum[i] == ArchitectManager.Tool.PreLockHider && current == ArchitectManager.Tool.PreLockSeeker)));

            toolHighlights[i].color = active
                ? new Color(0.2f, 0.8f, 1f, 0.95f)     // Selected tool – bright cyan
                : new Color(0.15f, 0.17f, 0.22f, 0.9f); // Normal – sleek dark panel
        }
    }

    private void RefreshSideHighlight()
    {
        HumanPlayer player = FindObjectOfType<HumanPlayer>();
        bool asHider = player == null || player.PlayingAsHider;
        if (hiderSideImage != null)
        {
            hiderSideImage.color = asHider
                ? new Color(0.15f, 0.55f, 0.95f, 0.95f)
                : new Color(0.15f, 0.17f, 0.22f, 0.9f);
        }
        if (seekerSideImage != null)
        {
            seekerSideImage.color = asHider
                ? new Color(0.15f, 0.17f, 0.22f, 0.9f)
                : new Color(0.85f, 0.25f, 0.25f, 0.95f);
        }
    }

    private void SetVisible(bool v)
    {
        canvas.enabled = v;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI builder helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Font GetSafeFont()
    {
        // Unity 2021.3+ built-in font is LegacyRuntime.ttf; fall back to Arial, project fonts, or OS font.
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
        {
            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
            if (fonts != null && fonts.Length > 0)
                font = fonts[0];
        }
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return font;
    }

    private GameObject CreatePanel(string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.05f, 0.06f, 0.1f, 0.88f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;
        return go;
    }

    private Text CreateTextChild(Transform parent, string name, string content,
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
        t.horizontalOverflow   = HorizontalWrapMode.Wrap;
        t.verticalOverflow     = VerticalWrapMode.Overflow;
        t.resizeTextForBestFit = false;
        t.raycastTarget        = false;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

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
        img.color = new Color(0.15f, 0.17f, 0.22f, 0.9f);

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = new Color(0.15f, 0.17f, 0.22f, 0.9f);
        cb.highlightedColor = new Color(0.28f, 0.45f, 0.65f, 1f);
        cb.pressedColor     = new Color(0.1f, 0.25f, 0.45f, 1f);
        btn.colors          = cb;
        btn.targetGraphic   = img;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;

        // Label child
        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);

        labelText = labelGo.AddComponent<Text>();
        labelText.text                 = label;
        labelText.font                 = GetSafeFont();
        labelText.fontSize             = 16;
        labelText.fontStyle            = FontStyle.Bold;
        labelText.color                = Color.white;
        labelText.alignment            = TextAnchor.MiddleCenter;
        labelText.horizontalOverflow   = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow     = VerticalWrapMode.Overflow;
        labelText.resizeTextForBestFit = false;
        labelText.raycastTarget        = false;
        Outline labelOutline = labelGo.AddComponent<Outline>();
        labelOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        labelOutline.effectDistance = new Vector2(1.5f, -1.5f);

        RectTransform lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        // 16px left padding so text never crosses or touches the button's left edge
        lrt.offsetMin = new Vector2(16f, 2f);
        lrt.offsetMax = new Vector2(-6f, -2f);

        return go;
    }
}
