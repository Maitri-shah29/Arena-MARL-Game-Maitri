using UnityEngine;

public class SafeZonePerimeter : MonoBehaviour
{
    private SafeTriageZone triageZone;
    private GameObject visualContainer;
    private Material floorFillMat;
    private Material borderMat;
    private Material crossMat;
    private TextMesh labelMesh;

    private static readonly Color SafeGreen = new Color(0.18f, 0.85f, 0.44f); // #2ECC71

    private void Start()
    {
        triageZone = SafeTriageZone.Instance != null ? SafeTriageZone.Instance : GetComponent<SafeTriageZone>();
        if (triageZone == null)
        {
            triageZone = FindObjectOfType<SafeTriageZone>();
        }

        if (triageZone != null)
        {
            SetupThemedVisuals();
        }
    }

    public void SetupThemedVisuals()
    {
        if (visualContainer != null) return;
        if (triageZone == null) triageZone = SafeTriageZone.Instance;
        if (triageZone == null) return;

        visualContainer = new GameObject("SafeZone_ThemedVisuals");
        visualContainer.transform.SetParent(triageZone.transform, false);

        // 1. Translucent pulsing ground fill quad (NO COLLIDER)
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "SafeZone_FillQuad";
        quad.transform.SetParent(visualContainer.transform, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        quad.transform.localScale = new Vector3(5f, 5f, 1f);

        // Remove collider
        Collider quadCol = quad.GetComponent<Collider>();
        if (quadCol != null) DestroyImmediate(quadCol);

        floorFillMat = ShaderHelper.CreateTransparentMaterial(new Color(SafeGreen.r, SafeGreen.g, SafeGreen.b, 0.16f), "SafeZone_FillMat");
        MeshRenderer quadRend = quad.GetComponent<MeshRenderer>();
        if (quadRend != null)
        {
            quadRend.material = floorFillMat;
            quadRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quadRend.receiveShadows = false;
        }

        // 2. High-contrast perimeter border
        GameObject borderObj = new GameObject("SafeZone_Border");
        borderObj.transform.SetParent(visualContainer.transform, false);
        borderObj.transform.localPosition = new Vector3(0f, 0.04f, 0f);

        LineRenderer borderLr = borderObj.AddComponent<LineRenderer>();
        borderLr.useWorldSpace = false;
        borderLr.loop = true;
        borderLr.positionCount = 4;
        borderLr.startWidth = 0.12f;
        borderLr.endWidth = 0.12f;
        borderLr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        borderLr.receiveShadows = false;

        borderMat = ShaderHelper.CreateUnlitMaterial(SafeGreen, "SafeZone_BorderMat");
        borderLr.material = borderMat;

        float h = 2.5f;
        borderLr.SetPosition(0, new Vector3(-h, 0f, -h));
        borderLr.SetPosition(1, new Vector3(h, 0f, -h));
        borderLr.SetPosition(2, new Vector3(h, 0f, h));
        borderLr.SetPosition(3, new Vector3(-h, 0f, h));

        // Medical cross on the floor, with no collider to block evacuation.
        crossMat = ShaderHelper.CreateUnlitMaterial(Color.white, "TriageCross");
        CreateCrossBar("TriageCrossVertical", new Vector3(0.22f, 0.02f, 1.2f));
        CreateCrossBar("TriageCrossHorizontal", new Vector3(1.2f, 0.02f, 0.22f));

        // 3. World-space label: "SAFE BASE // TRIAGE"
        GameObject labelObj = new GameObject("SafeZone_Label");
        labelObj.transform.SetParent(visualContainer.transform, false);
        labelObj.transform.localPosition = new Vector3(0f, 0.05f, -2.1f);
        labelObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        labelMesh = labelObj.AddComponent<TextMesh>();
        labelMesh.text = "SAFE BASE // TRIAGE";
        labelMesh.fontSize = 24;
        labelMesh.characterSize = 0.10f;
        labelMesh.alignment = TextAlignment.Center;
        labelMesh.anchor = TextAnchor.MiddleCenter;
        labelMesh.color = SafeGreen;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            labelMesh.font = font;
            MeshRenderer labelRend = labelMesh.GetComponent<MeshRenderer>();
            if (labelRend != null && font.material != null)
            {
                labelRend.material = font.material;
            }
        }
    }

    private void CreateCrossBar(string name, Vector3 scale)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        bar.transform.SetParent(visualContainer.transform, false);
        bar.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        bar.transform.localScale = scale;
        Collider collider = bar.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        MeshRenderer renderer = bar.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = crossMat;
    }

    private void Update()
    {
        if (floorFillMat == null) return;

        // Subtle breathing animation using unscaled time
        float pulse = 0.15f + 0.08f * Mathf.Sin(Time.unscaledTime * 2.2f);
        Color c = new Color(SafeGreen.r, SafeGreen.g, SafeGreen.b, pulse);
        ShaderHelper.SetMaterialColor(floorFillMat, c);
    }

    private void OnDestroy()
    {
        if (visualContainer != null) Destroy(visualContainer);
        if (floorFillMat != null) Destroy(floorFillMat);
        if (borderMat != null) Destroy(borderMat);
        if (crossMat != null) Destroy(crossMat);
    }
}
