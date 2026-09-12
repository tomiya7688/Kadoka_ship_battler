using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.Ships
{
    public sealed class ShipController : MonoBehaviour
    {
        [SerializeField] private TeamSide teamSide;
        [Min(1f)] [SerializeField] private float maxHull = 100f;

        public TeamSide TeamSide => teamSide;
        public float MaxHull => maxHull;
        public float CurrentHull { get; private set; }
        public bool IsDestroyed => CurrentHull <= 0f;

        private void Awake()
        {
            CurrentHull = maxHull;
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || IsDestroyed)
                return;

            CurrentHull = Mathf.Max(0f, CurrentHull - amount);
        }
    }
}
