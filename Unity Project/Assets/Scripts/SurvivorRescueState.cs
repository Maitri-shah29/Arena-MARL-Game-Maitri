using UnityEngine;

[RequireComponent(typeof(AgentActions))]
public class SurvivorRescueState : MonoBehaviour
{
    public enum State { Trapped, BeingEscorted, Rescued }
    public State CurrentState { get; private set; } = State.Trapped;

    [SerializeField] private float followDistance = 1.1f;
    [SerializeField] private float regroupDistance = 2.5f;

    private AgentActions survivor;
    private AgentActions rescuer;
    private SafeTriageZone triageZone;
    private bool waitingForSurvivor;
    private Rigidbody body;
    private bool originalKinematic;
    private TextMesh statusBadge;
    private Renderer[] renderers;
    private Color[] originalColors;
    private int[] colorProperties;

    public Transform AssignedRescuer => rescuer != null ? rescuer.transform : null;
    public TextMesh StatusBadge => statusBadge;

    private void Awake()
    {
        survivor = GetComponent<AgentActions>();
        body = GetComponent<Rigidbody>();
        originalKinematic = body != null && body.isKinematic;
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        colorProperties = new int[renderers.Length];
        RefreshOriginalColors();
        CreateStatusBadge();
    }

    public void RefreshOriginalColors()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].material;
            colorProperties[i] = material.HasProperty("_BaseColor")
                ? Shader.PropertyToID("_BaseColor")
                : material.HasProperty("_Color") ? Shader.PropertyToID("_Color") : -1;
            if (colorProperties[i] != -1)
                originalColors[i] = material.GetColor(colorProperties[i]);
        }
    }

    public bool OnLocatedByDrone(AgentActions drone, SafeTriageZone zone)
    {
        if (CurrentState != State.Trapped || drone == null || zone == null) return false;
        CurrentState = State.BeingEscorted;
        rescuer = drone;
        triageZone = zone;
        waitingForSurvivor = false;
        survivor.MovementLocked = false;
        rescuer.SetRescueDestination(zone.transform.position, 0.5f);
        survivor.SetRescueDestination(rescuer.transform.position, followDistance);
        UpdateBadge("[EXTRACTING]", Color.yellow);
        return true;
    }

    public bool MarkAsSafelyRescued()
    {
        if (CurrentState != State.BeingEscorted) return false;
        CurrentState = State.Rescued;
        survivor.ClearRescueDestination();
        if (rescuer != null) rescuer.ClearRescueDestination();
        rescuer = null;
        triageZone = null;
        survivor.WasCaptured = true;
        if (body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
        for (int i = 0; i < renderers.Length; i++)
            if (colorProperties[i] != -1)
                renderers[i].material.SetColor(colorProperties[i], Color.green);
        UpdateBadge("[SAFE]", Color.green);
        return true;
    }

    public void ResetState()
    {
        if (rescuer != null) rescuer.ClearRescueDestination();
        rescuer = null;
        triageZone = null;
        waitingForSurvivor = false;
        survivor.ClearRescueDestination();
        survivor.WasCaptured = false;
        survivor.MovementLocked = true;
        if (body != null) body.isKinematic = originalKinematic;
        for (int i = 0; i < renderers.Length; i++)
            if (colorProperties[i] != -1)
                renderers[i].material.SetColor(colorProperties[i], originalColors[i]);
        CurrentState = State.Trapped;
        UpdateBadge("[TRAPPED]", new Color(0f, 0.85f, 1f));
    }

    private void FixedUpdate()
    {
        if (CurrentState != State.BeingEscorted) return;
        if (rescuer == null || triageZone == null)
        {
            ResetState();
            return;
        }
        float separation = Vector3.Distance(rescuer.transform.position, transform.position);
        if (waitingForSurvivor)
        {
            if (separation <= followDistance + 0.4f)
            {
                waitingForSurvivor = false;
                rescuer.SetRescueDestination(triageZone.transform.position, 0.5f);
            }
        }
        else if (separation >= regroupDistance)
        {
            waitingForSurvivor = true;
            rescuer.SetRescueDestination(rescuer.transform.position, 0f);
        }
        survivor.SetRescueDestination(rescuer.transform.position, followDistance);
    }

    private void CreateStatusBadge()
    {
        GameObject badge = new GameObject("SurvivorBadge");
        badge.transform.SetParent(transform, false);
        badge.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        statusBadge = badge.AddComponent<TextMesh>();
        statusBadge.fontSize = 26;
        statusBadge.characterSize = 0.08f;
        statusBadge.alignment = TextAlignment.Center;
        statusBadge.anchor = TextAnchor.MiddleCenter;
        UpdateBadge("[TRAPPED]", new Color(0f, 0.85f, 1f));
    }

    private void UpdateBadge(string label, Color color)
    {
        statusBadge.text = label;
        statusBadge.color = color;
    }

    private void LateUpdate()
    {
        if (statusBadge != null && Camera.main != null)
            statusBadge.transform.rotation = Camera.main.transform.rotation;
    }
}
