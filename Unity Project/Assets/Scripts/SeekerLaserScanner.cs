using UnityEngine;

[RequireComponent(typeof(AgentActions))]
public class SeekerLaserScanner : MonoBehaviour
{
    private AgentActions agentActions;
    private LineRenderer laserRenderer;
    private const int RayCount = 20;
    private const float MaxScanDistance = 14f;
    private const float FovAngle = 67.5f;

    private Material laserMaterial;
    private Color normalColor = new Color(1f, 0.08f, 0.15f, 0.5f);
    private Color alertColor = new Color(1f, 0.8f, 0f, 0.85f);

    private void Awake()
    {
        agentActions = GetComponent<AgentActions>();
        if (agentActions.IsHiding)
        {
            // Only Seekers have scanner lasers
            Destroy(this);
            return;
        }

        SetupLaser();
    }

    private void SetupLaser()
    {
        GameObject laserObj = new GameObject("LaserScanner");
        laserObj.transform.SetParent(transform, false);
        laserObj.transform.localPosition = new Vector3(0f, 0.12f, 0f);

        laserRenderer = laserObj.AddComponent<LineRenderer>();
        laserRenderer.useWorldSpace = true;
        laserRenderer.loop = true;
        laserRenderer.positionCount = RayCount + 2;
        laserRenderer.startWidth = 0.04f;
        laserRenderer.endWidth = 0.04f;
        laserRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laserRenderer.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        laserMaterial = new Material(shader);
        laserMaterial.color = normalColor;
        laserRenderer.material = laserMaterial;
    }

    private void LateUpdate()
    {
        if (laserRenderer == null) return;

        // Origin at agent center slightly above floor
        Vector3 origin = transform.position + Vector3.up * 0.12f;
        laserRenderer.SetPosition(0, origin);

        float halfFov = FovAngle * 0.5f;
        float angleStep = FovAngle / (RayCount - 1);

        for (int i = 0; i < RayCount; i++)
        {
            float currentAngle = -halfFov + i * angleStep;
            Quaternion rot = Quaternion.AngleAxis(currentAngle, Vector3.up);
            Vector3 dir = rot * transform.forward;

            Vector3 hitPoint = origin + dir * MaxScanDistance;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, MaxScanDistance))
            {
                // Stop at obstacles / walls / boxes
                if (hit.collider.gameObject != gameObject)
                {
                    hitPoint = hit.point;
                }
            }

            hitPoint.y = origin.y;
            laserRenderer.SetPosition(i + 1, hitPoint);
        }

        // Close the loop back to origin
        laserRenderer.SetPosition(RayCount + 1, origin);

        // Subtle pulsing laser effect
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 6f);
        Color col = normalColor * pulse;
        col.a = normalColor.a;
        laserRenderer.startColor = col;
        laserRenderer.endColor = col;
    }
}
