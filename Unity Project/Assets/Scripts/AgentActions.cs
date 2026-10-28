using Unity.Barracuda;
using Unity.MLAgents.Policies;
using UnityEngine;

public class AgentActions : MonoBehaviour
{
    [SerializeField] private new Rigidbody rigidbody = null;
    [SerializeField] private HideAndSeekAgent hideAndSeekAgent = null;
    [SerializeField] private float runSpeed = 1f;
    [SerializeField] private float drag = 0.3f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float grabDistance = 2f;
    [SerializeField] private float holdBreakDistance = 4f;
    [SerializeField] private bool isHiding = true;

    private Vector2 movementInput = Vector2.zero;
    private float rotationInput = 0f;
    private Vector3? rescueDestination;
    private float rescueStopDistance;
    private bool approachOnly;

    private BoxHolding grabbedBox;
    private Quaternion targetRelativeRotation;

    private BehaviorParameters behaviorParameters = null;

    public Rigidbody Rigidbody { get { return rigidbody; } }
    public HideAndSeekAgent HideAndSeekAgent { get { return hideAndSeekAgent; } }
    
    public bool IsHiding { get { return isHiding; } }

    public bool IsHolding { get { return grabbedBox != null; } }
    // Escort destinations only; an approach toward a sighted survivor is reported by IsApproaching.
    public bool HasRescueDestination => rescueDestination.HasValue && !approachOnly;
    public bool IsApproaching => rescueDestination.HasValue && approachOnly;
    // Pins a policy-driven agent in place (trapped survivors). Human-piloted agents ignore it.
    public bool MovementLocked { get; set; }
    public bool WasCaptured { get; set; } = false;

    public GameController GameController { get; set; }


    private void Awake()
    {
        behaviorParameters = GetComponent<BehaviorParameters>();
        if (GetComponent<AgentTacticalRing>() == null)
        {
            gameObject.AddComponent<AgentTacticalRing>();
        }
        if (!isHiding && GetComponent<SeekerLaserScanner>() == null)
        {
            gameObject.AddComponent<SeekerLaserScanner>();
        }
    }

    private void FixedUpdate()
    {
        if (rescueDestination.HasValue)
        {
            ApplyRescueSteering();
        }
        else if (MovementLocked && (hideAndSeekAgent == null || hideAndSeekAgent.enabled))
        {
            // Zero input but still run Movement() so drag settles any bump from a drone.
            movementInput = Vector2.zero;
            rotationInput = 0f;
        }

        if (!rigidbody.isKinematic && !WasCaptured && (isHiding || GameController.GracePeriodEnded))
        {
            Movement();
        }

        movementInput = Vector2.zero;
        rotationInput = 0f;
    }

    private void ApplyRescueSteering()
    {
        movementInput = Vector2.zero;
        rotationInput = 0f;
        Vector3 direction = rescueDestination.Value - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= rescueStopDistance * rescueStopDistance)
        {
            return;
        }

        Vector3 desired = direction.normalized;
        Vector3 local = transform.InverseTransformDirection(desired);
        movementInput = new Vector2(local.x, local.z);
        float angle = Vector3.SignedAngle(transform.forward, desired, Vector3.up);
        rotationInput = Mathf.Clamp(angle / (rotationSpeed * Time.fixedDeltaTime), -1f, 1f);
    }

    public void SetRescueDestination(Vector3 destination, float stopDistance)
    {
        rescueDestination = destination;
        rescueStopDistance = Mathf.Max(0f, stopDistance);
        approachOnly = false;
    }

    public void SetApproachDestination(Vector3 destination, float stopDistance)
    {
        SetRescueDestination(destination, stopDistance);
        approachOnly = true;
    }

    public void ClearRescueDestination()
    {
        rescueDestination = null;
        approachOnly = false;
    }

    private void Movement()
    {
        // Apply movement
        Vector2 direction = movementInput.normalized;
        Vector3 force = direction.x * transform.right + direction.y * transform.forward;
        rigidbody.AddForce(force * runSpeed, ForceMode.Impulse);

        // Additional movement drag
        Vector3 currentVel = new Vector3(rigidbody.velocity.x, Mathf.Max(0f, rigidbody.velocity.y), rigidbody.velocity.z);
        Vector3 dragForce = -currentVel * drag;
        rigidbody.AddForce(dragForce, ForceMode.Impulse);

        float delta = rotationInput * rotationSpeed * Time.fixedDeltaTime;
        rigidbody.MoveRotation(rigidbody.rotation * Quaternion.AngleAxis(delta, Vector3.up));

        // Adjust grabbed object position / rotation
        if (grabbedBox != null)
        {
            // Adjust position
            Vector3 targetPosition = transform.position + transform.forward * grabDistance;
            Vector3 towards = targetPosition - grabbedBox.Rigidbody.position;
            grabbedBox.Rigidbody.velocity = towards * 10f;

            // Adjust rotation
            Quaternion targetRotation = transform.rotation * targetRelativeRotation;
            Vector3 angularTowards = ShortestPathFromTo(grabbedBox.transform.rotation, targetRotation);
            grabbedBox.Rigidbody.angularVelocity = angularTowards * 0.1f;

            // Break in case the object is too far from holder
            if (Vector3.Distance(grabbedBox.Rigidbody.position, transform.position) > holdBreakDistance)
            {
                grabbedBox.Release();
                grabbedBox = null;
            }
        }
    }


    // Shortest path rotation from one quaternion to another, returned as euler angles
    private Vector3 ShortestPathFromTo(Quaternion from, Quaternion to)
    {
        Quaternion q = Quaternion.Inverse(from) * to;
        Vector3 v = q.eulerAngles;
        float x = v.x > 180f ? v.x - 360f : v.x;
        float y = v.y > 180f ? v.y - 360f : v.y;
        float z = v.z > 180f ? v.z - 360f : v.z;
        return new Vector3(x, y, z);
    }



    public void ApplyMovement(Vector2 input)
    {
        movementInput += input;
    }

    public void ApplyRotation(float delta)
    {
        rotationInput += delta;
    }

    public void GrabBox()
    {
        if (grabbedBox == null)
        {
            if (Physics.Raycast(new Ray(transform.position, transform.forward), out RaycastHit hit))
            {
                if (hit.distance < grabDistance && IsMovable(hit.collider))
                {
                    BoxHolding boxHolding = hit.collider.gameObject.GetComponent<BoxHolding>();
                    if (boxHolding.TryGrab(this))
                    {
                        grabbedBox = boxHolding;
                        // Keep rotation of the object relative to the agent
                        targetRelativeRotation = Quaternion.Inverse(transform.rotation) * grabbedBox.transform.rotation;
                    }
                }
            }
        }
    }

    public void ReleaseBox()
    {
        if (grabbedBox != null)
        {
            grabbedBox.Release();
            grabbedBox = null;
        }
    }

    public void LockBox(bool tryLock)
    {
        if (Physics.Raycast(new Ray(transform.position, transform.forward), out RaycastHit hit))
        {
            if (hit.distance < grabDistance && IsMovable(hit.collider))
            {
                BoxHolding boxHolding = hit.collider.gameObject.GetComponent<BoxHolding>();
                boxHolding.TryLockUnlock(this, tryLock);
            }
        }
    }


    public void ResetAgent()
    {
        ClearRescueDestination();
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        grabbedBox = null;
        WasCaptured = false;
        gameObject.SetActive(true);
    }

    public void SwitchToTraining()
    {
        behaviorParameters.BehaviorType = BehaviorType.Default;
        behaviorParameters.Model = null;
        behaviorParameters.DeterministicInference = false;
    }

    public void SwitchToHeuristic()
    {
        behaviorParameters.BehaviorType = BehaviorType.HeuristicOnly;
        behaviorParameters.Model = null;
    }

    public void SwitchToInference(NNModel model)
    {
        if (model == null)
        {
            return;
        }

        behaviorParameters.Model = model;
        behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
        behaviorParameters.DeterministicInference = true;
    }



    private bool IsMovable(Collider collider)
    {
        return collider.CompareTag("Box") || collider.CompareTag("Box Hider Lock") || collider.CompareTag("Box Seeker Lock") ||
               collider.CompareTag("Ramp") || collider.CompareTag("Ramp Hider Lock") || collider.CompareTag("Ramp Seeker Lock");
    }


    private void OnDrawGizmos()
    {
        if (grabbedBox != null && GameController.DebugDrawBoxHold)
        {
            Gizmos.DrawLine(transform.position, grabbedBox.transform.position);
        }
    }
}
