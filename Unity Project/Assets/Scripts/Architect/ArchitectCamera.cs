using UnityEngine;

// ArchitectCamera – top-down / isometric orbit camera for the Architect phase.
//
// Attach to the Main Camera alongside TacticalCameraController.
// Active ONLY while ArchitectManager.IsArchitectPhase == true.
// Uses Time.unscaledDeltaTime so it works while Time.timeScale == 0.
//
// Controls (architect phase only):
//   Middle-mouse drag OR WASD  → pan
//   Right-mouse drag           → orbit
//   Scroll wheel               → zoom
//   Tab                        → toggle top-down / isometric preset

public class ArchitectCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ArchitectManager architectManager = null;

    [Header("Orbit")]
    [SerializeField] private float orbitSensitivity  = 3f;
    [SerializeField] private float pitchMin          = 20f;
    [SerializeField] private float pitchMax          = 88f;

    [Header("Pan")]
    [SerializeField] private float panSpeed          = 0.15f;   // world units per pixel

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed         = 6f;
    [SerializeField] private float minZoomDist        = 5f;
    [SerializeField] private float maxZoomDist        = 60f;

    [Header("Starting angles (Isometric preset)")]
    [SerializeField] private float startPitch        = 55f;
    [SerializeField] private float startYaw          = 45f;
    [SerializeField] private float startDistance     = 30f;

    // Saved previous camera state for restore
    private Vector3    savedPosition;
    private Quaternion savedRotation;

    // Pivot point that we orbit around
    private Vector3 pivot;

    // Current orbital state
    private float yaw;
    private float pitch;
    private float distance;

    // Cached camera reference (this.gameObject IS the camera)
    private Camera cam;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        if (architectManager != null)
        {
            architectManager.OnArchitectPhaseStarted += Activate;
            architectManager.OnRoundStarted          += Deactivate;
        }
    }

    private void OnDisable()
    {
        if (architectManager != null)
        {
            architectManager.OnArchitectPhaseStarted -= Activate;
            architectManager.OnRoundStarted          -= Deactivate;
        }
    }

    // Called by ArchitectManager.OnArchitectPhaseStarted
    private void Activate()
    {
        savedPosition = transform.position;
        savedRotation = transform.rotation;

        // Compute pivot from platform center (parent or world origin)
        // ArchitectManager is on the Platform, which has a position
        pivot    = architectManager.transform.position;
        yaw      = startYaw;
        pitch    = startPitch;
        distance = startDistance;

        ApplyCameraTransform();
        enabled = true;
    }

    // Called by ArchitectManager.OnRoundStarted
    private void Deactivate()
    {
        // Restore original camera pose so TacticalCameraController takes back smoothly
        transform.position = savedPosition;
        transform.rotation = savedRotation;
        enabled = false;
    }

    private void Start()
    {
        // Start disabled – enabled by Activate() event
        enabled = false;
    }

    private void LateUpdate()
    {
        // Safety: if phase ended, stop running
        if (architectManager == null || !architectManager.IsArchitectPhase)
        {
            enabled = false;
            return;
        }

        float dt = Time.unscaledDeltaTime; // works at timeScale == 0

        HandleOrbit(dt);
        HandlePan(dt);
        HandleZoom();
        HandlePresetToggle();

        ApplyCameraTransform();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Input handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleOrbit(float dt)
    {
        if (!Input.GetMouseButton(1)) return;
        yaw   += Input.GetAxisRaw("Mouse X") * orbitSensitivity;
        pitch -= Input.GetAxisRaw("Mouse Y") * orbitSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
    }

    private void HandlePan(float dt)
    {
        if (architectManager != null)
        {
            HandTrackingReceiver hand = architectManager.GetComponent<HandTrackingReceiver>();
            if (hand != null && hand.HasActiveHand && hand.OpenPalmDelta.sqrMagnitude > 0f)
            {
                Vector3 planarForward = Vector3.Cross(transform.right, Vector3.up).normalized;
                pivot -= (transform.right * hand.OpenPalmDelta.x + planarForward * hand.OpenPalmDelta.y) * 25f;
            }
        }
        // Middle-mouse drag
        bool mmb = Input.GetMouseButton(2);
        // WASD while NOT holding right-mouse (right-mouse = orbit)
        bool wasd = !Input.GetMouseButton(1) &&
                    (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                     Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D));

        if (!mmb && !wasd) return;

        Vector3 right   = transform.right;
        Vector3 forward = Vector3.Cross(right, Vector3.up); // horizontal-only forward

        if (mmb)
        {
            float dx = -Input.GetAxisRaw("Mouse X") * panSpeed * distance * 0.05f;
            float dz = -Input.GetAxisRaw("Mouse Y") * panSpeed * distance * 0.05f;
            pivot += right * dx + forward * dz;
        }
        if (wasd)
        {
            float spd = panSpeed * distance * 0.5f;
            if (Input.GetKey(KeyCode.W)) pivot += forward * spd * dt;
            if (Input.GetKey(KeyCode.S)) pivot -= forward * spd * dt;
            if (Input.GetKey(KeyCode.A)) pivot -= right   * spd * dt;
            if (Input.GetKey(KeyCode.D)) pivot += right   * spd * dt;
        }

        // Clamp pivot inside arena bounds
        if (architectManager != null)
        {
            float half = architectManager.GetComponent<MapGenerator>() != null
                ? architectManager.GetComponent<MapGenerator>().MapSize * 0.5f
                : 15f;
            Vector3 center = architectManager.transform.position;
            pivot.x = Mathf.Clamp(pivot.x, center.x - half, center.x + half);
            pivot.z = Mathf.Clamp(pivot.z, center.z - half, center.z + half);
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * 5f, minZoomDist, maxZoomDist);
        }
    }

    private void HandlePresetToggle()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // Toggle between isometric (pitch≈55) and top-down (pitch≈88)
            pitch = pitch < 80f ? 88f : startPitch;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Camera transform
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyCameraTransform()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3    pos = pivot - (rot * Vector3.forward * distance);

        transform.position = pos;
        transform.rotation = rot;
    }
}
