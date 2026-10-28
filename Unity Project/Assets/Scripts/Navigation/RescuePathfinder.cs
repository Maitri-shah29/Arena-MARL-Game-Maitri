using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// RescuePathfinder: Hybrid navigation system for ResQ-MARL disaster environments.
/// Provides collision-free global pathfinding around walls, collapsed pillars, tilted slabs,
/// rubble heaps, and barricades. Checks Unity NavMesh first, with an instant A* 2D Grid
/// pathfinder and line-of-sight shortcutting fallback for dynamic environments.
/// </summary>
public class RescuePathfinder : MonoBehaviour
{
    public static RescuePathfinder Instance { get; private set; }

    [Header("Grid Configuration")]
    [SerializeField] private float arenaSize = 20f;
    [SerializeField] private float cellSize = 0.5f;
    [SerializeField] private float agentClearanceRadius = 0.52f;
    [SerializeField] private LayerMask obstacleLayerMask = ~0;

    private int gridResolution;
    private bool[,] walkableGrid;
    private float lastScanTime = -10f;
    private GameController gameController;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        gameController = GetComponent<GameController>();
        if (gameController == null) gameController = FindObjectOfType<GameController>();
        if (gameController != null) arenaSize = gameController.ArenaSize;

        gridResolution = Mathf.RoundToInt(arenaSize / cellSize);
        if (gridResolution <= 0) gridResolution = 40;
        walkableGrid = new bool[gridResolution, gridResolution];
    }

    private void Start()
    {
        ScanObstacles();
        if (gameController != null)
        {
            gameController.OnEpisodeReset += HandleEpisodeReset;
        }
    }

    private void OnDestroy()
    {
        if (gameController != null)
        {
            gameController.OnEpisodeReset -= HandleEpisodeReset;
        }
        if (Instance == this) Instance = null;
    }

    private void HandleEpisodeReset()
    {
        ScanObstacles();
    }

    /// <summary>
    /// Scans the entire arena floor and builds an obstacle avoidance grid.
    /// Excludes trigger colliders, floor, and agents.
    /// </summary>
    public void ScanObstacles()
    {
        if (walkableGrid == null || walkableGrid.GetLength(0) != gridResolution)
        {
            walkableGrid = new bool[gridResolution, gridResolution];
        }

        Vector3 center = transform.position;
        float halfSize = arenaSize * 0.5f;

        for (int x = 0; x < gridResolution; x++)
        {
            float xPos = center.x - halfSize + (x + 0.5f) * cellSize;
            for (int z = 0; z < gridResolution; z++)
            {
                float zPos = center.z - halfSize + (z + 0.5f) * cellSize;
                Vector3 samplePos = new Vector3(xPos, center.y + 0.4f, zPos);

                // Check for obstacle collision with agent clearance radius
                Collider[] hits = Physics.OverlapSphere(samplePos, agentClearanceRadius, obstacleLayerMask, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider col = hits[i];
                    if (col.isTrigger) continue;
                    if (col.CompareTag("Seeker") || col.CompareTag("Hider")) continue;
                    if (col.gameObject.name == "Floor" || col.transform.position.y < center.y + 0.05f) continue;

                    blocked = true;
                    break;
                }

                walkableGrid[x, z] = !blocked;
            }
        }

        lastScanTime = Time.time;
    }

    /// <summary>
    /// Computes a collision-free waypoint path from start to destination.
    /// </summary>
    public List<Vector3> FindPath(Vector3 start, Vector3 target)
    {
        // 1. If direct straight line is clear of obstacles, skip pathfinding
        if (IsDirectPathClear(start, target))
        {
            return new List<Vector3> { target };
        }

        // 2. Try Unity NavMesh if baked NavMesh is available
        NavMeshPath navPath = new NavMeshPath();
        if (NavMesh.CalculatePath(start, target, NavMesh.AllAreas, navPath) && navPath.status == NavMeshPathStatus.PathComplete && navPath.corners.Length > 1)
        {
            List<Vector3> navCorners = new List<Vector3>(navPath.corners);
            SmoothPath(navCorners);
            return navCorners;
        }

        // 3. Dynamic A* Grid Pathfinder
        if (Time.time - lastScanTime > 3f)
        {
            ScanObstacles();
        }

        WorldToGrid(start, out int startX, out int startZ);
        WorldToGrid(target, out int targetX, out int targetZ);

        startX = Mathf.Clamp(startX, 0, gridResolution - 1);
        startZ = Mathf.Clamp(startZ, 0, gridResolution - 1);
        targetX = Mathf.Clamp(targetX, 0, gridResolution - 1);
        targetZ = Mathf.Clamp(targetZ, 0, gridResolution - 1);

        // If target cell is blocked, find closest walkable cell to target
        if (!walkableGrid[targetX, targetZ])
        {
            if (!FindNearestWalkable(targetX, targetZ, out targetX, out targetZ))
            {
                return new List<Vector3> { target };
            }
        }

        List<Vector3> gridPath = RunAStar(startX, startZ, targetX, targetZ);
        if (gridPath != null && gridPath.Count > 0)
        {
            // Ensure final destination is the exact target position
            gridPath[gridPath.Count - 1] = target;
            SmoothPath(gridPath);
            return gridPath;
        }

        return new List<Vector3> { target };
    }

    private List<Vector3> RunAStar(int startX, int startZ, int targetX, int targetZ)
    {
        int[,] gScore = new int[gridResolution, gridResolution];
        int[,] fScore = new int[gridResolution, gridResolution];
        int[,] parentX = new int[gridResolution, gridResolution];
        int[,] parentZ = new int[gridResolution, gridResolution];
        bool[,] closed = new bool[gridResolution, gridResolution];

        for (int x = 0; x < gridResolution; x++)
        {
            for (int z = 0; z < gridResolution; z++)
            {
                gScore[x, z] = int.MaxValue;
                fScore[x, z] = int.MaxValue;
                parentX[x, z] = -1;
                parentZ[x, z] = -1;
            }
        }

        List<int> openSet = new List<int>(64);
        int startIndex = startZ * gridResolution + startX;
        openSet.Add(startIndex);
        gScore[startX, startZ] = 0;
        fScore[startX, startZ] = Heuristic(startX, startZ, targetX, targetZ);

        int[] dx = { 0, 0, -1, 1, -1, 1, -1, 1 };
        int[] dz = { -1, 1, 0, 0, -1, -1, 1, 1 };
        int[] costs = { 10, 10, 10, 10, 14, 14, 14, 14 };

        int iterations = 0;
        const int maxIterations = 800;

        while (openSet.Count > 0 && iterations++ < maxIterations)
        {
            int bestIdx = 0;
            int bestF = int.MaxValue;
            for (int i = 0; i < openSet.Count; i++)
            {
                int node = openSet[i];
                int nx = node % gridResolution;
                int nz = node / gridResolution;
                if (fScore[nx, nz] < bestF)
                {
                    bestF = fScore[nx, nz];
                    bestIdx = i;
                }
            }

            int current = openSet[bestIdx];
            openSet.RemoveAt(bestIdx);

            int cx = current % gridResolution;
            int cz = current / gridResolution;

            if (cx == targetX && cz == targetZ)
            {
                // Reconstruct path
                List<Vector3> path = new List<Vector3>();
                int currX = targetX;
                int currZ = targetZ;
                while (currX != -1 && currZ != -1)
                {
                    path.Add(GridToWorld(currX, currZ));
                    int px = parentX[currX, currZ];
                    int pz = parentZ[currX, currZ];
                    if (currX == startX && currZ == startZ) break;
                    currX = px;
                    currZ = pz;
                }
                path.Reverse();
                return path;
            }

            closed[cx, cz] = true;

            for (int dir = 0; dir < 8; dir++)
            {
                int nx = cx + dx[dir];
                int nz = cz + dz[dir];

                if (nx < 0 || nx >= gridResolution || nz < 0 || nz >= gridResolution) continue;
                if (closed[nx, nz] || !walkableGrid[nx, nz]) continue;

                // For diagonal steps, ensure both orthogonal neighbors are walkable (no corner-cutting)
                if (dir >= 4)
                {
                    if (!walkableGrid[cx + dx[dir], cz] || !walkableGrid[cx, cz + dz[dir]]) continue;
                }

                int tentativeG = gScore[cx, cz] + costs[dir];
                if (tentativeG < gScore[nx, nz])
                {
                    parentX[nx, nz] = cx;
                    parentZ[nx, nz] = cz;
                    gScore[nx, nz] = tentativeG;
                    fScore[nx, nz] = tentativeG + Heuristic(nx, nz, targetX, targetZ);

                    int neighborIndex = nz * gridResolution + nx;
                    if (!openSet.Contains(neighborIndex))
                    {
                        openSet.Add(neighborIndex);
                    }
                }
            }
        }

        return null;
    }

    private int Heuristic(int x1, int z1, int x2, int z2)
    {
        int dx = Mathf.Abs(x1 - x2);
        int dz = Mathf.Abs(z1 - z2);
        return 10 * (dx + dz) + (14 - 20) * Mathf.Min(dx, dz);
    }

    private bool FindNearestWalkable(int cx, int cz, out int foundX, out int foundZ)
    {
        for (int r = 1; r <= 5; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    int nx = cx + dx;
                    int nz = cz + dz;
                    if (nx >= 0 && nx < gridResolution && nz >= 0 && nz < gridResolution && walkableGrid[nx, nz])
                    {
                        foundX = nx;
                        foundZ = nz;
                        return true;
                    }
                }
            }
        }
        foundX = cx;
        foundZ = cz;
        return false;
    }

    /// <summary>
    /// Line-of-sight path smoothing: removes redundant zigzag waypoints if a direct
    /// path between non-adjacent waypoints is unobstructed.
    /// </summary>
    private void SmoothPath(List<Vector3> path)
    {
        if (path == null || path.Count <= 2) return;

        int current = 0;
        while (current < path.Count - 2)
        {
            int check = current + 2;
            while (check < path.Count && IsDirectPathClear(path[current], path[check]))
            {
                path.RemoveAt(current + 1);
            }
            current++;
        }
    }

    public bool IsDirectPathClear(Vector3 from, Vector3 to)
    {
        Vector3 start = from + Vector3.up * 0.4f;
        Vector3 end = to + Vector3.up * 0.4f;
        Vector3 dir = end - start;
        float dist = dir.magnitude;
        if (dist <= 0.05f) return true;

        dir /= dist;
        RaycastHit[] hits = Physics.SphereCastAll(start, 0.45f, dir, dist, obstacleLayerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col.isTrigger) continue;
            if (col.CompareTag("Seeker") || col.CompareTag("Hider")) continue;
            if (col.gameObject.name == "Floor" || col.transform.position.y < transform.position.y + 0.05f) continue;

            return false;
        }

        return true;
    }

    public void WorldToGrid(Vector3 worldPos, out int gx, out int gz)
    {
        Vector3 local = worldPos - transform.position;
        float halfSize = arenaSize * 0.5f;
        gx = Mathf.FloorToInt((local.x + halfSize) / cellSize);
        gz = Mathf.FloorToInt((local.z + halfSize) / cellSize);
    }

    public Vector3 GridToWorld(int gx, int gz)
    {
        float halfSize = arenaSize * 0.5f;
        float x = transform.position.x - halfSize + (gx + 0.5f) * cellSize;
        float z = transform.position.z - halfSize + (gz + 0.5f) * cellSize;
        return new Vector3(x, transform.position.y, z);
    }
}
