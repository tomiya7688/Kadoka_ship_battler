using KadokaShipBattler.Characters;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    // Stationary defensive behavior for the prototype, not a tactical director.
    [RequireComponent(typeof(PlayerCrewController), typeof(VisionSensor))]
    public sealed class CrewGuardAiController : MonoBehaviour
    {
        private PlayerCrewController actor;
        private float attackTimer;
        private void Awake() => actor = GetComponent<PlayerCrewController>();
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float deltaTime)
        {
            if (!actor.IsAvailable || actor.Arena == null || !actor.Arena.EnemyActionsEnabled || actor.Arena.IsFinished || deltaTime <= 0) return;
            attackTimer -= deltaTime;
            if (attackTimer > 0) return;
            var sensor = GetComponent<VisionSensor>();
            sensor.Scan();
            var attacked = false;
            foreach (var observation in sensor.Observations)
            {
                if (observation.Kind == ObservedTargetKind.Ammo || observation.TeamSide == actor.TeamSide) continue;
                if (!actor.TryAttackVisible(observation)) continue;
                attacked = true;
                break;
            }
            if (!attacked) actor.Face(new Vector2(-actor.Facing.y, actor.Facing.x));
            attackTimer = 1f;
        }
    }
}
