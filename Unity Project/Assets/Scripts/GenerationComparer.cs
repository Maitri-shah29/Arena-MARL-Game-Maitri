using System.Collections.Generic;
using System.Text;
using Unity.Barracuda;
using UnityEngine;

// Compares hider checkpoints against the same seeker baseline each round.
public class GenerationComparer : MonoBehaviour
{
    private class Tally
    {
        public int rounds;
        public int hiderWins;
        public float hiddenSum;
        public int capturedSum;
        public int hiderSum;
    }

    public static GenerationComparer Instance { get; private set; }

    private GenerationCatalog catalog;
    private List<AgentActions> hiders = new List<AgentActions>();
    private Tally[] tallies = new Tally[0];
    private int index;
    private ArchitectManager architect;
    private GameController controller;
    private Vector2 resultsScroll;
    private NNModel seekerBaseline;
    private bool detailsOpen;

    private bool CanChangeGeneration =>
        (architect != null && architect.IsArchitectPhase) ||
        (controller != null && controller.RoundSettled);

    public NNModel CurrentModel =>
        catalog != null && catalog.generations != null && catalog.generations.Length > 0
            ? catalog.generations[index].model
            : null;

    public string CurrentLabel =>
        catalog != null && catalog.generations != null && catalog.generations.Length > 0
            ? catalog.generations[index].label
            : "No generations";

    private void Awake()
    {
        Instance = this;
        architect = GetComponent<ArchitectManager>();
        controller = GetComponent<GameController>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Setup(List<AgentActions> hiderAgents, NNModel currentHider, NNModel currentSeeker)
    {
        hiders = hiderAgents;
        seekerBaseline = currentSeeker;
        catalog = Resources.Load<GenerationCatalog>("GenerationCatalog");
        if (catalog == null || catalog.generations == null || catalog.generations.Length == 0)
        {
            Debug.LogWarning("Generation catalog is missing. Checkpoints stay on the scene defaults.");
            return;
        }

        tallies = new Tally[catalog.generations.Length];
        for (int i = 0; i < tallies.Length; i++)
        {
            tallies[i] = new Tally();
        }

        index = 0;
        for (int i = 0; i < catalog.generations.Length; i++)
        {
            if (catalog.generations[i].model == currentHider)
            {
                index = i;
                break;
            }
        }

        Apply();
    }

    public void Next()
    {
        Step(1);
    }

    public void Previous()
    {
        Step(-1);
    }

    public void RecordRound(bool hidersWon, float timeHidden, int captured, int hiderCount)
    {
        if (tallies == null || index < 0 || index >= tallies.Length)
        {
            return;
        }

        Tally tally = tallies[index];
        tally.rounds++;
        if (hidersWon)
        {
            tally.hiderWins++;
        }
        tally.hiddenSum += timeHidden;
        tally.capturedSum += captured;
        tally.hiderSum += hiderCount;
    }

    public string ComparisonText
    {
        get
        {
            if (catalog == null || catalog.generations == null || catalog.generations.Length == 0)
            {
                return "No generation catalog";
            }

            StringBuilder text = new StringBuilder();
            text.Append("Results");
            bool any = false;
            for (int i = 0; i < catalog.generations.Length; i++)
            {
                Tally tally = tallies[i];
                if (tally.rounds == 0)
                {
                    continue;
                }

                any = true;
                float hidden = 100f * tally.hiddenSum / tally.rounds;
                float captured = tally.hiderSum > 0
                    ? 100f * tally.capturedSum / tally.hiderSum
                    : 0f;
                text.Append("\n").Append(catalog.generations[i].label);
                text.Append(": ").Append(tally.rounds).Append(" rounds, ");
                text.Append(tally.hiderWins).Append(" survivor evacuations");
                text.Append("\nHidden ").Append(hidden.ToString("0")).Append("%");
                text.Append(" | Rescued ").Append(captured.ToString("0")).Append("%");
            }

            if (!any)
            {
                text.Append("\nNo rounds yet");
            }
            return text.ToString();
        }
    }

    private void Step(int direction)
    {
        if (!CanChangeGeneration || catalog == null || catalog.generations == null || catalog.generations.Length == 0)
        {
            return;
        }

        int count = catalog.generations.Length;
        index = (index + direction + count) % count;
        Apply();
    }

    private void Update()
    {
        if (!CanChangeGeneration)
        {
            detailsOpen = false;
        }
        else if (Input.GetKeyDown(KeyCode.G))
        {
            detailsOpen = !detailsOpen;
        }
    }

    private void OnGUI()
    {
        // Keep the arena clear while a round is running. Model changes are only
        // available during the build phase or while the result is displayed.
        if (!CanChangeGeneration)
        {
            return;
        }

        float tabWidth = Mathf.Min(150f, Screen.width - 16f);
        if (tabWidth <= 0f)
        {
            return;
        }

        Rect tab = new Rect(Screen.width - tabWidth - 8f, Screen.height - 34f, tabWidth, 26f);
        if (GUI.Button(tab, detailsOpen ? "Close AI [G]" : "Compare AI [G]"))
        {
            detailsOpen = !detailsOpen;
        }

        if (!detailsOpen)
        {
            return;
        }

        float width = Mathf.Min(300f, Screen.width - 16f);
        float height = Mathf.Min(270f, Screen.height - 50f);
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        Rect panel = new Rect(Screen.width - width - 8f, Screen.height - height - 42f,
            width, height);
        Color previousColor = GUI.color;
        GUI.color = new Color(0.03f, 0.04f, 0.08f, 0.96f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUILayout.BeginArea(panel, GUI.skin.box);

        GUIStyle title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 16 };
        GUIStyle result = new GUIStyle(GUI.skin.label) { wordWrap = true };
        GUILayout.Label("COMPARE SWARM RESCUE AI", title);
        GUILayout.Label("Optional: compare saved AI models. Playing does not train them.", result);
        GUILayout.Label(CurrentLabel, title);
        GUILayout.Label("Seekers keep the same AI for a fair comparison.", result);

        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && CanChangeGeneration;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Previous")) Previous();
        if (GUILayout.Button("Next")) Next();
        GUILayout.EndHorizontal();
        GUI.enabled = wasEnabled;

        if (!CanChangeGeneration)
        {
            GUILayout.Label("Choose a model between rounds.");
        }

        resultsScroll = GUILayout.BeginScrollView(resultsScroll, GUILayout.ExpandHeight(true));
        GUILayout.Label(ComparisonText, result);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void Apply()
    {
        NNModel model = CurrentModel;
        if (model == null)
        {
            return;
        }

        ApplyTeam(hiders, model);
        FindObjectOfType<HumanPlayer>()?.SetModels(model, seekerBaseline);
    }

    private static void ApplyTeam(List<AgentActions> team, NNModel model)
    {
        if (team == null)
        {
            return;
        }

        foreach (AgentActions agent in team)
        {
            if (agent == null || agent.HideAndSeekAgent == null || !agent.HideAndSeekAgent.enabled)
            {
                continue;
            }
            agent.SwitchToInference(model);
        }
    }
}
