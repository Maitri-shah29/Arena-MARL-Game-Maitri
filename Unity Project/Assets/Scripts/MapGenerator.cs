using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class MapGenerator : MonoBehaviour
{
    [SerializeField] private float mapSize = 20f; // controls where objects are randomized
    [SerializeField] private GameObject wallPrefab = null;
    [SerializeField] private Transform wallsParent = null;
    [SerializeField] private float wallY = 1f;
    [SerializeField] private float wallThickness = 0.25f;
    [SerializeField] private float wallsPosition = 0f; // if equal to 0, then mapSize value is used instead
    [SerializeField] private GameObject floorGameObject = null;

    [Header("Agent placement")]
    // instantiateAgents must be on if agent count should be randomized every episode
    [SerializeField] private Transform agentParent = null;
    [SerializeField] private AgentActions hiderPrefab = null;
    [SerializeField] private AgentActions seekerPrefab = null;
    [SerializeField] private int numHidersMin = 3;
    [SerializeField] private int numHidersMax = 3;
    [SerializeField] private int numSeekersMin = 3;
    [SerializeField] private int numSeekersMax = 3;
    [SerializeField] private float agentY = 1f;
    [SerializeField] private float agentRadius = 0.75f;

    [Header("Object placement")]
    // instantiateBoxes must be on if box count should be randomized every episode
    [SerializeField] private bool instantiateBoxes = true;
    [SerializeField] private BoxHolding[] boxPrefabs = null;
    [SerializeField] private int numBoxesMin = 2;
    [SerializeField] private int numBoxesMax = 4;
    [SerializeField] private float boxY = 1f;
    [SerializeField] private float objectRadius = 1.5f;

    [Header("Subroom generation")]
    [SerializeField] private bool generateSubroom = false;
    [SerializeField] private float roomSize = 10f;
    [SerializeField] private float doorWidth = 2.5f;


    private const int numTriesAgent = 50;
    private const int numTriesBox = 15;

    private List<GameObject> generatedWalls = null;
    private List<GameObject> layoutObjects = null;
    private bool persistentLayout = false;
    private bool boundaryReady = false;

    private AgentActions[] hiders = null;
    private AgentActions[] seekers = null;
    private BoxHolding[] boxes = null;

    public int NumHiders { get; private set; } = 0;
    public int NumSeekers { get; private set; } = 0;
    public List<AgentActions> GetInstantiatedHiders() => hiders.ToList();
    public List<AgentActions> GetInstantiatedSeekers() => seekers.ToList();

    // Read-only accessors for ArchitectManager and ArchitectCamera
    public float AgentRadius  => agentRadius;
    public float ObjectRadius => objectRadius;
    public float MapSize      => mapSize;
    public float WallY        => wallY;
    public float BoxY         => boxY;
    public bool UsesPersistentLayout => persistentLayout;

    public void SetPersistentLayout(bool enabled)
    {
        persistentLayout = enabled;
    }


    private void Awake()
    {
        if (SystemArgs.ArenaParamsPath != null)
        {
            Debug.Log("Arena generation configuration file: " + SystemArgs.ArenaParamsPath);
            string content = File.ReadAllText(SystemArgs.ArenaParamsPath);
            JsonUtility.FromJsonOverwrite(content, this);
        }
    }

    public void Initialize()
    {
        // TODO: scan and remove existing agents

        hiders = Enumerable.Range(0, numHidersMax).Select(_ => Instantiate(hiderPrefab, agentParent)).ToArray();
        seekers = Enumerable.Range(0, numSeekersMax).Select(_ => Instantiate(seekerPrefab, agentParent)).ToArray();

        if (!instantiateBoxes)
        {
            boxes = FindObjectsOfType<BoxHolding>();
        }
    }

    public void Generate()
    {
        if (persistentLayout)
        {
            EnsureBoundary();
            PlaceAgentsOnly();
            return;
        }

        if (PlayerLayout.TryLoad(out PlayerLayoutFile savedLayout))
        {
            GenerateSavedLayout(savedLayout);
            return;
        }

        generatedWalls?.ForEach((GameObject wall) => Destroy(wall));
        generatedWalls = new List<GameObject>();
        DestroyLayoutObjects();
        GenerateMainRoom();
        
        if (instantiateBoxes)
        {
            if (boxes != null)
            {
                Array.ForEach(boxes, (BoxHolding box) => Destroy(box.gameObject));
            }
        }

        if (generateSubroom)
        {
            PlaceSubroomWalls(0f);
            PlaceSubroomWalls(-90f);
        }

        PlaceStuff();
    }


    private void GenerateSavedLayout(PlayerLayoutFile layout)
    {
        generatedWalls?.ForEach((GameObject wall) => { if (wall != null) Destroy(wall); });
        generatedWalls = new List<GameObject>();
        DestroyLayoutObjects();
        if (instantiateBoxes && boxes != null)
        {
            Array.ForEach(boxes, (BoxHolding box) => { if (box != null) Destroy(box.gameObject); });
            boxes = null;
        }

        GenerateMainRoom();
        layoutObjects = new List<GameObject>();
        foreach (PlayerLayoutFile.Piece piece in layout.pieces)
        {
            if (piece == null)
            {
                continue;
            }

            Vector3 world = transform.TransformPoint(new Vector3(piece.px, piece.py, piece.pz));
            Quaternion rotation = transform.rotation * new Quaternion(piece.rx, piece.ry, piece.rz, piece.rw);
            if (piece.kind == "box" || piece.kind == "pillar" || piece.kind == "ramp" || piece.kind == "rubble")
            {
                BoxHolding prefabToSpawn = null;
                if (piece.kind == "pillar") prefabToSpawn = Resources.Load<BoxHolding>("Debris/Pillar_Debris");
                else if (piece.kind == "ramp") prefabToSpawn = Resources.Load<BoxHolding>("Debris/Tilted_Slab_Debris");
                else if (piece.kind == "rubble") prefabToSpawn = Resources.Load<BoxHolding>("Debris/Rubble_Mound_Debris");

                if (prefabToSpawn == null && boxPrefabs != null && boxPrefabs.Length > 0)
                    prefabToSpawn = boxPrefabs[0];

                if (prefabToSpawn != null)
                {
                    BoxHolding spawned = Instantiate(prefabToSpawn, world, rotation, transform);
                    spawned.name = piece.kind + "_Debris";
                    spawned.transform.localScale = new Vector3(piece.sx, piece.sy, piece.sz);
                    spawned.gameObject.AddComponent<ArchitectObject>();
                    if (piece.lockTeam == 1) spawned.ArchitectPreLock(true);
                    else if (piece.lockTeam == 2) spawned.ArchitectPreLock(false);
                    layoutObjects.Add(spawned.gameObject);
                }
            }
            else if (wallPrefab != null)
            {
                GameObject wall = Instantiate(wallPrefab, world, rotation, transform);
                wall.transform.localScale = new Vector3(piece.sx, piece.sy, piece.sz);
                wall.AddComponent<ArchitectObject>();
                layoutObjects.Add(wall);
            }
        }

        PlaceAgentsOnly();
    }

    private void DestroyLayoutObjects()
    {
        if (layoutObjects == null)
        {
            return;
        }

        foreach (GameObject piece in layoutObjects)
        {
            if (piece != null)
            {
                Destroy(piece);
            }
        }
        layoutObjects.Clear();
    }

    private void EnsureBoundary()
    {
        if (boundaryReady && generatedWalls != null && generatedWalls.Count > 0 && generatedWalls.TrueForAll(wall => wall != null))
        {
            return;
        }

        generatedWalls?.ForEach((GameObject wall) => { if (wall != null) Destroy(wall); });
        generatedWalls = new List<GameObject>();
        GenerateMainRoom();
        boundaryReady = true;
    }

    private void PlaceAgentsOnly()
    {
        List<(Vector2, float)> itemPlacement = new List<(Vector2, float)>();
        foreach (ArchitectObject piece in FindObjectsOfType<ArchitectObject>())
        {
            if (piece == null)
            {
                continue;
            }

            Vector3 local = piece.transform.position - transform.position;
            float clearance = objectRadius;
            Renderer pieceRenderer = piece.GetComponent<Renderer>();
            if (pieceRenderer != null)
            {
                clearance = Mathf.Max(pieceRenderer.bounds.extents.x, pieceRenderer.bounds.extents.z) + agentRadius;
            }
            itemPlacement.Add((new Vector2(local.x, local.z), clearance));
        }

        NumHiders = Random.Range(numHidersMin, numHidersMax + 1);
        NumSeekers = Random.Range(numSeekersMin, numSeekersMax + 1);
        PlaceTeam(itemPlacement, NumHiders, PickPointHider, hiders, true);
        PlaceTeam(itemPlacement, NumSeekers, PickPointSeeker, seekers, false);
    }

    private void PlaceTeam(List<(Vector2, float)> itemPlacement, int count, Func<Vector2> pickPoint, AgentActions[] team, bool hidersOnSouth)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryPlaceObject(itemPlacement, pickPoint, agentRadius, numTriesAgent))
            {
                float edge = mapSize * 0.5f - agentRadius - 1f;
                float along = Mathf.Lerp(-edge, edge, count <= 1 ? 0.5f : i / (float)(count - 1));
                itemPlacement.Add((new Vector2(along, hidersOnSouth ? -edge : edge), agentRadius));
            }

            int id = itemPlacement.Count - 1;
            team[i].transform.position = new Vector3(itemPlacement[id].Item1.x, agentY, itemPlacement[id].Item1.y) + transform.position;
            team[i].transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
    }

    private void GenerateMainRoom()
    {
        float size = wallsPosition == 0f ? mapSize : wallsPosition;

        floorGameObject.transform.localScale = new Vector3(size * 0.1f, 1f, size * 0.1f);
        float sx = size;
        float sz = wallThickness;
        Vector3 pos = new Vector3(0f, wallY, size * 0.5f);
        for (int i = 0; i < 4; i++)
        {
            GameObject wall = Instantiate(wallPrefab, pos + transform.position, Quaternion.identity, wallsParent);
            wall.transform.localScale = new Vector3(sx, wall.transform.localScale.y, sz);
            generatedWalls.Add(wall);

            float tmp = sx;
            sx = sz;
            sz = tmp;

            pos = Quaternion.AngleAxis(90f, Vector3.up) * pos;
        }
    }

    private void PlaceSubroomWalls(float rotation)
    {
        Vector3 roomCenter = 0.5f * (mapSize - roomSize) * new Vector3(1f, 0f, -1f);
        float wallZ = -mapSize * 0.5f + roomSize;

        float doorPosition = Random.Range(doorWidth * 0.5f, roomSize - doorWidth * 0.5f);
        if (doorPosition > doorWidth * 0.5f + 0.25f)
        {
            float wallLength = doorPosition - doorWidth * 0.5f;
            float wallX = mapSize * 0.5f - roomSize + wallLength * 0.5f;
            GameObject wall = Instantiate(wallPrefab, new Vector3(wallX, wallY, wallZ) + transform.position, Quaternion.identity, wallsParent);
            wall.transform.localScale = new Vector3(wallLength, wall.transform.localScale.y, wall.transform.localScale.z);
            wall.transform.RotateAround(roomCenter, Vector3.up, rotation);

            generatedWalls.Add(wall);
        }
        if (doorPosition < roomSize - doorWidth * 0.5f - 0.25f)
        {
            float wallLength = roomSize - doorPosition - doorWidth * 0.5f;
            float wallX = mapSize * 0.5f - wallLength * 0.5f;
            GameObject wall = Instantiate(wallPrefab, new Vector3(wallX, wallY, wallZ) + transform.position, Quaternion.identity, wallsParent);
            wall.transform.localScale = new Vector3(wallLength, wall.transform.localScale.y, wall.transform.localScale.z);
            wall.transform.RotateAround(roomCenter, Vector3.up, rotation);

            generatedWalls.Add(wall);
        }
    }

    private void PlaceStuff()
    {
        List<(Vector2, float)> itemPlacement = new List<(Vector2, float)>();

        NumHiders = Random.Range(numHidersMin, numHidersMax + 1);
        NumSeekers = Random.Range(numSeekersMin, numSeekersMax + 1);
        for (int i = 0; i < NumHiders; i++)
        {
            if (!TryPlaceObject(itemPlacement, PickPointHider, agentRadius, numTriesAgent))
            {
                // this shouldn't happen during the training, as it may break trainer
                Debug.LogError("Couldn't randomize agent placement");
                return;
            }
        }
        for (int i = 0; i < NumSeekers; i++)
        {
            if (!TryPlaceObject(itemPlacement, PickPointSeeker, agentRadius, numTriesAgent))
            {
                // this shouldn't happen during the training, as it may break trainer
                Debug.LogError("Couldn't randomize agent placement");
                return;
            }
        }

        int numBoxes = instantiateBoxes ? Random.Range(numBoxesMin, numBoxesMax + 1) : boxes.Length;
        for (int i = 0; i < numBoxes; i++)
        {
            if (!TryPlaceObject(itemPlacement, PickPointBox, objectRadius, numTriesBox))
            {
                Debug.LogWarning("Couldn't randomize box placement");
                numBoxes = i;
                break;
            }
        }

        
        for (int i = 0; i < NumHiders; i++)
        {
            float x = itemPlacement[i].Item1.x;
            float z = itemPlacement[i].Item1.y;
            hiders[i].transform.position = new Vector3(x, agentY, z) + transform.position;
            hiders[i].transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        for (int i = 0; i < NumSeekers; i++)
        {
            int id = i + NumHiders;
            float x = itemPlacement[id].Item1.x;
            float z = itemPlacement[id].Item1.y;
            seekers[i].transform.position = new Vector3(x, agentY, z) + transform.position;
            seekers[i].transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        if (instantiateBoxes)
        {
            boxes = new BoxHolding[numBoxes];
        }
        for (int i = 0; i < numBoxes; i++)
        {
            int id = i + NumHiders + NumSeekers;
            float x = itemPlacement[id].Item1.x;
            float z = itemPlacement[id].Item1.y;
            if (instantiateBoxes)
            {
                BoxHolding boxPrefab = boxPrefabs[Random.Range(0, boxPrefabs.Length)];
                boxes[i] = Instantiate(boxPrefab);
            }
            boxes[i].transform.position = new Vector3(x, boxY, z) + transform.position;
            boxes[i].transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
    }

    private bool TryPlaceObject(List<(Vector2, float)> itemPlacement, Func<Vector2> pickPointFn, float radius, int numTries)
    {
        for (int _ = 0; _ < numTries; _++)
        {
            Vector2 point = pickPointFn.Invoke();
            bool correct = true;
            for (int i = 0; i < itemPlacement.Count; i++)
            {
                if (Vector2.Distance(itemPlacement[i].Item1, point) < itemPlacement[i].Item2 + radius)
                {
                    correct = false;
                    break;
                }
            }

            if (correct)
            {
                itemPlacement.Add((point, radius));
                return true;
            }
        }
        return false;
    }


    private Vector2 PickPointAnywhere(float margin)
    {
        float v = mapSize * 0.5f - margin;
        return PickPointRect(-v, v, -v, v);
    }

    private Vector2 PickPointRoom(float margin)
    {
        float u = mapSize * 0.5f - roomSize + margin;
        float v = mapSize * 0.5f - margin;
        return PickPointRect(u, v, -v, -u);
    }

    private Vector2 PickPointOutside(float margin)
    {
        float u = mapSize * 0.5f - roomSize;
        float v = mapSize * 0.5f;
        
        float x1A = u + margin;
        float x2A = v - margin;
        float z1A = -u + margin;
        float z2A = v - margin;
        float areaA = (x2A - x1A) * (z2A - z1A);

        float x1B = -v + margin;
        float x2B = u - margin;
        float z1B = -v + margin;
        float z2B = v - margin;
        float areaB = (x2B - x1B) * (z2B - z1B);

        return Random.Range(0f, areaA + areaB) < areaA ? PickPointRect(x1A, x2A, z1A, z2A)
                                                       : PickPointRect(x1B, x2B, z1B, z2B);
    }

    private Vector2 PickPointRect(float minX, float maxX, float minZ, float maxZ)
    {
        return new Vector2(Random.Range(minX, maxX), Random.Range(minZ, maxZ));
    }

    private Vector2 PickPointHider()
    {
        return generateSubroom ? PickPointRoom(0.5f * wallThickness + agentRadius)
                               : PickPointAnywhere(0.5f * wallThickness + agentRadius);
    }

    private Vector2 PickPointSeeker()
    {
        return generateSubroom ? PickPointOutside(0.5f * wallThickness + agentRadius)
                               : PickPointAnywhere(0.5f * wallThickness + agentRadius);
    }

    private Vector2 PickPointBox()
    {
        if (generateSubroom)
        {
            return Random.Range(0, 2) == 0 ? PickPointRoom(0.5f * wallThickness + objectRadius)
                                           : PickPointOutside(0.5f * wallThickness + objectRadius);
        }
        else
        {
            return PickPointAnywhere(0.5f * wallThickness + objectRadius);
        }
    }


    public bool InstantiatesBoxesOnReset() => instantiateBoxes;
}
