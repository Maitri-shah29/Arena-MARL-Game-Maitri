using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisasterThemeManager : MonoBehaviour
{
    public static DisasterThemeManager Instance { get; private set; }

    [Header("ResQ-MARL Disaster Theme Configuration")]
    [Tooltip("Master toggle for all disaster/rescue theming. When false, zero theming is applied.")]
    [SerializeField] public bool disasterTheme = true;

    private GameController gameController;
    private readonly List<SurvivorVisuals> survivorVisualsList = new List<SurvivorVisuals>();
    private readonly List<DroneVisuals> droneVisualsList = new List<DroneVisuals>();
    private FloorTheme floorTheme;
    private SafeZonePerimeter safeZonePerimeter;

    public bool IsThemeActive => disasterTheme && !Application.isBatchMode;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        gameController = GetComponent<GameController>();
        if (gameController == null)
        {
            gameController = FindObjectOfType<GameController>();
        }
    }

    private void Start()
    {
        if (!IsThemeActive)
        {
            return;
        }

        if (gameController != null)
        {
            gameController.OnEpisodeReset += HandleEpisodeReset;
        }

        StartCoroutine(InitializeThemeDeferred());
    }

    private IEnumerator InitializeThemeDeferred()
    {
        // Wait until the end of the first frame so MapGenerator has instantiated agents and platform
        yield return new WaitForEndOfFrame();

        if (!IsThemeActive) yield break;

        ApplyThemeToArena();
        ApplyThemeToAgents();
    }

    private void ApplyThemeToArena()
    {
        // 1. Floor Theme
        if (floorTheme == null)
        {
            floorTheme = gameObject.AddComponent<FloorTheme>();
        }
        floorTheme.ApplyTheme();

        // 2. Safe Base Perimeter
        SafeTriageZone triage = SafeTriageZone.Instance != null ? SafeTriageZone.Instance : FindObjectOfType<SafeTriageZone>();
        if (triage != null && triage.GetComponent<SafeZonePerimeter>() == null)
        {
            safeZonePerimeter = triage.gameObject.AddComponent<SafeZonePerimeter>();
        }
    }

    private void ApplyThemeToAgents()
    {
        if (gameController == null) return;

        // Apply DroneVisuals to all Seekers
        int seekerCount = gameController.SeekerCount;
        for (int i = 0; i < seekerCount; i++)
        {
            AgentActions seeker = gameController.GetSeeker(i);
            if (seeker != null)
            {
                DroneVisuals dv = seeker.GetComponent<DroneVisuals>();
                if (dv == null)
                {
                    dv = seeker.gameObject.AddComponent<DroneVisuals>();
                }
                if (!droneVisualsList.Contains(dv))
                {
                    droneVisualsList.Add(dv);
                }
            }
        }

        // Apply SurvivorVisuals to all Hiders
        int hiderCount = gameController.HiderCount;
        for (int i = 0; i < hiderCount; i++)
        {
            AgentActions hider = gameController.GetHider(i);
            if (hider != null)
            {
                SurvivorVisuals sv = hider.GetComponent<SurvivorVisuals>();
                if (sv == null)
                {
                    sv = hider.gameObject.AddComponent<SurvivorVisuals>();
                }
                if (!survivorVisualsList.Contains(sv))
                {
                    survivorVisualsList.Add(sv);
                }
            }
        }
    }

    private void HandleEpisodeReset()
    {
        if (!IsThemeActive) return;

        // Ensure newly active/re-enabled agents have visual components
        ApplyThemeToAgents();

        // Reset episode flags on all survivors (e.g. latched Located status)
        foreach (var sv in survivorVisualsList)
        {
            if (sv != null)
            {
                sv.ResetEpisode();
            }
        }
    }

    public int GetLocatedSurvivorCount()
    {
        int count = 0;
        foreach (var sv in survivorVisualsList)
        {
            if (sv != null && sv.WasLocated)
            {
                count++;
            }
        }
        return count;
    }

    private void OnDestroy()
    {
        if (gameController != null)
        {
            gameController.OnEpisodeReset -= HandleEpisodeReset;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
