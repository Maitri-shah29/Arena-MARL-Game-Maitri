using System;
using System.Collections.Generic;
using UnityEngine;

// ArchitectManager – core of "The Architect's Gambit" Phase A.
// Attach to the Platform GameObject alongside GameController.
//
// Responsibilities:
//   • Owns the Architect state (paused / running).
//   • Manages energy budget and action costs.
//   • Handles all placement tools: SpawnWall, SpawnBox, Demolish, PreLockBox.
//   • Renders a ghost preview mesh under the cursor.
//   • Validates placement via Physics.OverlapBox + bounds + agent clearance.
//   • Exposes events for future FeatureExtractor / BookmakerAI subscribers.
//   • Cleans up architect-spawned objects at round start.
//   • Undo stack (spawns and pre-locks only; demolish is not undoable).

[RequireComponent(typeof(GameController))]
public class ArchitectManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Architect Enable")]
    [Tooltip("Set false to run exactly as the original (training / headless). Zero behavior change when off.")]
    [SerializeField] public bool architectEnabled = true;

    [Header("Budget")]
    [SerializeField] private int energyBudgetPerRound = 100;

    [Header("Tool Costs")]
    [SerializeField] private int costSpawnWall  = 40;
    [SerializeField] private int costSpawnBox   = 25;
    [SerializeField] private int costDemolish   = 35;
    [SerializeField] private int costPreLock    = 20;

    [Header("Grid")]
    [SerializeField] private float gridCellSize = 1f;

    [Header("Wall spawn dimensions (world-space half-extents for overlap test)")]
    [Tooltip("Half-extents of a freshly placed wall for OverlapBox validation. Default wall prefab is 5x2x0.25 scaled to (1,1,1) so half-extents are (0.5,1,0.125).")]
    [SerializeField] private Vector3 wallHalfExtents = new Vector3(0.5f, 1f, 0.125f);
    [Tooltip("Default wall prefab local scale – applied to the spawned wall so it matches interior room walls.")]
    [SerializeField] private Vector3 wallScale = new Vector3(5f, 2f, 0.25f);

    [Header("References")]
    [Tooltip("Wall prefab (same one MapGenerator uses).")]
    [SerializeField] private GameObject wallPrefab = null;
    [Tooltip("Box prefab (same one MapGenerator uses).")]
    [SerializeField] private BoxHolding boxPrefab = null;
    [Tooltip("MapGenerator on this platform.")]
    [SerializeField] private MapGenerator mapGenerator = null;

    [Header("Ghost preview materials (runtime-tinted; leave null to auto-create)")]
    [Tooltip("Source material for the ghost. Leave null to use Standard shader fallback.")]
    [SerializeField] private Material ghostSourceMaterial = null;

    // ─────────────────────────────────────────────────────────────────────────
    //  Events (Phase B FeatureExtractor will subscribe)
    // ─────────────────────────────────────────────────────────────────────────

    public event Action<int>            OnBudgetChanged;        // arg: remaining energy
    public event Action                 OnLayoutChanged;        // fired on any place/demolish/lock
    public event Action                 OnArchitectPhaseStarted;
    public event Action                 OnRoundStarted;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public read-only state (Phase B FeatureExtractor reads these)
    // ─────────────────────────────────────────────────────────────────────────

    public int  EnergyRemaining    => energyRemaining;
    public int  NetObstacleChange  => placedThisRound - demolishedThisRound;
    public IReadOnlyList<ArchitectActionRecord> ActionsThisRound => actionsThisRound;

    // ─────────────────────────────────────────────────────────────────────────
    //  Tool enumeration (drives UI highlight and keyboard shortcuts 1-4)
    // ─────────────────────────────────────────────────────────────────────────

    public enum Tool { SpawnWall, SpawnBox, Demolish, PreLockHider, PreLockSeeker }
    public Tool CurrentTool { get; private set; } = Tool.SpawnWall;

    // ─────────────────────────────────────────────────────────────────────────
    //  Internal state
    // ─────────────────────────────────────────────────────────────────────────

    private bool isArchitectPhase = false;
    private float savedTimeScale  = 1f;

    private int energyRemaining;
    private int placedThisRound;
    private int demolishedThisRound;
    private List<ArchitectActionRecord> actionsThisRound = new List<ArchitectActionRecord>();

    // All objects this architect spawned this round (for cleanup next round)
    private List<GameObject> architectSpawnedObjects = new List<GameObject>();

    // Undo stack – only spawns and pre-locks are undoable
    private Stack<UndoEntry> undoStack = new Stack<UndoEntry>();

    // Ghost preview
    private GameObject ghostObj   = null;
    private Material   ghostMatOk = null;
    private Material   ghostMatBad= null;

    // Current wall rotation step (0-3, each = 90 degrees around Y)
    private int wallRotationStep = 0;

    // Feedback message shown by ArchitectUI
    public string FeedbackMessage { get; private set; } = "";

    // Cached ref to the GameController's agent lists (set by GameController)
    private GameController gameController;

    // Floor plane used for cursor projection
    private Plane floorPlane;

    // Active camera at phase start (restored on EndArchitectPhase)
    private TacticalCameraController tacticalCamCtrl;

    // ─────────────────────────────────────────────────────────────────────────
    //  Undo entry
    // ─────────────────────────────────────────────────────────────────────────

    private class UndoEntry
    {
        public enum Kind { Spawn, PreLock }
        public Kind          kind;
        public GameObject    spawnedObject;   // for Spawn
        public BoxHolding    lockedBox;       // for PreLock
        public int           cost;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        gameController = GetComponent<GameController>();
        BuildGhostMaterials();
    }

    private void Update()
    {
        if (!isArchitectPhase) return;

        HandleToolHotkeys();
        UpdateGhost();
        HandleMouseClick();
        HandleUndo();
    }

    private void OnDestroy()
    {
        // Clean up ghost and runtime materials
        if (ghostObj != null) Destroy(ghostObj);
        if (ghostMatOk  != null) Destroy(ghostMatOk);
        if (ghostMatBad != null) Destroy(ghostMatBad);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Called by GameController at the end of ResetScene()
    // ─────────────────────────────────────────────────────────────────────────

    public void BeginArchitectPhase()
    {
        if (!architectEnabled) return;

        // Clean up anything the architect placed in the previous round
        CleanupArchitectObjects();

        isArchitectPhase = true;
        energyRemaining  = energyBudgetPerRound;
        placedThisRound  = 0;
        demolishedThisRound = 0;
        actionsThisRound.Clear();
        undoStack.Clear();
        wallRotationStep = 0;
        FeedbackMessage  = "Place objects or press Space to start round.";

        // Floor plane at platform floor level (y = platform.y, nominally 0)
        floorPlane = new Plane(Vector3.up, transform.position);

        // Pause game
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        // Disable TacticalCameraController so ArchitectCamera takes over
        tacticalCamCtrl = Camera.main != null
            ? Camera.main.GetComponent<TacticalCameraController>()
            : null;
        if (tacticalCamCtrl != null) tacticalCamCtrl.enabled = false;

        ShowGhost(true);
        OnArchitectPhaseStarted?.Invoke();
        OnBudgetChanged?.Invoke(energyRemaining);
    }

    // Called by ArchitectUI "Start Round" button or Space key
    public void EndArchitectPhase()
    {
        if (!isArchitectPhase) return;

        isArchitectPhase = false;
        ShowGhost(false);
        FeedbackMessage = "";

        // Restore time
        Time.timeScale = savedTimeScale;

        // Re-enable tactical camera
        if (tacticalCamCtrl != null) tacticalCamCtrl.enabled = true;

        OnRoundStarted?.Invoke();
    }

    // Convenience accessor for ArchitectUI
    public bool IsArchitectPhase => isArchitectPhase;

    // ─────────────────────────────────────────────────────────────────────────
    //  Cleanup
    // ─────────────────────────────────────────────────────────────────────────

    // Destroys all objects that carry the ArchitectObject marker component.
    // Called at BeginArchitectPhase BEFORE mapGenerator.Generate() so that
    // GameController.holdObjects and BoxHolding.Reset() never see architect boxes
    // they didn't register – preventing any null-ref or position confusion.
    private void CleanupArchitectObjects()
    {
        foreach (GameObject go in architectSpawnedObjects)
        {
            if (go != null) Destroy(go);
        }
        architectSpawnedObjects.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Tool hotkeys
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleToolHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetTool(Tool.SpawnWall);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetTool(Tool.SpawnBox);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetTool(Tool.Demolish);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetTool(Tool.PreLockHider);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SetTool(Tool.PreLockSeeker);

        // R → rotate wall by 90° (only meaningful for SpawnWall)
        if (Input.GetKeyDown(KeyCode.R)) wallRotationStep = (wallRotationStep + 1) % 4;

        // Space / Enter → start round
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            EndArchitectPhase();
    }

    public void SetTool(Tool t)
    {
        CurrentTool = t;
        FeedbackMessage = "";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Ghost preview
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildGhostMaterials()
    {
        // We need a material that renders semi-transparent in the Game view.
        // URP Lit in transparent mode is not guaranteed, so we use a runtime
        // approach: clone whatever ghostSourceMaterial was assigned (e.g. Wall.mat),
        // or fall back to a new Standard material, then force Transparent mode.

        Material src = ghostSourceMaterial != null
            ? ghostSourceMaterial
            : new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));

        ghostMatOk  = new Material(src);
        ghostMatBad = new Material(src);

        SetTransparent(ghostMatOk,  new Color(0f,   1f, 0f, 0.45f));
        SetTransparent(ghostMatBad, new Color(1f,   0f, 0f, 0.45f));
    }

    // Forces a material to render in transparent/fade mode and sets its color.
    private static void SetTransparent(Material mat, Color color)
    {
        mat.color = color;

        // Handle both Built-in and URP shaders gracefully.
        // Built-in Standard:
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 3);           // Transparent
            mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",    0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        // URP Lit (Surface = 1 → Transparent):
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend",   0);
            mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",    0);
            mat.renderQueue = 3000;
        }
    }

    private void ShowGhost(bool visible)
    {
        if (ghostObj == null)
        {
            ghostObj = new GameObject("__ArchitectGhost__");
            ghostObj.AddComponent<MeshFilter>();
            ghostObj.AddComponent<MeshRenderer>();
            // No collider – ghost must be ignored by raycasts.
        }
        ghostObj.SetActive(visible);
    }

    private void UpdateGhost()
    {
        if (ghostObj == null) return;

        // Skip ghost for tools that don't show a preview
        bool showGhostForTool = CurrentTool == Tool.SpawnWall || CurrentTool == Tool.SpawnBox;
        if (!showGhostForTool)
        {
            ghostObj.SetActive(false);
            return;
        }
        ghostObj.SetActive(true);

        Vector3 worldPos;
        bool hitFloor = ScreenRayToFloor(out worldPos);
        if (!hitFloor)
        {
            ghostObj.SetActive(false);
            return;
        }

        // Snap to grid
        worldPos = SnapToGrid(worldPos);

        // Choose mesh and half-extents based on tool
        Mesh mesh;
        Vector3 halfExt;
        Quaternion rot;
        float yPos;

        if (CurrentTool == Tool.SpawnWall)
        {
            mesh    = GetPrimitiveMesh(PrimitiveType.Cube);
            halfExt = new Vector3(wallScale.x * 0.5f, wallScale.y * 0.5f, wallScale.z * 0.5f);
            rot     = Quaternion.Euler(0f, wallRotationStep * 90f, 0f);
            yPos    = transform.position.y + mapGenerator.WallY;
        }
        else // SpawnBox
        {
            mesh    = GetPrimitiveMesh(PrimitiveType.Cube);
            halfExt = new Vector3(1f, 1f, 1f); // box scale is 2x2x2 in prefab → 1 unit half-extent
            rot     = Quaternion.identity;
            yPos    = transform.position.y + mapGenerator.BoxY;
        }

        Vector3 pos = new Vector3(worldPos.x, yPos, worldPos.z);
        ghostObj.transform.position   = pos;
        ghostObj.transform.rotation   = rot;
        ghostObj.transform.localScale = CurrentTool == Tool.SpawnWall ? wallScale : new Vector3(2f, 2f, 2f);

        MeshFilter mf = ghostObj.GetComponent<MeshFilter>();
        if (mf.sharedMesh != mesh) mf.sharedMesh = mesh;

        // Validate and color the ghost
        bool valid = IsPlacementValid(pos, halfExt, rot);
        ghostObj.GetComponent<MeshRenderer>().sharedMaterial = valid ? ghostMatOk : ghostMatBad;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Mouse click handling
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleMouseClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        // Don't process if the click is on a UI element
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        switch (CurrentTool)
        {
            case Tool.SpawnWall:      TrySpawnWall();            break;
            case Tool.SpawnBox:       TrySpawnBox();             break;
            case Tool.Demolish:       TryDemolish();             break;
            case Tool.PreLockHider:   TryPreLock(forHiders: true);  break;
            case Tool.PreLockSeeker:  TryPreLock(forHiders: false); break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Spawn Wall
    // ─────────────────────────────────────────────────────────────────────────

    private void TrySpawnWall()
    {
        if (!ChargeBudget(costSpawnWall, "Wall")) return;

        Vector3 worldPos;
        if (!ScreenRayToFloor(out worldPos)) { RefundBudget(costSpawnWall); return; }
        worldPos = SnapToGrid(worldPos);

        Quaternion rot = Quaternion.Euler(0f, wallRotationStep * 90f, 0f);
        float yPos = transform.position.y + mapGenerator.WallY;
        Vector3 pos = new Vector3(worldPos.x, yPos, worldPos.z);

        Vector3 halfExt = new Vector3(wallScale.x * 0.5f, wallScale.y * 0.5f, wallScale.z * 0.5f);
        if (!IsPlacementValid(pos, halfExt, rot))
        {
            RefundBudget(costSpawnWall);
            FeedbackMessage = "Cannot place wall here.";
            return;
        }

        GameObject wall = Instantiate(wallPrefab, pos, rot);
        wall.transform.localScale = wallScale;
        wall.AddComponent<ArchitectObject>();
        architectSpawnedObjects.Add(wall);

        RecordAction(ArchitectActionRecord.ActionType.SpawnWall, pos, costSpawnWall);
        undoStack.Push(new UndoEntry { kind = UndoEntry.Kind.Spawn, spawnedObject = wall, cost = costSpawnWall });
        placedThisRound++;
        FeedbackMessage = $"Wall placed. Energy: {energyRemaining}";
        OnLayoutChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Spawn Box
    // ─────────────────────────────────────────────────────────────────────────

    private void TrySpawnBox()
    {
        if (!ChargeBudget(costSpawnBox, "Box")) return;

        Vector3 worldPos;
        if (!ScreenRayToFloor(out worldPos)) { RefundBudget(costSpawnBox); return; }
        worldPos = SnapToGrid(worldPos);

        float yPos = transform.position.y + mapGenerator.BoxY;
        Vector3 pos = new Vector3(worldPos.x, yPos, worldPos.z);
        Vector3 halfExt = new Vector3(1f, 1f, 1f); // box is 2x2x2 in world

        if (!IsPlacementValid(pos, halfExt, Quaternion.identity))
        {
            RefundBudget(costSpawnBox);
            FeedbackMessage = "Cannot place box here.";
            return;
        }

        BoxHolding box = Instantiate(boxPrefab, pos, Quaternion.identity);
        box.gameObject.AddComponent<ArchitectObject>();
        architectSpawnedObjects.Add(box.gameObject);

        RecordAction(ArchitectActionRecord.ActionType.SpawnBox, pos, costSpawnBox);
        undoStack.Push(new UndoEntry { kind = UndoEntry.Kind.Spawn, spawnedObject = box.gameObject, cost = costSpawnBox });
        placedThisRound++;
        FeedbackMessage = $"Box placed. Energy: {energyRemaining}";
        OnLayoutChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Demolish
    // ─────────────────────────────────────────────────────────────────────────

    // Tags that are eligible for demolition (all box and ramp variants)
    private static readonly string[] demolishableTags = new string[]
    {
        "Box", "Box Hider Lock", "Box Seeker Lock",
        "Ramp", "Ramp Hider Lock", "Ramp Seeker Lock"
    };

    private void TryDemolish()
    {
        if (!ChargeBudget(costDemolish, "Demolish")) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            RefundBudget(costDemolish);
            FeedbackMessage = "Nothing hit.";
            return;
        }

        GameObject target = hit.collider.gameObject;

        // Allow if ArchitectObject marker is present (placed wall)
        bool hasMarker = target.GetComponent<ArchitectObject>() != null;

        // Allow if tag is one of the demolishable box/ramp variants
        bool hasTag = false;
        foreach (string t in demolishableTags)
        {
            if (target.CompareTag(t)) { hasTag = true; break; }
        }

        if (!hasMarker && !hasTag)
        {
            RefundBudget(costDemolish);
            FeedbackMessage = "Can't demolish boundary walls, agents, or the floor.";
            return;
        }

        // Remove from architect tracking list if present
        architectSpawnedObjects.Remove(target);
        // Demolish undo is NOT supported – document this
        undoStack.Clear(); // Clear stack since we can't undo past a demolish cleanly

        RecordAction(ArchitectActionRecord.ActionType.Demolish, target.transform.position, costDemolish);
        demolishedThisRound++;
        Destroy(target);
        FeedbackMessage = $"Demolished. Energy: {energyRemaining}";
        OnLayoutChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Pre-Lock Box
    // ─────────────────────────────────────────────────────────────────────────

    private void TryPreLock(bool forHiders)
    {
        if (!ChargeBudget(costPreLock, "Pre-Lock")) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            RefundBudget(costPreLock);
            FeedbackMessage = "No box hit.";
            return;
        }

        BoxHolding bh = hit.collider.GetComponent<BoxHolding>();
        if (bh == null)
        {
            RefundBudget(costPreLock);
            FeedbackMessage = "Click on a box to pre-lock it.";
            return;
        }

        // ArchitectPreLock is defined on BoxHolding (we add it there)
        bool ok = bh.ArchitectPreLock(forHiders);
        if (!ok)
        {
            RefundBudget(costPreLock);
            FeedbackMessage = "Box already locked.";
            return;
        }

        undoStack.Push(new UndoEntry { kind = UndoEntry.Kind.PreLock, lockedBox = bh, cost = costPreLock });
        string team = forHiders ? "Hider" : "Seeker";
        RecordAction(ArchitectActionRecord.ActionType.PreLock, bh.transform.position, costPreLock);
        FeedbackMessage = $"Box pre-locked for {team}s. Energy: {energyRemaining}";
        OnLayoutChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Undo
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleUndo()
    {
        if (!Input.GetKeyDown(KeyCode.Z)) return;
        if (undoStack.Count == 0)
        {
            FeedbackMessage = "Nothing to undo.";
            return;
        }

        UndoEntry entry = undoStack.Pop();
        switch (entry.kind)
        {
            case UndoEntry.Kind.Spawn:
                if (entry.spawnedObject != null)
                {
                    architectSpawnedObjects.Remove(entry.spawnedObject);
                    Destroy(entry.spawnedObject);
                    placedThisRound = Mathf.Max(0, placedThisRound - 1);
                }
                break;

            case UndoEntry.Kind.PreLock:
                if (entry.lockedBox != null)
                    entry.lockedBox.ArchitectUnlock();
                break;
        }

        energyRemaining += entry.cost;
        if (actionsThisRound.Count > 0)
            actionsThisRound.RemoveAt(actionsThisRound.Count - 1);

        FeedbackMessage = $"Undone. Energy: {energyRemaining}";
        OnBudgetChanged?.Invoke(energyRemaining);
        OnLayoutChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Placement validation
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsPlacementValid(Vector3 pos, Vector3 halfExt, Quaternion rot)
    {
        // 1. Must be inside arena bounds (with a half-extent margin)
        float arenaHalf = mapGenerator.MapSize * 0.5f;
        Vector3 local = pos - transform.position;
        if (Mathf.Abs(local.x) + halfExt.x > arenaHalf) return false;
        if (Mathf.Abs(local.z) + halfExt.z > arenaHalf) return false;

        // 2. Sync transforms so any just-placed objects are seen by the physics query
        Physics.SyncTransforms();

        // 3. OverlapBox – shrink slightly to avoid touching-but-not-penetrating floor
        Vector3 shrunk = new Vector3(halfExt.x * 0.95f, halfExt.y * 0.95f, halfExt.z * 0.95f);
        Collider[] overlaps = Physics.OverlapBox(pos, shrunk, rot);
        foreach (Collider c in overlaps)
        {
            // Ignore the ghost itself (it has no collider, but belt-and-suspenders)
            if (ghostObj != null && c.gameObject == ghostObj) continue;
            // Any other collider means invalid
            return false;
        }

        // 4. Must not be within agentRadius + 0.5 of any active agent
        float minAgentDist = mapGenerator.AgentRadius + 0.5f;
        foreach (AgentActions h in gameController.GetHiders())
        {
            if (h.gameObject.activeInHierarchy &&
                Vector3.Distance(new Vector3(pos.x, h.transform.position.y, pos.z),
                                 h.transform.position) < minAgentDist)
                return false;
        }
        foreach (AgentActions s in gameController.GetSeekers())
        {
            if (s.gameObject.activeInHierarchy &&
                Vector3.Distance(new Vector3(pos.x, s.transform.position.y, pos.z),
                                 s.transform.position) < minAgentDist)
                return false;
        }

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    // Projects a screen-space ray onto the floor plane and returns the world point.
    private bool ScreenRayToFloor(out Vector3 worldPoint)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float dist;
        if (floorPlane.Raycast(ray, out dist))
        {
            worldPoint = ray.GetPoint(dist);
            return true;
        }
        worldPoint = Vector3.zero;
        return false;
    }

    // Snaps a world point to the grid, relative to the platform center.
    private Vector3 SnapToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position;
        local.x = Mathf.Round(local.x / gridCellSize) * gridCellSize;
        local.z = Mathf.Round(local.z / gridCellSize) * gridCellSize;
        return transform.position + local;
    }

    // Returns true if budget is sufficient and deducts the cost.
    private bool ChargeBudget(int cost, string actionName)
    {
        if (energyRemaining < cost)
        {
            FeedbackMessage = $"Not enough energy for {actionName} (need {cost}, have {energyRemaining}).";
            return false;
        }
        energyRemaining -= cost;
        OnBudgetChanged?.Invoke(energyRemaining);
        return true;
    }

    private void RefundBudget(int cost)
    {
        energyRemaining += cost;
        OnBudgetChanged?.Invoke(energyRemaining);
    }

    private void RecordAction(ArchitectActionRecord.ActionType type, Vector3 pos, int cost)
    {
        actionsThisRound.Add(new ArchitectActionRecord
        {
            actionType = type,
            position   = pos,
            cost       = cost
        });
    }

    // Returns a shared primitive mesh (cached by Unity internally).
    private static Mesh GetPrimitiveMesh(PrimitiveType type)
    {
        // Create a temp GO, grab its mesh, destroy GO
        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return mesh;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Data record for Phase B FeatureExtractor
// ─────────────────────────────────────────────────────────────────────────────

[Serializable]
public struct ArchitectActionRecord
{
    public enum ActionType { SpawnWall, SpawnBox, Demolish, PreLock }
    public ActionType actionType;
    public Vector3    position;
    public int        cost;
}
