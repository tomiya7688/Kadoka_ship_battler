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

        /* {
            責務: 指定したワールド座標が、この船の甲板矩形の内側にあるか判定します。
            前提: 甲板はこのTransformの位置を中心に、localScaleを幅と高さとする矩形です。境界上は内側に含みます。
            戻り値: 内側ならtrue。座標が非有限ならfalseです。
        } */
        public bool ContainsDeckPoint(Vector2 point)
        {
            // 非有限の座標で甲板上にいると誤判定しません。
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y)) return false;
            var offset = point - (Vector2)transform.position;
            var half = (Vector2)transform.localScale * 0.5f;
            return Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.y) <= half.y;
        }
    }
}
