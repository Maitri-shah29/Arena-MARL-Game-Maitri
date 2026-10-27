using UnityEngine;

// SurvivorRescueState - Manages the 3-state lifecycle of earthquake survivors:
// Trapped -> BeingEscorted -> Rescued
[RequireComponent(typeof(AgentActions))]
public class SurvivorRescueState : MonoBehaviour
{
    public enum State { Trapped, BeingEscorted, Rescued }
    public State CurrentState { get; private set; } = State.Trapped;

    [SerializeField] private float followSpeed = 2.5f;
    [SerializeField] private float stoppingDistance = 1.6f;

    private AgentActions agentActions;
    private Transform assignedRescuer;
    private Transform triageZoneTransform;
    private TextMesh statusBadge;

    public Transform AssignedRescuer => assignedRescuer;
    public TextMesh StatusBadge => statusBadge;

    private void Awake()
    {
        agentActions = GetComponent<AgentActions>();
        CreateStatusBadge();
    }

    private void Start()
    {
        if (SafeTriageZone.Instance != null)
        {
            triageZoneTransform = SafeTriageZone.Instance.transform;
        }
    }

    public void OnLocatedByDrone(Transform droneTransform)
    {
        if (CurrentState != State.Trapped) return;

        CurrentState = State.BeingEscorted;
        assignedRescuer = droneTransform;
        UpdateBadge("[EXTRACTING]", Color.yellow);
    }

    public void MarkAsSafelyRescued()
    {
        CurrentState = State.Rescued;
        agentActions.WasCaptured = true;
        UpdateBadge("[SAFE]", Color.green);
    }

    public void ResetState()
    {
        CurrentState = State.Trapped;
        assignedRescuer = null;
        UpdateBadge("[TRAPPED]", new Color(0f, 0.85f, 1f));
    }

    private void FixedUpdate()
    {
        if (CurrentState == State.BeingEscorted)
        {
            Vector3 targetPos = triageZoneTransform != null
                ? triageZoneTransform.position
                : (assignedRescuer != null ? assignedRescuer.position : transform.position);

            Vector3 diff = targetPos - transform.position;
            diff.y = 0f;

            if (diff.magnitude > stoppingDistance)
            {
                Vector3 moveDir = diff.normalized;
                agentActions.ApplyMovement(new Vector2(moveDir.x, moveDir.z) * followSpeed * Time.fixedDeltaTime);
            }
        }
    }

    private void CreateStatusBadge()
    {
        GameObject badgeObj = new GameObject("SurvivorBadge");
        badgeObj.transform.SetParent(transform, false);
        badgeObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        statusBadge = badgeObj.AddComponent<TextMesh>();
        statusBadge.fontSize = 26;
        statusBadge.characterSize = 0.08f;
        statusBadge.alignment = TextAlignment.Center;
        statusBadge.anchor = TextAnchor.MiddleCenter;
        UpdateBadge("[TRAPPED]", new Color(0f, 0.85f, 1f));
    }

    private void UpdateBadge(string text, Color color)
    {
        if (statusBadge != null)
        {
            statusBadge.text = text;
            statusBadge.color = color;
        }
    }

    private void LateUpdate()
    {
        if (statusBadge != null && Camera.main != null)
        {
            statusBadge.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
