using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.Ships
{
    public sealed class ShipController : MonoBehaviour
    {
        [SerializeField] private TeamSide teamSide;
        [Min(1f)] [SerializeField] private float maxHull = 100f;
        [Min(1f)] [SerializeField] private float maxCore = 30f;
        private ShipBattleState battleState;

        public TeamSide TeamSide => teamSide;
        public float MaxHull => maxHull;
        public ShipBattleState BattleState => battleState ??= new ShipBattleState(teamSide, maxHull, maxCore);
        public float CurrentHull => BattleState.CurrentHull;
        public float CurrentCore => BattleState.CurrentCore;
        public bool IsHullBreached => BattleState.IsHullBreached;
        public bool IsDestroyed => BattleState.IsDestroyed;

        private void Awake()
        {
            _ = BattleState;
        }

        public void Initialize(TeamSide side, float hull = 100f, float core = 30f)
        {
            teamSide = side;
            maxHull = hull;
            maxCore = core;
            battleState = new ShipBattleState(side, hull, core);
        }

        public void ApplyDamage(float amount)
        {
            BattleState.ApplyHullDamage(amount);
        }

        public bool TryDamageCore(float amount) => BattleState.TryDamageCore(amount);
    }
}
