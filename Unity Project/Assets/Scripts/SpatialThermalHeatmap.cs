using UnityEngine;

[RequireComponent(typeof(GameController))]
public class SpatialThermalHeatmap : MonoBehaviour
{
    private const int GridSize = 30;
    private const int RayCount = 16;
    private const float ScanDistance = 14f;
    private const float SampleInterval = 0.1f;

    private GameController controller;
    private readonly byte[] visits = new byte[GridSize * GridSize];
    private Color32[] colors;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Material material;
    private float scanClock;
    private int visitedCells;

    public float CoveragePercent => 100f * visitedCells / visits.Length;
    public bool Visible => meshRenderer != null && meshRenderer.enabled;

    private void Awake()
    {
        controller = GetComponent<GameController>();
        if (!Application.isBatchMode) BuildOverlay();
    }

    private void OnEnable()
    {
        if (controller != null) controller.OnEpisodeReset += ResetCoverage;
    }

    private void OnDisable()
    {
        if (controller != null) controller.OnEpisodeReset -= ResetCoverage;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H)) Toggle();
    }

    public void Toggle()
    {
        if (meshRenderer != null) meshRenderer.enabled = !meshRenderer.enabled;
    }

    private void FixedUpdate()
    {
        if (controller == null || controller.RoundSettled || !controller.GracePeriodEnded) return;
        scanClock += Time.fixedDeltaTime;
        if (scanClock < SampleInterval) return;
        scanClock = 0f;

        float halfFov = controller.ConeAngle * 0.5f;
        for (int seekerIndex = 0; seekerIndex < controller.SeekerCount; seekerIndex++)
        {
            AgentActions seeker = controller.GetSeeker(seekerIndex);
            if (seeker == null || !seeker.gameObject.activeInHierarchy) continue;
            Vector3 origin = seeker.transform.position + Vector3.up * 0.15f;
            for (int rayIndex = 0; rayIndex < RayCount; rayIndex++)
            {
                float angle = Mathf.Lerp(-halfFov, halfFov, (float)rayIndex / (RayCount - 1));
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * seeker.transform.forward;
                float distance = Physics.Raycast(origin, direction, out RaycastHit hit, ScanDistance)
                    ? hit.distance : ScanDistance;
                MarkRay(origin, direction, distance);
            }
        }
        if (mesh != null) mesh.colors32 = colors;
    }

    private void MarkRay(Vector3 origin, Vector3 direction, float distance)
    {
        float spacing = controller.ArenaSize / GridSize * 0.5f;
        for (float travel = 0f; travel <= distance; travel += spacing)
            MarkCell(origin + direction * travel);
    }

    private void MarkCell(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        float size = controller.ArenaSize;
        int x = Mathf.FloorToInt((local.x / size + 0.5f) * GridSize);
        int z = Mathf.FloorToInt((local.z / size + 0.5f) * GridSize);
        if (x < 0 || x >= GridSize || z < 0 || z >= GridSize) return;
        int index = z * GridSize + x;
        if (visits[index] == 0) visitedCells++;
        if (visits[index] < 4) visits[index]++;
        if (colors == null) return;
        Color32 tint = CellColor(visits[index]);
        for (int corner = 0; corner < 4; corner++)
            colors[index * 4 + corner] = tint;
    }

    private static Color32 CellColor(byte count)
    {
        if (count == 0) return new Color32(120, 25, 35, 55);
        if (count == 1) return new Color32(255, 165, 35, 75);
        if (count == 2) return new Color32(255, 210, 45, 85);
        return new Color32(40, 210, 190, 100);
    }

    public void ResetCoverage()
    {
        System.Array.Clear(visits, 0, visits.Length);
        visitedCells = 0;
        scanClock = 0f;
        if (colors == null) return;
        Color32 tint = CellColor(0);
        for (int i = 0; i < colors.Length; i++) colors[i] = tint;
        if (mesh != null) mesh.colors32 = colors;
    }

    private void BuildOverlay()
    {
        GameObject overlay = new GameObject("ThermalCoverageOverlay");
        overlay.layer = LayerMask.NameToLayer("Ignore Raycast");
        overlay.transform.SetParent(transform, false);
        MeshFilter filter = overlay.AddComponent<MeshFilter>();
        meshRenderer = overlay.AddComponent<MeshRenderer>();
        mesh = new Mesh { name = "ThermalCoverageGrid" };
        int cells = GridSize * GridSize;
        Vector3[] vertices = new Vector3[cells * 4];
        int[] triangles = new int[cells * 6];
        colors = new Color32[cells * 4];
        float cellSize = controller.ArenaSize / GridSize;
        float edge = -controller.ArenaSize * 0.5f;
        for (int z = 0; z < GridSize; z++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                int cell = z * GridSize + x;
                int v = cell * 4;
                int t = cell * 6;
                float x0 = edge + x * cellSize;
                float z0 = edge + z * cellSize;
                vertices[v] = new Vector3(x0, 0.035f, z0);
                vertices[v + 1] = new Vector3(x0 + cellSize, 0.035f, z0);
                vertices[v + 2] = new Vector3(x0, 0.035f, z0 + cellSize);
                vertices[v + 3] = new Vector3(x0 + cellSize, 0.035f, z0 + cellSize);
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        material = new Material(shader) { name = "ThermalCoverageMaterial" };
        material.renderQueue = 3100;
        meshRenderer.sharedMaterial = material;
        ResetCoverage();
        meshRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
