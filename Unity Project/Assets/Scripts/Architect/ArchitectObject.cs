using UnityEngine;

// Marker component – attached to every object the Architect spawns at runtime.
// Used by ArchitectManager for:
//   • cleanup at the start of each new round (before Generate())
//   • demolish eligibility check (only ArchitectObject or box/ramp tag objects may be demolished)
// No inspector fields needed; presence of the component is the signal.
public class ArchitectObject : MonoBehaviour { }
