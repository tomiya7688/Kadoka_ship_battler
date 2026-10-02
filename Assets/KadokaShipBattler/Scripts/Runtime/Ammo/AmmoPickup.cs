using KadokaShipBattler.Characters;
using UnityEngine;

namespace KadokaShipBattler.Ammo
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class AmmoPickup : MonoBehaviour
    {
        [SerializeField] private AmmoDefinition definition;
        private bool consumed;

        public AmmoDefinition Definition => definition;

        public void Initialize(AmmoDefinition ammo) => definition = ammo;

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        public bool TryPickup(CrewAmmoInventory inventory)
        {
            if (consumed || inventory == null || !inventory.TryPickup(definition))
                return false;

            consumed = true;
            GetComponent<BoxCollider2D>().enabled = false;
            Destroy(gameObject);
            return true;
        }
    }
}
