using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.Ammo
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class AmmoPickup : MonoBehaviour
    {
        [SerializeField] private AmmoDefinition definition;
        private bool consumed;
        private IAmmo round;
        private float lifetime;
        private bool expires;

        public AmmoDefinition Definition => definition;
        public IAmmo Round => round ?? definition;
        public bool IsConsumed => consumed;
        public int SpawnPointIndex { get; private set; } = -1;
        public float RemainingLifetime => lifetime;

        public void Initialize(AmmoDefinition ammo) => definition = ammo;

        public void Initialize(AmmoDeckRound instance, int spawnPointIndex, float groundLifetime)
        {
            if (instance == null || !(instance.Definition is AmmoDefinition ammo) || !instance.CanCarry ||
                groundLifetime <= 0 || float.IsNaN(groundLifetime) || float.IsInfinity(groundLifetime))
                throw new System.ArgumentException("Invalid deck pickup.");
            definition = ammo;
            round = instance;
            SpawnPointIndex = spawnPointIndex;
            lifetime = groundLifetime;
            expires = true;
        }

        public void TickLifetime(float deltaTime)
        {
            if (!expires || consumed || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            lifetime -= deltaTime;
            if (lifetime <= 0) Despawn();
        }

        public void Despawn()
        {
            if (consumed) return;
            if (round is ITrackedAmmo tracked) tracked.ReturnToDeck();
            consumed = true;
            GetComponent<BoxCollider2D>().enabled = false;
            Destroy(gameObject);
        }

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        public bool TryPickup(CrewAmmoInventory inventory)
        {
            if (consumed || inventory == null || !inventory.TryPickup(Round))
                return false;

            consumed = true;
            GetComponent<BoxCollider2D>().enabled = false;
            Destroy(gameObject);
            return true;
        }
        private void OnDestroy()
        {
            // Consumed pickups transfer ownership to the inventory; destroying their visual must not return the round.
            if (!consumed && round is ITrackedAmmo tracked) tracked.ReturnToDeck();
        }
    }
}
