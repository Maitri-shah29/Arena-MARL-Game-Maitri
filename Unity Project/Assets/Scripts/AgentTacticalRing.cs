using UnityEngine;

[RequireComponent(typeof(AgentActions))]
public class AgentTacticalRing : MonoBehaviour
{
    private AgentActions agentActions;
    private LineRenderer ringRenderer;
    private const int Segments = 28;
    private const float Radius = 0.85f;

    [SerializeField] private Material ringMaterial;

    private Color baseColor;
    private float pulseSpeed = 3f;

    private void Awake()
    {
        agentActions = GetComponent<AgentActions>();
        SetupRing();
    }

    private void SetupRing()
    {
        GameObject ringObj = new GameObject("TacticalRing");
        ringObj.transform.SetParent(transform, false);
        ringObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        ringRenderer = ringObj.AddComponent<LineRenderer>();
        ringRenderer.useWorldSpace = false;
        ringRenderer.loop = true;
        ringRenderer.positionCount = Segments;
        ringRenderer.startWidth = 0.06f;
        ringRenderer.endWidth = 0.06f;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;

        // Use standard sprite or unlit shader material
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);

        baseColor = agentActions.IsHiding
            ? new Color(0f, 0.85f, 1f, 0.85f)    // Neon Cyan for Hiders
            : new Color(1f, 0.12f, 0.2f, 0.85f);  // Crimson for Seekers

        mat.color = baseColor;
        ringRenderer.material = mat;

        // Calculate circle vertices
        float angleStep = 360f / Segments;
        for (int i = 0; i < Segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            ringRenderer.SetPosition(i, new Vector3(Mathf.Cos(rad) * Radius, 0f, Mathf.Sin(rad) * Radius));
        }
    }

    private void Update()
    {
        if (ringRenderer == null) return;

        if (agentActions.WasCaptured)
        {
            ringRenderer.enabled = false;
            return;
        }

        // Pulse emission
        float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * pulseSpeed);
        Color currentColor = baseColor * pulse;
        currentColor.a = baseColor.a;
        ringRenderer.startColor = currentColor;
        ringRenderer.endColor = currentColor;
    }
}
