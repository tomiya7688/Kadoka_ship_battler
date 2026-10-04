using KadokaShipBattler.Characters;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    // Stationary defensive behavior for the prototype, not a tactical director.
    [RequireComponent(typeof(PlayerCrewController))]
    public sealed class CrewGuardAiController : MonoBehaviour
    {
        private PlayerCrewController actor;
        private float attackTimer;
        private void Awake() => actor = GetComponent<PlayerCrewController>();
        private void Update()
        {
            if (!actor.IsAvailable || actor.Arena == null || !actor.Arena.EnemyActionsEnabled || actor.Arena.IsFinished) return;
            attackTimer -= Time.deltaTime;
            if (attackTimer > 0) return;
            if (!actor.TryAttack()) actor.Face(new Vector2(-actor.Facing.y, actor.Facing.x));
            attackTimer = 1f;
        }
    }
}
