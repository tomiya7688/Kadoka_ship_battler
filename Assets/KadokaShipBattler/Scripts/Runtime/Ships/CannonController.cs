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
            // 弾としての硬さが不正な船員は、発射時に失われないよう装填しません。
            var hardness = passenger.Definition.ProjectileHardness;
            if (float.IsNaN(hardness) || float.IsInfinity(hardness) || hardness < 0) return false;
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
            // 目標がない、または味方の船なら砲台に留めたままにします。
            if (target == null || OwnerShip == null || target.TeamSide == OwnerShip.TeamSide) return false;
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
            if (IsLoaded) return crew.Can(CharacterCapability.OperateCannon) && FireAt(targetShip);
            if (TryLoadFrom(inventory)) return true;
            // 直接操作中で弾を持たない船員は、自分自身を弾として装填します。AIの自動操作では装填しません。
            var controller = crew.GetComponent<PlayerCrewController>();
            return inventory != null && !inventory.HasAmmo && controller != null && controller.IsDirectlyControlled && TryLoadCharacter(crew);
        }
        private void OnDestroy() => state.Clear();
    }
}
