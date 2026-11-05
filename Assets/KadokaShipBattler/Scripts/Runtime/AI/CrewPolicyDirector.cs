using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: プレイヤーチームの方針状態をUnityの船員へ接続します。
        前提: 一つのBattlePrototypeに一つだけ作成します。敵の戦術は別の接続で実装します。
    } */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AiPolicyPanel))]
    public sealed class CrewPolicyDirector : MonoBehaviour
    {
        private readonly AiPolicyState state = new();
        public BattlePrototype Arena { get; private set; }
        public AiPolicyId TeamPolicy => state.TeamPolicy;

        /* { 処理: 同じGameObject上の戦闘を取得します。前提: BattlePrototypeの生成完了後に船員が登録します。 } */
        private void Awake() => Arena = GetComponent<BattlePrototype>();

        /* { 処理: プレイヤー船員の実際の能力を登録します。戻り値: 所属違い、未初期化、重複はfalseです。 } */
        public bool Register(PlayerCrewController actor)
        {
            // 他の戦闘や敵側の設定を混入させません。
            if (actor == null || Arena == null || actor.Arena != Arena || actor.TeamSide != TeamSide.Player) return false;
            var definition = actor.GetComponent<CrewMember>().Definition;
            if (definition == null) return false;
            var abilities = new CrewPolicyAbilities(definition.Capabilities,
                definition.AttackMode != NormalAttackMode.None && definition.AttackDamage > 0,
                definition.CarryCapacity, definition.MaxCarryCount, definition.CanFly, definition.CanPhase);
            return state.Register(actor.GetInstanceID(), abilities);
        }

        /* { 処理: 戦闘中の共通方針を変更します。戻り値: 戦闘終了や不正な指定はfalseです。 } */
        public bool TrySetTeamPolicy(AiPolicyId policyId)
        {
            // 操作対象が死亡しても、残る船員へのチーム指示は継続します。
            return Arena != null && !Arena.IsFinished && state.TrySetTeamPolicy(policyId);
        }

        /* { 処理: 生存中の所属船員の個人方針を変更します。戻り値: 設定不可時はfalseです。 } */
        public bool TrySetIndividualPolicy(PlayerCrewController actor, AiPolicyId policyId)
        {
            // 設定の可否はUIと外部呼出しで一致させます。
            if (GetUnavailableReason(actor, policyId).Length != 0) return false;
            return state.TrySetIndividualPolicy(actor.GetInstanceID(), policyId);
        }

        /* { 処理: 画面と設定APIで共有する拒否理由を返します。戻り値: 選択可能なら空文字列です。 } */
        public string GetUnavailableReason(PlayerCrewController actor, AiPolicyId policyId)
        {
            // 戦闘の状態と所属を先に確認します。
            if (Arena == null || Arena.IsFinished) return "Battle finished";
            if (actor == null || actor.Arena != Arena || actor.TeamSide != TeamSide.Player) return "Player crew only";
            if (!actor.IsAvailable) return "Crew unavailable";
            return state.GetUnavailableReason(actor.GetInstanceID(), policyId);
        }

        /* { 処理: 登録船員の個人設定を取得します。前提: 同じ戦闘の登録済み船員です。 } */
        public AiPolicyId GetIndividualPolicy(PlayerCrewController actor) => state.GetIndividualPolicy(actor.GetInstanceID());

        /* { 処理: 登録船員に対する方針と戦況の重みを取得します。前提: 侵入の有無は本人または味方の新しい観測で求めます。 } */
        public float GetWeight(PlayerCrewController actor, AiActionType action, float hullFraction, bool hasObservedInvader) =>
            state.GetWeight(actor.GetInstanceID(), action, hullFraction, hasObservedInvader);
    }
}
