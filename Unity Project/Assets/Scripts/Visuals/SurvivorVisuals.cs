using UnityEngine;

public class SurvivorVisuals : MonoBehaviour
{
    private AgentActions agentActions;
    private SurvivorRescueState rescueState;
    private AgentTacticalRing tacticalRing;
    private GameController gameController;
    private int hiderIndex = -1;

    // Located status latched for the episode (Amendment 4)
    public bool WasLocated { get; private set; }

    // Distress ripple for Trapped state
    private GameObject distressRingObj;
    private LineRenderer distressRingRenderer;
    private const int RingSegments = 24;

    // Escort tether line for BeingEscorted state
    private GameObject tetherObj;
    private LineRenderer tetherRenderer;

    // Materials
    private Material ringMat;
    private Material tetherMat;

    private SurvivorRescueState.State lastState = (SurvivorRescueState.State)(-1);

    private void Awake()
    {
        agentActions = GetComponent<AgentActions>();
        rescueState = GetComponent<SurvivorRescueState>();
        tacticalRing = GetComponent<AgentTacticalRing>();
    }

    private void Start()
    {
        gameController = FindObjectOfType<GameController>();
        ResolveHiderIndex();
        ApplySurvivorSkin();
        if (rescueState != null) rescueState.RefreshOriginalColors();
        SetupDistressRing();
        SetupTetherLine();
        RestyleBadgeFont();
    }

    private void ResolveHiderIndex()
    {
        if (gameController == null || agentActions == null) return;
        for (int i = 0; i < gameController.HiderCount; i++)
        {
            if (gameController.GetHider(i) == agentActions)
            {
                hiderIndex = i;
                break;
            }
        }
    }

    public void ResetEpisode()
    {
        WasLocated = false;
        lastState = (SurvivorRescueState.State)(-1);
        if (tetherRenderer != null) tetherRenderer.enabled = false;
        if (distressRingRenderer != null) distressRingRenderer.enabled = true;
    }

    private void ApplySurvivorSkin()
    {
        // High-visibility civilian search-and-rescue vest (Cyan/Blue + Reflective trim)
        MeshRenderer mainRenderer = GetComponent<MeshRenderer>();
        if (mainRenderer != null)
        {
            Material survivorMat = ShaderHelper.CreateOpaqueMaterial(new Color(0.12f, 0.65f, 0.95f), "Survivor_Chassis");
            if (survivorMat.HasProperty("_Smoothness")) survivorMat.SetFloat("_Smoothness", 0.4f);
            mainRenderer.material = survivorMat;
        }

        // Child eye/detail renderers -> safety vest hi-vis trim
        MeshRenderer[] childRenderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var rend in childRenderers)
        {
            if (rend != mainRenderer && rend.gameObject != distressRingObj && rend.gameObject != tetherObj)
            {
                Material trimMat = ShaderHelper.CreateOpaqueMaterial(new Color(0.95f, 0.95f, 0.95f), "Survivor_ReflectiveTrim");
                rend.material = trimMat;
            }
        }
    }

    private void RestyleBadgeFont()
    {
        if (rescueState != null && rescueState.StatusBadge != null)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                rescueState.StatusBadge.font = font;
                MeshRenderer rend = rescueState.StatusBadge.GetComponent<MeshRenderer>();
                if (rend != null && font.material != null)
                {
                    rend.material = font.material;
                }
            }
        }
    }

    private void SetupDistressRing()
    {
        distressRingObj = new GameObject("DistressRippleRing");
        distressRingObj.transform.SetParent(transform, false);
        distressRingObj.transform.localPosition = new Vector3(0f, 0.04f, 0f);

        distressRingRenderer = distressRingObj.AddComponent<LineRenderer>();
        distressRingRenderer.useWorldSpace = false;
        distressRingRenderer.loop = true;
        distressRingRenderer.positionCount = RingSegments;
        distressRingRenderer.startWidth = 0.05f;
        distressRingRenderer.endWidth = 0.05f;
        distressRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        distressRingRenderer.receiveShadows = false;

        ringMat = ShaderHelper.CreateUnlitMaterial(new Color(1f, 0.35f, 0.1f, 0.7f), "DistressRipple_Mat");
        distressRingRenderer.material = ringMat;

        // Ensure NO collider
        Collider col = distressRingObj.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
    }

    private void SetupTetherLine()
    {
        tetherObj = new GameObject("EscortTetherLine");
        tetherObj.transform.SetParent(transform, false);

        tetherRenderer = tetherObj.AddComponent<LineRenderer>();
        tetherRenderer.useWorldSpace = true;
        tetherRenderer.positionCount = 2;
        tetherRenderer.startWidth = 0.04f;
        tetherRenderer.endWidth = 0.04f;
        tetherRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tetherRenderer.receiveShadows = false;
        tetherRenderer.enabled = false;

        tetherMat = ShaderHelper.CreateUnlitMaterial(new Color(1f, 0.88f, 0.15f, 0.8f), "EscortTether_Mat");
        tetherRenderer.material = tetherMat;

        // Ensure NO collider
        Collider col = tetherObj.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
    }

    private void Update()
    {
        CheckLocatedStatus();
        UpdateStateVisuals();
        AnimateDistressRing();
        UpdateTether();
    }

    private void CheckLocatedStatus()
    {
        if (WasLocated) return;

        if (rescueState != null && rescueState.CurrentState != SurvivorRescueState.State.Trapped)
        {
            WasLocated = true;
            return;
        }

        if (gameController != null)
        {
            if (hiderIndex < 0) ResolveHiderIndex();

            if (hiderIndex >= 0)
            {
                int seekerCount = gameController.SeekerCount;
                for (int s = 0; s < seekerCount; s++)
                {
                    if (gameController.GetSeekerSeesHider(s, hiderIndex))
                    {
                        WasLocated = true;
                        break;
                    }
                }
            }
        }
    }

    private void UpdateStateVisuals()
    {
        if (rescueState == null) return;

        SurvivorRescueState.State cur = rescueState.CurrentState;
        if (cur != lastState)
        {
            lastState = cur;
            switch (cur)
            {
                case SurvivorRescueState.State.Trapped:
                    if (tacticalRing != null)
                    {
                        tacticalRing.SetBaseColor(new Color(1f, 0.35f, 0.08f, 0.9f)); // Amber/Red distress
                        tacticalRing.SetPulseSpeed(4f);
                    }
                    if (rescueState.StatusBadge != null)
                    {
                        rescueState.StatusBadge.text = "!";
                        rescueState.StatusBadge.color = new Color(1f, 0.35f, 0.08f);
                    }
                    if (distressRingRenderer != null) distressRingRenderer.enabled = true;
                    if (tetherRenderer != null) tetherRenderer.enabled = false;
                    break;

                case SurvivorRescueState.State.BeingEscorted:
                    if (tacticalRing != null)
                    {
                        tacticalRing.SetBaseColor(new Color(1f, 0.88f, 0.1f, 0.95f)); // Yellow escort
                        tacticalRing.SetPulseSpeed(2.5f);
                    }
                    if (rescueState.StatusBadge != null)
                    {
                        rescueState.StatusBadge.text = ">";
                        rescueState.StatusBadge.color = new Color(1f, 0.88f, 0.1f);
                    }
                    if (distressRingRenderer != null) distressRingRenderer.enabled = false;
                    if (tetherRenderer != null) tetherRenderer.enabled = true;
                    break;

                case SurvivorRescueState.State.Rescued:
                    if (tacticalRing != null)
                    {
                        tacticalRing.SetBaseColor(new Color(0.18f, 0.85f, 0.44f, 0.95f)); // Safe Green
                        tacticalRing.SetPulseSpeed(1.2f);
                    }
                    if (rescueState.StatusBadge != null)
                    {
                        rescueState.StatusBadge.text = "OK";
                        rescueState.StatusBadge.color = new Color(0.18f, 0.85f, 0.44f);
                    }
                    if (distressRingRenderer != null) distressRingRenderer.enabled = false;
                    if (tetherRenderer != null) tetherRenderer.enabled = false;
                    break;
            }
        }
    }

    private void AnimateDistressRing()
    {
        if (distressRingRenderer == null || !distressRingRenderer.enabled) return;

        // Expanding & fading distress ripple pulse using unscaled time
        float cycle = (Time.unscaledTime * 0.7f) % 1f; // 0 to 1
        float radius = 0.85f + cycle * 1.1f;
        float alpha = (1f - cycle) * 0.8f;

        Color c = new Color(1f, 0.35f, 0.08f, alpha);
        distressRingRenderer.startColor = c;
        distressRingRenderer.endColor = c;

        float angleStep = 360f / RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            distressRingRenderer.SetPosition(i, new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius));
        }
    }

    private void UpdateTether()
    {
        if (tetherRenderer == null || !tetherRenderer.enabled) return;

        Transform rescuer = rescueState != null ? rescueState.AssignedRescuer : null;
        if (rescuer != null && rescuer.gameObject.activeInHierarchy)
        {
            Vector3 myPos = transform.position + Vector3.up * 0.35f;
            Vector3 rescuerPos = rescuer.position + Vector3.up * 0.35f;
            tetherRenderer.SetPosition(0, myPos);
            tetherRenderer.SetPosition(1, rescuerPos);
        }
        else
        {
            tetherRenderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (distressRingObj != null) Destroy(distressRingObj);
        if (tetherObj != null) Destroy(tetherObj);
    }
}
