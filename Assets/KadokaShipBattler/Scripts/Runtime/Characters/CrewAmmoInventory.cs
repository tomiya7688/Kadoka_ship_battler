using KadokaShipBattler.Ammo;
using UnityEngine;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.Characters
{
    [RequireComponent(typeof(CrewMember))]
    public sealed class CrewAmmoInventory : MonoBehaviour
    {
        private CrewMember crewMember;

        private readonly AmmoCarryState state = new AmmoCarryState(0f, 0);
        public AmmoCarryState State
        {
            get
            {
                if (crewMember == null) crewMember = GetComponent<CrewMember>();
                var definition = crewMember.Definition;
                state.ConfigureLimits(definition != null ? definition.CarryCapacity : 0f,
                    definition != null ? definition.MaxCarryCount : 0);
                return state;
            }
        }
        public AmmoDefinition CarriedAmmo => State.CarriedAmmo as AmmoDefinition;
        public bool HasAmmo => State.Count > 0;
        public int Count => State.Count;
        public float CurrentWeight => State.CurrentWeight;
        public float CarryCapacity => State.CarryCapacity;
        public int MaxCarryCount => State.MaxCarryCount;
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

        public bool CanPickup(AmmoDefinition ammo)
        {
            var inventory = State;
            return inventory.CanPickup(ammo, crewMember.Can(CharacterCapability.CarryAmmo));
        }

        public AmmoDefinition TakeAmmo()
        {
            return State.TakeAmmo() as AmmoDefinition;
        }
    }
}
