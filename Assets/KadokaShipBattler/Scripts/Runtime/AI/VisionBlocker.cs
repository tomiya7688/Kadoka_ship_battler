using UnityEngine;

namespace KadokaShipBattler.AI
{
    // Attach to wall / closed-door Collider2D objects. Disabling this component opens the sight line.
    [DisallowMultipleComponent]
    public sealed class VisionBlocker : MonoBehaviour { }
}
