using KadokaShipBattler.Ammo;
using UnityEngine;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.Characters
{
    [RequireComponent(typeof(CrewMember))]
    public sealed class CrewAmmoInventory : MonoBehaviour
    {
        private CrewMember crewMember;

        public AmmoCarryState State { get; } = new AmmoCarryState();
        public AmmoDefinition CarriedAmmo => State.CarriedAmmo as AmmoDefinition;
        public bool HasAmmo => CarriedAmmo != null;
        public TeamSide TeamSide => GetComponent<CrewMember>().TeamSide;

        private void Awake()
        {
            crewMember = GetComponent<CrewMember>();
        }

        public bool TryPickup(AmmoDefinition ammo)
        {
            if (crewMember == null)
                crewMember = GetComponent<CrewMember>();
            return State.TryPickup(ammo, crewMember.Can(CharacterCapability.CarryAmmo));
        }

        public AmmoDefinition TakeAmmo()
        {
            return State.TakeAmmo() as AmmoDefinition;
        }
    }
}
