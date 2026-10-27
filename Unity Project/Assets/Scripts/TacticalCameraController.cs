using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TacticalCameraController : MonoBehaviour
{
    public enum CameraViewMode { Isometric, TopDown, DynamicChase, FirstPerson }

    [Header("Mode & Target")]
    [SerializeField] private CameraViewMode currentMode = CameraViewMode.Isometric;
    [SerializeField] private Vector3 arenaCenter = Vector3.zero;

    [Header("Isometric Settings")]
    [SerializeField] private Vector3 isoOffset = new Vector3(-16f, 21f, -18f);

    [Header("Top-Down Settings")]
    [SerializeField] private float topDownHeight = 28f;

    [Header("Orbit & Zoom")]
    [SerializeField] private float orbitSpeed = 4f;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minDistance = 8f;
    [SerializeField] private float maxDistance = 45f;

    private float currentDistance = 26f;
    private float orbitAngleY = 40f;
    private float orbitAngleX = 45f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private GameController gameController;

    private GUIStyle headerStyle;
    private GUIStyle subStyle;
    private GUIStyle boxStyle;
    private bool stylesInitialized = false;

    private void Start()
    {
        gameController = FindObjectOfType<GameController>();
        currentDistance = isoOffset.magnitude;
        ApplyMode(currentMode);
    }

    private void Update()
    {
        HandleInput();
        UpdateCameraTransform();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyMode(CameraViewMode.Isometric);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyMode(CameraViewMode.TopDown);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyMode(CameraViewMode.DynamicChase);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ApplyMode(CameraViewMode.FirstPerson);

        // Orbit with Right Mouse Button
        if (Input.GetMouseButton(1))
        {
            orbitAngleY += Input.GetAxis("Mouse X") * orbitSpeed;
            orbitAngleX -= Input.GetAxis("Mouse Y") * orbitSpeed;
            orbitAngleX = Mathf.Clamp(orbitAngleX, 15f, 85f);
        }

        // Zoom with ScrollWheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentDistance = Mathf.Clamp(currentDistance - scroll * zoomSpeed * 5f, minDistance, maxDistance);
        }
    }

    public void ApplyMode(CameraViewMode mode)
    {
        currentMode = mode;
        switch (mode)
        {
            case CameraViewMode.Isometric:
                orbitAngleY = 40f;
                orbitAngleX = 45f;
                currentDistance = 28f;
                break;

            case CameraViewMode.TopDown:
                orbitAngleY = 0f;
                orbitAngleX = 88f;
                currentDistance = topDownHeight;
                break;

            case CameraViewMode.DynamicChase:
                orbitAngleX = 35f;
                currentDistance = 16f;
                break;
        }
    }

    private void UpdateCameraTransform()
    {
        if (currentMode == CameraViewMode.FirstPerson && gameController != null)
        {
            HumanPlayer player = FindObjectOfType<HumanPlayer>();
            AgentActions target = player != null ? player.PossessedAgent : null;
            if (target == null)
                target = gameController.GetSeekers().FirstOrDefault(agent => agent != null && agent.gameObject.activeInHierarchy);
            if (target != null)
            {
                transform.position = target.transform.position + Vector3.up * 0.6f;
                transform.rotation = target.transform.rotation;
                return;
            }
        }
        Vector3 focalPoint = arenaCenter;

        if (currentMode == CameraViewMode.DynamicChase && gameController != null)
        {
            var seekers = gameController.GetSeekers().Where(s => s != null && s.gameObject.activeInHierarchy).ToList();
            var hiders = gameController.GetHiders().Where(h => h != null && h.gameObject.activeInHierarchy && !h.WasCaptured).ToList();

            if (seekers.Count > 0 && hiders.Count > 0)
            {
                // Find seeker closest to any hider
                AgentActions closestSeeker = seekers[0];
                AgentActions closestHider = hiders[0];
                float minDst = float.MaxValue;

                foreach (var s in seekers)
                {
                    foreach (var h in hiders)
                    {
                        float d = Vector3.Distance(s.transform.position, h.transform.position);
                        if (d < minDst)
                        {
                            minDst = d;
                            closestSeeker = s;
                            closestHider = h;
                        }
                    }
                }

                // Midpoint between action focal point
                focalPoint = (closestSeeker.transform.position + closestHider.transform.position) * 0.5f;
                orbitAngleY = Mathf.LerpAngle(orbitAngleY, closestSeeker.transform.eulerAngles.y - 30f, Time.deltaTime * 1.5f);
            }
        }

        Quaternion rotation = Quaternion.Euler(orbitAngleX, orbitAngleY, 0f);
        Vector3 position = focalPoint - (rotation * Vector3.forward * currentDistance);

        transform.position = Vector3.Lerp(transform.position, position, Time.deltaTime * 6f);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(focalPoint - transform.position), Time.deltaTime * 6f);
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true
        };
        headerStyle.normal.textColor = Color.white;

        subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true
        };
        subStyle.normal.textColor = new Color(1f, 0.95f, 0.75f, 1f);

        boxStyle = new GUIStyle(GUI.skin.box);
        Texture2D darkBg = new Texture2D(1, 1);
        darkBg.SetPixel(0, 0, new Color(0.04f, 0.06f, 0.09f, 0.85f));
        darkBg.Apply();
        boxStyle.normal.background = darkBg;

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitStyles();

        HumanPlayer player = FindObjectOfType<HumanPlayer>();
        if (player != null && player.IsPlayingRound)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(16, 16, 420, 110), boxStyle);
        GUILayout.BeginVertical();

        GUILayout.Label("ResQ-MARL // DISASTER SEARCH & RESCUE PROTOCOL", headerStyle);

        string phaseText = "RECONNAISSANCE // SITE ASSESSMENT";
        Color phaseColor = new Color(0.2f, 0.85f, 1f);
        if (gameController != null && gameController.GracePeriodEnded)
        {
            phaseText = "ACTIVE // AUTONOMOUS SWARM RESCUE ENGAGED";
            phaseColor = new Color(1f, 0.65f, 0.15f);
        }

        GUI.color = phaseColor;
        GUILayout.Label($"STATUS: {phaseText}", subStyle);
        GUI.color = Color.white;

        GUILayout.Label($"CAMERA: [{currentMode}]  (1: Isometric | 2: Radar Top-Down | 3: Dynamic Chase)", subStyle);
        GUILayout.Label("CONTROLS: RMB Drag to Orbit | Scroll to Zoom | P: Reset Episode", subStyle);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
