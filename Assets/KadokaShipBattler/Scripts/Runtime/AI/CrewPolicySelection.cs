using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 船員の評価処理を所属チームの方針状態に接続します。
        前提: Arenaはコンポーネント追加後に設定されるため、接続は初期化完了後に行います。
    } */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCrewController))]
    public sealed class CrewPolicySelection : MonoBehaviour
    {
        private PlayerCrewController actor;
        public CrewPolicyDirector Director { get; private set; }

        /* { 処理: 船員の操作コンポーネントを取得します。 } */
        private void Awake() => actor = GetComponent<PlayerCrewController>();

        /* { 処理: 戦闘生成後に方針画面を使えるよう登録します。 } */
        private void Start() => EnsureConnected();

        /* { 処理: 登録を確保し、評価に必要な方針の重みを返します。戻り値: 対象外の船員は中立の1です。 } */
        public float GetWeight(AiActionType action, float hullFraction, bool hasObservedInvader)
        {
            // 別陣営の戦術をプレイヤーの方針で上書きしません。
            EnsureConnected();
            return Director != null ? Director.GetWeight(actor, action, hullFraction, hasObservedInvader) : 1;
        }

        /* {
            処理: 所属戦闘に一つの方針状態を作り、船員を一度だけ登録します。
            副作用: 戦闘GameObjectへ方針状態と画面を追加します。毎評価での再生成は行いません。
        } */
        private void EnsureConnected()
        {
            // Awake中の未設定Arenaを利用しません。
            if (Director != null || actor.Arena == null || actor.TeamSide != TeamSide.Player) return;
            Director = actor.Arena.GetComponent<CrewPolicyDirector>();
            if (Director == null) Director = actor.Arena.gameObject.AddComponent<CrewPolicyDirector>();
            Director.Register(actor);
        }
    }
}
