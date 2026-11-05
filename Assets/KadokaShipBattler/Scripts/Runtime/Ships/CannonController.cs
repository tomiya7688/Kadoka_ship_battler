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
        private CrewMember loadedCharacter;

        public IAmmo LoadedRound => state.LoadedAmmo;
        public AmmoDefinition LoadedAmmo => (state.LoadedAmmo is AmmoDeckRound round ? round.Definition : state.LoadedAmmo) as AmmoDefinition;
        public bool IsLoaded => state.LoadedAmmo != null || loadedCharacter != null;
        public CrewMember LoadedCharacter => loadedCharacter;
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
            return inventory != null && OwnerShip != null && loadedCharacter == null &&
                state.TryLoadFrom(inventory.State, OwnerShip.BattleState, inventory.TeamSide);
        }

        /* { 処理: 同じ陣営の生存した船員を、空の砲台へ弾として装填します。戻り値: 装填できたらtrueです。 } */
        public bool TryLoadCharacter(CrewMember passenger)
        {
            if (passenger == null || OwnerShip == null || IsLoaded || !passenger.IsAlive || passenger.TeamSide != OwnerShip.TeamSide)
                return false;
            // 砲台の中では行動せず、発射まで待ちます。
            loadedCharacter = passenger;
            var controller = passenger.GetComponent<PlayerCrewController>();
            if (controller != null) controller.enabled = false;
            passenger.transform.position = transform.position;
            return true;
        }

        /* { 処理: 装填したキャラクターを敵船へ発射します。戻り値: 発射できたらtrueです。 } */
        private bool FireCharacterAt(ShipController target)
        {
            // 目標がなければ砲台に留めたままにします。
            if (target == null) return false;
            var passenger = loadedCharacter;
            loadedCharacter = null;
            return CharacterProjectile.Launch(passenger, transform.position, target) != null;
        }

        public bool FireAt(ShipController target)
        {
            if (loadedCharacter != null) return FireCharacterAt(target);
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
