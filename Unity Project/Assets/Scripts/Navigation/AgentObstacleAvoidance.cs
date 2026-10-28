using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AgentObstacleAvoidance: High-performance local obstacle avoidance, multi-agent de-confliction,
/// and anti-stuck watchdog for ResQ-MARL rescue drones and survivors.
/// Prevents agents from colliding into walls/debris or mutually blocking each other in chokepoints.
/// </summary>
[RequireComponent(typeof(AgentActions))]
public class AgentObstacleAvoidance : MonoBehaviour
{
    [Header("Whisker Sensor Settings")]
    [SerializeField] private float forwardWhiskerDistance = 2.4f;
    [SerializeField] private float sideWhiskerDistance = 1.6f;
    [SerializeField] private float whiskerHeight = 0.35f;

    [Header("Multi-Agent Avoidance")]
    [SerializeField] private float agentRepulsionRadius = 2.0f;
    [SerializeField] private float agentRepulsionWeight = 1.4f;

    [Header("Anti-Stuck Watchdog")]
    [SerializeField] private float stuckDetectionTime = 0.55f;
    [SerializeField] private float stuckDisplacementThreshold = 0.08f;
    [SerializeField] private float unstuckDuration = 0.45f;

    private AgentActions agentActions;
    private Rigidbody rb;
    private GameController gameController;

    // Waypoint tracking
    private readonly List<Vector3> waypoints = new List<Vector3>();
    private int currentWaypointIdx = 0;
    private Vector3 lastTargetPos = Vector3.negativeInfinity;
    private float repathCooldown = 0f;

    // Anti-stuck state
    private Vector3 lastRecordedPosition;
    private float positionSampleTimer = 0f;
    private float timeStuck = 0f;
    private bool isUnsticking = false;
    private float unstuckTimer = 0f;
    private Vector3 unstuckDirection = Vector3.back;
    private float unstuckTurnBias = 1f;

    private void Awake()
    {
        agentActions = GetComponent<AgentActions>();
        rb = GetComponent<Rigidbody>();
        lastRecordedPosition = transform.position;
    }

    private void Start()
    {
        gameController = FindObjectOfType<GameController>();
    }

    /// <summary>
    /// Resets navigation state upon round reset.
    /// </summary>
    public void ResetState()
    {
        waypoints.Clear();
        currentWaypointIdx = 0;
        lastTargetPos = Vector3.negativeInfinity;
        repathCooldown = 0f;
        timeStuck = 0f;
        isUnsticking = false;
        unstuckTimer = 0f;
        lastRecordedPosition = transform.position;
    }

    /// <summary>
    /// Computes steering towards target destination while routing through A* waypoints,
    /// avoiding obstacles via whiskers, repelling other seekers, and recovering from stuck states.
    /// </summary>
    public bool ComputeRescueSteering(Vector3 destination, float stopDistance, float rotationSpeed, out Vector2 moveInput, out float rotInput)
    {
        moveInput = Vector2.zero;
        rotInput = 0f;

        Vector3 myPos = transform.position;
        Vector3 toDestination = destination - myPos;
        toDestination.y = 0f;

        if (toDestination.sqrMagnitude <= stopDistance * stopDistance)
        {
            waypoints.Clear();
            return false;
        }

        // 1. Anti-Stuck Active Recovery
        UpdateStuckWatchdog(true);
        if (isUnsticking)
        {
            Vector3 localUnstuck = transform.InverseTransformDirection(unstuckDirection);
            moveInput = new Vector2(localUnstuck.x, localUnstuck.z).normalized;
            rotInput = unstuckTurnBias;
            return true;
        }

        // 2. Waypoint Path Management
        UpdateWaypoints(destination);

        Vector3 nextTarget = destination;
        if (waypoints.Count > 0 && currentWaypointIdx < waypoints.Count)
        {
            nextTarget = waypoints[currentWaypointIdx];
            Vector3 toWp = nextTarget - myPos;
            toWp.y = 0f;

            // If arrived near waypoint, advance to next
            if (toWp.sqrMagnitude <= 0.7f * 0.7f)
            {
                currentWaypointIdx++;
                if (currentWaypointIdx < waypoints.Count)
                {
                    nextTarget = waypoints[currentWaypointIdx];
                }
                else
                {
                    nextTarget = destination;
                }
            }
        }

        Vector3 desiredDir = (nextTarget - myPos);
        desiredDir.y = 0f;
        if (desiredDir.sqrMagnitude < 0.001f) desiredDir = transform.forward;
        else desiredDir.Normalize();

        // 3. Multi-Agent Seeker Repulsion
        Vector3 agentAvoidance = ComputeAgentRepulsion();

        // 4. Whisker Obstacle Avoidance (Context Steering)
        Vector3 obstacleAvoidance = ComputeWhiskerAvoidance(desiredDir);

        // 5. Final Steer Blending
        Vector3 finalMoveDir = desiredDir;
        if (obstacleAvoidance.sqrMagnitude > 0.01f)
        {
            finalMoveDir = (desiredDir * 0.5f + obstacleAvoidance * 1.5f).normalized;
        }
        if (agentAvoidance.sqrMagnitude > 0.01f)
        {
            finalMoveDir = (finalMoveDir + agentAvoidance * agentRepulsionWeight).normalized;
        }

        Vector3 localDir = transform.InverseTransformDirection(finalMoveDir);
        moveInput = new Vector2(localDir.x, localDir.z);

        float angle = Vector3.SignedAngle(transform.forward, finalMoveDir, Vector3.up);
        rotInput = Mathf.Clamp(angle / (rotationSpeed * Time.fixedDeltaTime), -1f, 1f);

        return true;
    }

    /// <summary>
    /// Filters policy-driven or manual movement vectors to avert obstacle collision and multi-agent jams.
    /// </summary>
    public void FilterPolicyMovement(ref Vector2 moveInput, ref float rotInput, float rotationSpeed)
    {
        if (moveInput.sqrMagnitude < 0.01f) return;

        // Anti-stuck watchdog during wandering
        UpdateStuckWatchdog(true);
        if (isUnsticking)
        {
            Vector3 localUnstuck = transform.InverseTransformDirection(unstuckDirection);
            moveInput = new Vector2(localUnstuck.x, localUnstuck.z).normalized;
            rotInput = unstuckTurnBias;
            return;
        }

        Vector3 worldMove = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        Vector3 agentAvoid = ComputeAgentRepulsion();
        Vector3 obsAvoid = ComputeWhiskerAvoidance(worldMove);

        if (obsAvoid.sqrMagnitude > 0.01f || agentAvoid.sqrMagnitude > 0.01f)
        {
            Vector3 adjustedWorld = (worldMove + obsAvoid * 1.4f + agentAvoid * agentRepulsionWeight).normalized;
            Vector3 local = transform.InverseTransformDirection(adjustedWorld);
            moveInput = new Vector2(local.x, local.z);

            float angle = Vector3.SignedAngle(transform.forward, adjustedWorld, Vector3.up);
            rotInput = Mathf.Clamp(rotInput + angle / (rotationSpeed * Time.fixedDeltaTime), -1f, 1f);
        }
    }

    private void UpdateWaypoints(Vector3 destination)
    {
        repathCooldown -= Time.fixedDeltaTime;
        bool targetChanged = Vector3.Distance(destination, lastTargetPos) > 1.2f;

        if (targetChanged || (repathCooldown <= 0f && waypoints.Count == 0))
        {
            lastTargetPos = destination;
            repathCooldown = 1.0f; // Repath once per second to reduce overhead

            if (RescuePathfinder.Instance != null)
            {
                waypoints.Clear();
                List<Vector3> path = RescuePathfinder.Instance.FindPath(transform.position, destination);
                if (path != null)
                {
                    waypoints.AddRange(path);
                }
                currentWaypointIdx = 0;
            }
        }
    }

    /// <summary>
    /// Computes lateral repulsion away from other nearby seekers and survivors.
    /// Solves head-on deadlocks with standard starboard-pass rules and priority yielding.
    /// </summary>
    private Vector3 ComputeAgentRepulsion()
    {
        if (gameController == null) gameController = FindObjectOfType<GameController>();
        if (gameController == null) return Vector3.zero;

        Vector3 repulsion = Vector3.zero;
        Vector3 myPos = transform.position;

        int seekerCount = gameController.SeekerCount;
        for (int i = 0; i < seekerCount; i++)
        {
            AgentActions other = gameController.GetSeeker(i);
            if (other == null || other == agentActions || !other.gameObject.activeInHierarchy) continue;

            Vector3 toOther = other.transform.position - myPos;
            toOther.y = 0f;
            float dist = toOther.magnitude;

            if (dist < agentRepulsionRadius && dist > 0.05f)
            {
                float strength = (agentRepulsionRadius - dist) / agentRepulsionRadius;
                Vector3 away = -toOther.normalized;

                // Lateral avoidance bias: steer to the right of the other agent
                Vector3 lateralRight = Vector3.Cross(Vector3.up, toOther.normalized);
                bool facingHeadOn = Vector3.Dot(transform.forward, other.transform.forward) < -0.3f;

                if (facingHeadOn)
                {
                    // Both steer to their right (starboard pass)
                    repulsion += (lateralRight * 0.8f + away * 0.4f) * strength;
                }
                else
                {
                    // Deterministic right-of-way: lower InstanceID keeps priority; higher yields
                    bool higherPriority = GetInstanceID() < other.GetInstanceID();
                    float yieldFactor = higherPriority ? 0.4f : 1.2f;
                    repulsion += (away * 0.7f + lateralRight * 0.5f) * strength * yieldFactor;
                }
            }
        }

        return repulsion.normalized;
    }

    /// <summary>
    /// 5-Ray Whisker radar to detect obstacles and find the clearest open tangent.
    /// </summary>
    private Vector3 ComputeWhiskerAvoidance(Vector3 travelDir)
    {
        Vector3 origin = transform.position + Vector3.up * whiskerHeight;
        float[] angles = { 0f, 25f, -25f, 55f, -55f };
        float[] distances = { forwardWhiskerDistance, forwardWhiskerDistance * 0.9f, forwardWhiskerDistance * 0.9f, sideWhiskerDistance, sideWhiskerDistance };

        bool hitCenter = false;
        Vector3 hitNormal = Vector3.zero;
        float leftClearance = 0f;
        float rightClearance = 0f;

        for (int i = 0; i < angles.Length; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, angles[i], 0f) * travelDir;
            float maxDist = distances[i];

            if (Physics.SphereCast(origin, 0.28f, dir, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsValidObstacle(hit.collider))
                {
                    if (Mathf.Abs(angles[i]) < 15f)
                    {
                        hitCenter = true;
                        hitNormal = hit.normal;
                    }
                    if (angles[i] > 0f) leftClearance += hit.distance;
                    else if (angles[i] < 0f) rightClearance += hit.distance;
                    continue;
                }
            }

            // Ray had full clearance
            if (angles[i] > 0f) leftClearance += maxDist;
            else if (angles[i] < 0f) rightClearance += maxDist;
        }

        if (hitCenter)
        {
            // Pick clearest side to steer around obstacle
            bool turnRight = rightClearance >= leftClearance;
            Vector3 tangent = turnRight
                ? Vector3.Cross(Vector3.up, -hitNormal).normalized
                : Vector3.Cross(-Vector3.up, -hitNormal).normalized;

            Vector3 avoidDir = (tangent * 0.85f + hitNormal * 0.35f).normalized;
            return avoidDir;
        }

        return Vector3.zero;
    }

    private bool IsValidObstacle(Collider col)
    {
        if (col == null || col.isTrigger) return false;
        if (col.transform.IsChildOf(transform)) return false;
        if (col.CompareTag("Seeker") || col.CompareTag("Hider")) return false;
        if (col.gameObject.name == "Floor" || col.transform.position.y < transform.position.y + 0.05f) return false;
        return true;
    }

    /// <summary>
    /// Tracks agent velocity and displacement to detect perma-stuck conditions and trigger active escape.
    /// </summary>
    private void UpdateStuckWatchdog(bool isTryingToMove)
    {
        if (isUnsticking)
        {
            unstuckTimer -= Time.fixedDeltaTime;
            if (unstuckTimer <= 0f || (transform.position - lastRecordedPosition).sqrMagnitude > 0.4f * 0.4f)
            {
                isUnsticking = false;
                unstuckTimer = 0f;
                lastRecordedPosition = transform.position;
            }
            return;
        }

        positionSampleTimer += Time.fixedDeltaTime;
        if (positionSampleTimer >= 0.4f)
        {
            float displacement = Vector3.Distance(transform.position, lastRecordedPosition);
            lastRecordedPosition = transform.position;
            positionSampleTimer = 0f;

            if (isTryingToMove && displacement < stuckDisplacementThreshold)
            {
                timeStuck += 0.4f;
                if (timeStuck >= stuckDetectionTime)
                {
                    // AGENT IS STUCK! Trigger emergency escape
                    isUnsticking = true;
                    unstuckTimer = unstuckDuration;
                    timeStuck = 0f;

                    // Reverse while pivoting towards open space
                    unstuckTurnBias = (Random.value > 0.5f) ? 1f : -1f;
                    unstuckDirection = (-transform.forward + transform.right * unstuckTurnBias * 0.6f).normalized;
                }
            }
            else
            {
                timeStuck = Mathf.Max(0f, timeStuck - 0.5f);
            }
        }
    }
}
