using KadokaShipBattler.Characters;
using UnityEngine;

namespace KadokaShipBattler.Ships
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ShipCore : MonoBehaviour
    {
        [SerializeField] private ShipController ownerShip;
        public ShipController OwnerShip => ownerShip != null ? ownerShip : ownerShip = GetComponentInParent<ShipController>();

        private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;
        public void Initialize(ShipController ship) => ownerShip = ship;

        public bool TryAttack(CrewMember attacker)
        {
            if (attacker == null || OwnerShip == null || attacker.TeamSide == OwnerShip.TeamSide ||
                !attacker.Can(CharacterCapability.Combat))
                return false;
            return OwnerShip.TryDamageCore(Mathf.Max(1f, attacker.Definition.CombatSkill * 5f));
        }
    }
}
