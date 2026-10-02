using KadokaShipBattler.Ammo;
using KadokaShipBattler.Core;
using KadokaShipBattler.Ships;
using UnityEngine;
namespace KadokaShipBattler.Characters
{
    [RequireComponent(typeof(CrewMember), typeof(CrewAmmoInventory))]
    public sealed class PlayerCrewController : MonoBehaviour
    {
        public BattlePrototype Arena { get; set; }
        private CrewMember Crew => GetComponent<CrewMember>();
        private CrewAmmoInventory Inventory => GetComponent<CrewAmmoInventory>();
        private bool CanAct => Arena == null || !Arena.IsFinished;
        private void Update()
        {
            Move(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.E)) TryInteract();
            if (Input.GetKeyDown(KeyCode.Space)) TryAttack();
        }
        public void Move(Vector2 input, float deltaTime)
        {
            if (!CanAct || Crew.Definition == null || deltaTime <= 0f) return;
            var next = (Vector2)transform.position + Vector2.ClampMagnitude(input, 1f) * (Crew.Definition.MoveSpeed * deltaTime);
            if (Arena == null || Arena.CanPlayerStand(next)) transform.position = new Vector3(next.x, next.y, 0f);
        }
        public bool TryInteract()
        {
            if (!CanAct) return false;
            Physics2D.SyncTransforms();
            var colliders = Physics2D.OverlapCircleAll(transform.position, 0.8f);
            System.Array.Sort(colliders, (a, b) => (a.transform.position - transform.position).sqrMagnitude.CompareTo((b.transform.position - transform.position).sqrMagnitude));
            foreach (var collider in colliders)
            {
                var cannon = collider.GetComponent<CannonController>();
                if (cannon != null && cannon.TryInteract(Crew, Inventory)) return true;
                var pickup = collider.GetComponent<AmmoPickup>();
                if (pickup != null && pickup.TryPickup(Inventory)) return true;
            }
            return false;
        }
        public bool TryAttack()
        {
            if (!CanAct) return false;
            Physics2D.SyncTransforms();
            foreach (var collider in Physics2D.OverlapCircleAll(transform.position, 0.9f))
            {
                var core = collider.GetComponent<ShipCore>();
                if (core != null && core.TryAttack(Crew)) return true;
            }
            return false;
        }
    }
}
