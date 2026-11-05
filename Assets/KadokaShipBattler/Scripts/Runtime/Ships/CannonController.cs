using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using UnityEngine;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.Ships
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CannonController : MonoBehaviour
    {
        [SerializeField] private ShipController ownerShip;
        [SerializeField] private ShipController targetShip;
        private readonly CannonState state = new CannonState();

        public IAmmo LoadedRound => state.LoadedAmmo;
        public AmmoDefinition LoadedAmmo => (state.LoadedAmmo is AmmoDeckRound round ? round.Definition : state.LoadedAmmo) as AmmoDefinition;
        public bool IsLoaded => state.LoadedAmmo != null;
        public ShipController OwnerShip => ownerShip != null ? ownerShip : ownerShip = GetComponentInParent<ShipController>();

        private void Awake()
        {
            if (ownerShip == null)
                ownerShip = GetComponentInParent<ShipController>();
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        public void Initialize(ShipController owner, ShipController target)
        {
            ownerShip = owner;
            targetShip = target;
        }

        public bool TryLoadFrom(CrewAmmoInventory inventory)
        {
            return inventory != null && OwnerShip != null &&
                state.TryLoadFrom(inventory.State, OwnerShip.BattleState, inventory.TeamSide);
        }

        public bool FireAt(ShipController target)
        {
            return OwnerShip != null && target != null && state.FireAt(OwnerShip.BattleState, target.BattleState);
        }

        public bool TryInteract(CrewMember crew, CrewAmmoInventory inventory)
        {
            if (crew == null || OwnerShip == null || crew.TeamSide != OwnerShip.TeamSide)
                return false;
            return IsLoaded ? crew.Can(CharacterCapability.OperateCannon) && FireAt(targetShip) : TryLoadFrom(inventory);
        }
        private void OnDestroy() => state.Clear();
    }
}
