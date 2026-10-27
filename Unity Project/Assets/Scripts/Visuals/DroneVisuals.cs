using System.Collections.Generic;
using UnityEngine;

public class DroneVisuals : MonoBehaviour
{
    private AgentActions agentActions;
    private GameController gameController;
    private int seekerIndex = -1;

    // Beacon
    private GameObject beaconObj;
    private Material beaconMaterial;
    private Light beaconLight;
    private static readonly Color BeaconBaseColor = new Color(1f, 0.42f, 0f); // Hazard Orange
    private static readonly Color BeaconOffColor = new Color(0.2f, 0.08f, 0f);

    // Sight lines to visible survivors (Amendment 9)
    private readonly List<LineRenderer> sightLines = new List<LineRenderer>();
    private Material sightLineMaterial;

    private void Awake()
    {
        agentActions = GetComponent<AgentActions>();
    }

    private void Start()
    {
        gameController = FindObjectOfType<GameController>();
        ResolveSeekerIndex();
        ApplyDroneChassisSkin();
        SetupBeacon();
        SetupSightLines();
        RestyleExistingScanner();
    }

    private void ResolveSeekerIndex()
    {
        if (gameController == null || agentActions == null) return;
        for (int i = 0; i < gameController.SeekerCount; i++)
        {
            if (gameController.GetSeeker(i) == agentActions)
            {
                seekerIndex = i;
                break;
            }
        }
    }

    private void ApplyDroneChassisSkin()
    {
        // Reskin body to Rescue Drone Hazard Orange with dark trim
        MeshRenderer mainRenderer = GetComponent<MeshRenderer>();
        if (mainRenderer != null)
        {
            // Create a runtime instance material (never modify shared asset)
            Material droneBodyMat = ShaderHelper.CreateOpaqueMaterial(new Color(1f, 0.42f, 0.02f), "RescueDrone_Chassis");
            if (droneBodyMat.HasProperty("_Smoothness")) droneBodyMat.SetFloat("_Smoothness", 0.6f);
            if (droneBodyMat.HasProperty("_Metallic")) droneBodyMat.SetFloat("_Metallic", 0.3f);
            mainRenderer.material = droneBodyMat;
        }

        // Child eye renderers -> dark stealth graphite/black
        MeshRenderer[] childRenderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var rend in childRenderers)
        {
            if (rend != mainRenderer && rend.gameObject != beaconObj)
            {
                Material trimMat = ShaderHelper.CreateOpaqueMaterial(new Color(0.08f, 0.10f, 0.12f), "RescueDrone_Trim");
                rend.material = trimMat;
            }
        }
    }

    private void SetupBeacon()
    {
        if (beaconObj != null) return;

        // Small sphere beacon atop the drone capsule (NO COLLIDER)
        beaconObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        beaconObj.name = "DroneEmergencyBeacon";
        beaconObj.transform.SetParent(transform, false);
        beaconObj.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        beaconObj.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);

        // CRITICAL: Destroy collider so it never obstructs raycasts or physics
        Collider col = beaconObj.GetComponent<Collider>();
        if (col != null)
        {
            DestroyImmediate(col);
        }

        beaconMaterial = ShaderHelper.CreateEmissiveMaterial(BeaconBaseColor, BeaconBaseColor, 2.5f, "DroneBeacon_Mat");
        MeshRenderer rend = beaconObj.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            rend.material = beaconMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        // Add subtle point light
        beaconLight = beaconObj.AddComponent<Light>();
        beaconLight.type = LightType.Point;
        beaconLight.color = BeaconBaseColor;
        beaconLight.range = 2.8f;
        beaconLight.intensity = 1.2f;
        beaconLight.shadows = LightShadows.None;
    }

    private void SetupSightLines()
    {
        if (sightLineMaterial == null)
        {
            sightLineMaterial = ShaderHelper.CreateUnlitMaterial(new Color(1f, 0.65f, 0.05f, 0.75f), "DroneSightLine_Mat");
        }
    }

    private void RestyleExistingScanner()
    {
        SeekerLaserScanner scanner = GetComponent<SeekerLaserScanner>();
        if (scanner != null)
        {
            // Amber hazard sweep color
            scanner.SetScannerColor(new Color(1f, 0.55f, 0.08f, 0.65f));
        }

        AgentTacticalRing ring = GetComponent<AgentTacticalRing>();
        if (ring != null)
        {
            ring.SetBaseColor(new Color(1f, 0.45f, 0.02f, 0.9f));
        }
    }

    private void Update()
    {
        UpdateBeaconPulse();
        UpdateSightLines();
    }

    private void UpdateBeaconPulse()
    {
        if (beaconMaterial == null) return;

        // 2Hz emergency strobe flash using unscaled time
        float strobe = Mathf.Sin(Time.unscaledTime * 12.5f);
        bool flash = strobe > 0.2f;

        Color targetColor = flash ? BeaconBaseColor : BeaconOffColor;
        float emissionIntensity = flash ? 3.5f : 0.2f;

        ShaderHelper.SetMaterialColor(beaconMaterial, targetColor);
        if (beaconMaterial.HasProperty("_EmissionColor"))
        {
            beaconMaterial.SetColor("_EmissionColor", targetColor * emissionIntensity);
        }

        if (beaconLight != null)
        {
            beaconLight.intensity = flash ? 1.6f : 0.1f;
        }
    }

    private void UpdateSightLines()
    {
        if (gameController == null)
        {
            gameController = FindObjectOfType<GameController>();
            if (gameController == null) return;
        }

        if (seekerIndex < 0)
        {
            ResolveSeekerIndex();
            if (seekerIndex < 0) return;
        }

        int hiderCount = gameController.HiderCount;

        // Ensure we have a LineRenderer per hider
        while (sightLines.Count < hiderCount)
        {
            GameObject lineObj = new GameObject("SightLine_" + sightLines.Count);
            lineObj.transform.SetParent(transform, false);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = 0.035f;
            lr.endWidth = 0.035f;
            lr.material = sightLineMaterial;
            lr.startColor = new Color(1f, 0.65f, 0.05f, 0.75f);
            lr.endColor = new Color(1f, 0.65f, 0.05f, 0.75f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;

            sightLines.Add(lr);
        }

        Vector3 dronePos = transform.position + Vector3.up * 0.4f;

        for (int i = 0; i < hiderCount; i++)
        {
            bool seesHider = gameController.GetSeekerSeesHider(seekerIndex, i);
            LineRenderer lr = sightLines[i];

            if (seesHider)
            {
                AgentActions hider = gameController.GetHider(i);
                if (hider != null && hider.gameObject.activeInHierarchy)
                {
                    lr.enabled = true;
                    lr.SetPosition(0, dronePos);
                    lr.SetPosition(1, hider.transform.position + Vector3.up * 0.4f);
                    continue;
                }
            }

            if (lr.enabled)
            {
                lr.enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (beaconObj != null)
        {
            Destroy(beaconObj);
        }
        foreach (var lr in sightLines)
        {
            if (lr != null) Destroy(lr.gameObject);
        }
        sightLines.Clear();
    }
}
