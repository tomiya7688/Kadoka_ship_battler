using System.Text;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 味方の方針・選択行動・評価値・予約と、敵の戦術目標・役割・傾向を、開発中の画面へ表示します。
        前提: エディターと開発ビルドだけでコンパイルします。F1キーで表示を切り替え、表示中だけ0.25秒ごとに文字列を作り直します。
              表示は既存の公開状態を読むだけで、AIの判断や状態を変更しません。
    } */
    public sealed class BattleDebugOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float RefreshSeconds = 0.25f;
        private readonly StringBuilder builder = new();
        private BattlePrototype arena;
        private string text = string.Empty;
        private float nextRefreshAt;
        private bool isVisible;

        /* { 処理: 表示対象の戦闘を取得します。 } */
        private void Awake() => arena = GetComponent<BattlePrototype>();

        /* { 処理: F1で表示を切り替え、表示中は一定間隔で内容を更新します。 } */
        private void Update()
        {
            // 非表示の間は文字列を作りません。
            if (Input.GetKeyDown(KeyCode.F1)) isVisible = !isVisible;
            if (!isVisible || Time.unscaledTime < nextRefreshAt) return;
            nextRefreshAt = Time.unscaledTime + RefreshSeconds;
            text = Build();
        }

        /* { 処理: 表示中だけ、画面右側に内容を描画します。 } */
        private void OnGUI()
        {
            if (!isVisible) return;
            GUI.Box(new Rect(Screen.width - 640, 10, 630, 520), "AI debug (F1)");
            GUI.Label(new Rect(Screen.width - 630, 35, 610, 490), text);
        }

        /* { 処理: 敵と味方の状態を一つの文字列にまとめます。戻り値: 表示用の複数行文字列です。 } */
        private string Build()
        {
            builder.Clear();
            AppendEnemy();
            builder.AppendLine();
            AppendPlayerTeam();
            return builder.ToString();
        }

        /* { 処理: 敵の共有目標、推定した傾向、船員ごとの役割を追加します。 } */
        private void AppendEnemy()
        {
            var tactics = arena.EnemyTactics;
            if (tactics == null) return;
            var tendency = tactics.Tendency;
            builder.AppendLine($"[Enemy] goal={tactics.Goal} shells/min={tendency.ShellsPerMinute:0.0} boarding={tendency.BoardingPressure:0.00} " +
                $"heavy={tendency.HeavyAmmoRatio:0.00}({tendency.SampledAmmoCount}) seen: intruders={tactics.Perception.Intruders.Count} ammo={tactics.Perception.OwnAmmo.Count}");
            foreach (var member in arena.EnemyCrew)
            {
                var controller = member.GetComponent<EnemyRoleController>();
                var role = controller != null ? tactics.GetRole(controller.MemberId) : EnemyTacticalRole.None;
                builder.AppendLine($"  {member.Definition.DisplayName}: {role} hp={member.CurrentHp:0}");
            }
        }

        /* { 処理: 味方のチーム方針と、船員ごとの個人方針、選択行動、評価値の異常、予約を追加します。 } */
        private void AppendPlayerTeam()
        {
            var policy = GetComponent<CrewPolicyDirector>();
            builder.AppendLine($"[Player] team policy={(policy != null ? policy.TeamPolicy.ToString() : "-")}");
            foreach (var actor in arena.PlayerCrew)
            {
                var utility = actor.GetComponent<UtilityAiAgent>();
                var individual = policy != null ? policy.GetIndividualPolicy(actor).ToString() : "-";
                var action = actor.IsDirectlyControlled ? "Direct" : utility.CurrentAction.ToString();
                builder.AppendLine($"  {actor.name}: {individual} / {action} {FormatReservation(actor)}{(HasInvalidScore(utility) ? " !INVALID SCORE" : string.Empty)}");
            }
        }

        /* { 処理: 船員の現在の予約を、資源の種類・ID・優先度・残り秒数で表します。予約がなければ空文字です。 } */
        private static string FormatReservation(PlayerCrewController actor)
        {
            var assignment = actor.GetComponent<CrewTaskAssignment>();
            if (assignment == null || !assignment.CurrentReservation.HasValue) return string.Empty;
            var reservation = assignment.CurrentReservation.Value;
            return $"reserve {reservation.Target.Kind}#{reservation.Target.TargetId} p{reservation.Priority} {reservation.ExpiresAt - Time.time:0.0}s";
        }

        /* { 処理: 評価値にNaNや無限大が含まれるか判定します。戻り値: 異常があればtrueです。 } */
        private static bool HasInvalidScore(UtilityAiAgent utility)
        {
            if (utility == null) return false;
            var scores = utility.Scores;
            for (var index = 0; index < scores.Count; index++)
                if (float.IsNaN(scores[index].Score) || float.IsInfinity(scores[index].Score)) return true;
            return false;
        }
#endif
    }
}
