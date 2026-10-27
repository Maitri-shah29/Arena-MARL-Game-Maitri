using UnityEngine;

public class BoxHolding : MonoBehaviour
{
    [SerializeField] private new Rigidbody rigidbody = null;

    [SerializeField] private Material materialDefault = null;
    [SerializeField] private Material materialLockHider = null;
    [SerializeField] private Material materialLockSeeker = null;

    [SerializeField] private string tagDefault = "";
    [SerializeField] private string tagLockHider = "";
    [SerializeField] private string tagLockSeeker = "";

    private MeshRenderer meshRenderer = null;

    private AgentActions owner = null;
    private AgentActions lockOwner = null;

    private Vector3 startPosition;
    private Quaternion startRotation;

    // ── Architect pre-lock state ──────────────────────────────────────────────
    // True while this box is locked by the Architect (not by an in-game agent).
    // Cleared by Reset() so the lock dissolves at the start of each new episode.
    private bool _isArchitectLocked = false;

    // Hidden sentinel AgentActions used as a non-null lockOwner placeholder.
    // TryGrab and TryLockUnlock only check lockOwner != null, so any non-null
    // value blocks agent interaction for the whole round.
    private AgentActions _architectSentinel = null;


    public int ArchitectLockTeam
    {
        get
        {
            if (!_isArchitectLocked) return 0;
            return tag == tagLockHider ? 1 : 2;
        }
    }

    public AgentActions Owner { get { return owner; } }
    public AgentActions LockOwner { get { return lockOwner; } }
    public Rigidbody Rigidbody { get { return rigidbody; } }


    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }


    public bool TryGrab(AgentActions agent)
    {
        if (lockOwner != null || owner != null)
        {
            return false;
        }
        owner = agent;
        return true;
    }

    public void Release()
    {
        owner = null;
    }

    public void TryLockUnlock(AgentActions agent, bool tryLock)
    {
        if (tryLock)
        {
            if (lockOwner == null && (owner == null || owner.IsHiding == agent.IsHiding))
            {
                rigidbody.isKinematic = true;
                owner?.ReleaseBox();
                owner = null;
                lockOwner = agent;
                meshRenderer.material = agent.IsHiding ? materialLockHider : materialLockSeeker;
                tag = agent.IsHiding ? tagLockHider : tagLockSeeker;
            }
        }
        else
        {
            if (lockOwner != null && lockOwner.IsHiding == agent.IsHiding)
            {
                rigidbody.isKinematic = false;
                lockOwner = null;
                meshRenderer.material = materialDefault;
                tag = tagDefault;
            }
        }
    }


    // ── Architect pre-lock API ────────────────────────────────────────────────
    // Locks the box for one team without requiring a live AgentActions reference.
    // The lock persists for the whole round:
    //   - TryGrab returns false (lockOwner != null -> grab blocked for all agents).
    //   - Agents cannot unlock it because the sentinel's IsHiding value
    //     does not match any real agent team check for TryLockUnlock.
    // BoxHolding.Reset() clears lockOwner and the sentinel automatically, so
    // the lock is dissolved at the start of each new episode.
    // Returns false if already locked (by agent or architect).
    public bool ArchitectPreLock(bool forHiders)
    {
        if (lockOwner != null) return false;   // already locked
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

        if (_architectSentinel == null)
        {
            GameObject s = new GameObject("__ArchitectLockSentinel__");
            s.hideFlags = HideFlags.HideInHierarchy;
            _architectSentinel = s.AddComponent<AgentActions>();
        }

        rigidbody.isKinematic = true;
        owner?.ReleaseBox();
        owner = null;
        lockOwner = _architectSentinel;
        meshRenderer.material = forHiders ? materialLockHider : materialLockSeeker;
        tag = forHiders ? tagLockHider : tagLockSeeker;
        _isArchitectLocked = true;
        return true;
    }

    // Reverts an architect pre-lock (called by Undo). Safe to call if not locked.
    public void ArchitectUnlock()
    {
        if (!_isArchitectLocked) return;
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        rigidbody.isKinematic = false;
        lockOwner = null;
        meshRenderer.material = materialDefault;
        tag = tagDefault;
        _isArchitectLocked = false;
    }


    public void RestorePlacedPose()
    {
        owner = null;
        if (rigidbody != null)
        {
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.isKinematic = _isArchitectLocked;
        }
        transform.position = startPosition;
        transform.rotation = startRotation;
    }

    public void SavePlacedPose()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public void Reset()
    {
        owner = null;
        lockOwner = null;
        _isArchitectLocked = false;
        if (_architectSentinel != null)
        {
            Destroy(_architectSentinel.gameObject);
            _architectSentinel = null;
        }
        tag = tagDefault;
        rigidbody.isKinematic = false;
        meshRenderer.material = materialDefault;
        transform.position = startPosition;
        transform.rotation = startRotation;
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
    }
}
