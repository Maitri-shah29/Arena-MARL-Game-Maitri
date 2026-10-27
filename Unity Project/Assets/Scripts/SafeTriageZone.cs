using System;
using UnityEngine;

// SafeTriageZone - Marks the emergency triage / medical evacuation base.
// Rescued earthquake survivors are escorted or guided into this zone.
public class SafeTriageZone : MonoBehaviour
{
    public static SafeTriageZone Instance { get; private set; }

    [Header("Triage Base Settings")]
    [SerializeField] private Vector3 zoneSize = new Vector3(5f, 1f, 5f);
    [SerializeField] private Color zoneColor = new Color(0.1f, 0.85f, 0.35f, 0.4f);
    [SerializeField] private Color perimeterColor = new Color(0.1f, 0.95f, 0.4f, 0.9f);

    public event Action<AgentActions> OnSurvivorEvacuated;

    private BoxCollider triggerCollider;
    private LineRenderer perimeterLine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        SetupTrigger();
        SetupVisualPerimeter();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void SetupTrigger()
    {
        triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }
        triggerCollider.isTrigger = true;
        triggerCollider.size = zoneSize;
        triggerCollider.center = new Vector3(0f, zoneSize.y * 0.5f, 0f);
    }

    private void SetupVisualPerimeter()
    {
        perimeterLine = GetComponent<LineRenderer>();
        if (perimeterLine == null)
        {
            perimeterLine = gameObject.AddComponent<LineRenderer>();
        }

        perimeterLine.useWorldSpace = false;
        perimeterLine.loop = true;
        perimeterLine.positionCount = 4;
        perimeterLine.startWidth = 0.08f;
        perimeterLine.endWidth = 0.08f;
        perimeterLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        perimeterLine.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.color = perimeterColor;
        perimeterLine.material = mat;

        float hx = zoneSize.x * 0.5f;
        float hz = zoneSize.z * 0.5f;
        float y = 0.05f;

        perimeterLine.SetPosition(0, new Vector3(-hx, y, -hz));
        perimeterLine.SetPosition(1, new Vector3(hx, y, -hz));
        perimeterLine.SetPosition(2, new Vector3(hx, y, hz));
        perimeterLine.SetPosition(3, new Vector3(-hx, y, hz));
    }

    private void OnTriggerEnter(Collider other)
    {
        AgentActions agent = other.GetComponent<AgentActions>();
        if (agent != null && agent.IsHiding)
        {
            SurvivorRescueState survivor = other.GetComponent<SurvivorRescueState>();
            if (survivor != null && survivor.CurrentState == SurvivorRescueState.State.BeingEscorted)
            {
                survivor.MarkAsSafelyRescued();
                OnSurvivorEvacuated?.Invoke(agent);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = zoneColor;
        Gizmos.DrawWireCube(transform.position + new Vector3(0f, zoneSize.y * 0.5f, 0f), zoneSize);
    }
}
