using Unity.Barracuda;
using UnityEngine;

// Takes over one hider or one seeker after the architect round starts.
// The rest of that team, and the whole other team, keep using their checkpoints.
public class HumanPlayer : MonoBehaviour
{
    private GameController gameController;
    private ArchitectManager architect;
    private NNModel hiderModel;
    private NNModel seekerModel;
    private AgentActions possessed;
    private bool playingAsHider = true;
    private bool roundActive;

    public bool IsPlayingRound => roundActive;

    public void SetModels(NNModel hiderCheckpoint, NNModel seekerCheckpoint)
    {
        hiderModel = hiderCheckpoint;
        seekerModel = seekerCheckpoint;
    }

    public bool PlayingAsHider => playingAsHider;
    public bool HasControl => possessed != null;

    public void Setup(GameController controller, ArchitectManager architectManager, NNModel hidersCheckpoint, NNModel seekersCheckpoint)
    {
        if (architect != null)
        {
            architect.OnArchitectPhaseStarted -= OnArchitectPhaseStarted;
            architect.OnRoundStarted -= OnRoundStarted;
        }

        gameController = controller;
        architect = architectManager;
        hiderModel = hidersCheckpoint;
        seekerModel = seekersCheckpoint;

        if (architect != null)
        {
            architect.OnArchitectPhaseStarted += OnArchitectPhaseStarted;
            architect.OnRoundStarted += OnRoundStarted;
        }
        else
        {
            roundActive = true;
            TakeControl();
        }
    }

    private void OnDestroy()
    {
        if (architect != null)
        {
            architect.OnArchitectPhaseStarted -= OnArchitectPhaseStarted;
            architect.OnRoundStarted -= OnRoundStarted;
        }
    }

    public void SetSide(bool asHider)
    {
        if (playingAsHider == asHider && possessed != null)
        {
            return;
        }

        playingAsHider = asHider;
        if (roundActive)
        {
            ReleaseControl();
            TakeControl();
        }
    }

    private void OnArchitectPhaseStarted()
    {
        roundActive = false;
        ReleaseControl();
    }

    private void OnRoundStarted()
    {
        roundActive = true;
        TakeControl();
    }

    private void Update()
    {
        if (!roundActive || gameController == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            SetSide(!playingAsHider);
        }

        if (possessed == null || possessed.WasCaptured || !possessed.gameObject.activeInHierarchy)
        {
            if (possessed != null)
            {
                ReleaseControl();
                TakeControl();
            }
            return;
        }

        if (architect != null && architect.IsArchitectPhase)
        {
            return;
        }

        Camera view = Camera.main;
        if (view == null)
        {
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;
        if (Input.GetKey(KeyCode.W)) vertical += 1f;
        if (Input.GetKey(KeyCode.S)) vertical -= 1f;
        if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.D)) horizontal += 1f;

        Vector3 camForward = view.transform.forward;
        camForward.y = 0f;
        Vector3 camRight = view.transform.right;
        camRight.y = 0f;
        if (camForward.sqrMagnitude > 0.001f && camRight.sqrMagnitude > 0.001f)
        {
            camForward.Normalize();
            camRight.Normalize();
            Vector3 world = camRight * horizontal + camForward * vertical;
            Vector3 local = possessed.transform.InverseTransformDirection(world);
            possessed.ApplyMovement(new Vector2(local.x, local.z));
        }

        if (!Input.GetMouseButton(1))
        {
            possessed.ApplyRotation(Input.GetAxis("Mouse X"));
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            possessed.GrabBox();
        }
        if (!Input.GetKey(KeyCode.E) && possessed.IsHolding)
        {
            possessed.ReleaseBox();
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            possessed.LockBox(true);
        }
        if (Input.GetKeyDown(KeyCode.U))
        {
            possessed.LockBox(false);
        }
    }

    private void TakeControl()
    {
        ReleaseControl();
        possessed = FindTeammate(playingAsHider);
        if (possessed == null)
        {
            return;
        }

        if (possessed.HideAndSeekAgent != null)
        {
            possessed.HideAndSeekAgent.enabled = false;
        }
    }

    private void ReleaseControl()
    {
        if (possessed == null)
        {
            return;
        }

        AgentActions released = possessed;
        possessed = null;

        if (released.HideAndSeekAgent != null)
        {
            released.HideAndSeekAgent.enabled = true;
        }

        NNModel model = released.IsHiding ? hiderModel : seekerModel;
        released.SwitchToInference(model);
    }

    private AgentActions FindTeammate(bool hider)
    {
        var team = hider ? gameController.GetHiders() : gameController.GetSeekers();
        foreach (AgentActions agent in team)
        {
            if (agent != null && agent.gameObject.activeInHierarchy && !agent.WasCaptured)
            {
                return agent;
            }
        }
        return null;
    }

    private void OnGUI()
    {
        if (gameController == null)
        {
            return;
        }

        bool building = architect != null && architect.IsArchitectPhase;
        if (building)
        {
            return;
        }

        string side = playingAsHider ? "Trapped Survivor" : "Rescue Drone";
        string status = possessed != null ? "Operate: " + side : "Recon Camera Mode";
        string note = (!playingAsHider && possessed != null && !gameController.GracePeriodEnded)
            ? "Rescue drones deploy after reconnaissance"
            : "Tab switches operator";

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        title.normal.textColor = Color.white;
        GUIStyle line = new GUIStyle(title) { fontSize = 14 };

        GUI.Box(new Rect(8f, 8f, 320f, 150f), "");
        GUI.Label(new Rect(16f, 12f, 290f, 22f), "DISASTER RESCUE MISSION", title);
        GUI.Label(new Rect(16f, 34f, 290f, 20f), status, line);
        GUI.Label(new Rect(16f, 54f, 290f, 20f),
            "Survivors Located: " + gameController.CapturedHiders + " / " + gameController.HiderCount, line);
        GUI.Label(new Rect(16f, 74f, 290f, 20f),
            "Evacuated to Safe Base: " + gameController.EvacuatedCount + " / " + gameController.HiderCount, line);
        string footer = gameController.RoundSettled && !string.IsNullOrEmpty(gameController.LastRoundSummary)
            ? gameController.LastRoundSummary
            : note;
        GUI.Label(new Rect(16f, 98f, 290f, 45f), footer, line);
    }
}
