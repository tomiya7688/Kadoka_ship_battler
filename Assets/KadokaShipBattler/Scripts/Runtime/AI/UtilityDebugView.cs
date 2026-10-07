using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: AIの評価内訳と選択地点を開発中の画面へ表示します。
        前提: 評価処理から表示を分離します。リリースでは表示処理をコンパイルしません。
    } */
    public sealed class UtilityDebugView : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private UtilityAiAgent utility;
        private Characters.PlayerCrewController actor;

        /* { 処理: 生成時のコンポーネント順序に依存せず、開始時に表示対象を取得します。 } */
        private void Start()
        {
            // RequireComponentによる追加中は、UtilityAiAgentが存在するとは限りません。
            utility = GetComponent<UtilityAiAgent>();
            actor = GetComponent<Characters.PlayerCrewController>();
        }

        /* { 処理: 明示的に有効にした船員の上位評価を表示します。 } */
        private void OnGUI()
        {
            // 通常の実行時には文字列の生成やソートを行いません。
            if (utility == null || !utility.IsDebugVisible || utility.Decision == null || actor.IsDirectlyControlled) return;
            GUI.Label(new Rect(10, 220 + GetCrewSlot() * 45, 1100, 45), $"{name}: {utility.CurrentAction}\n{FormatTopScores()}");
        }

        /* { 処理: 表示位置に使用する自船編成の枠番号を求めます。戻り値: 編成外は0です。 } */
        private int GetCrewSlot()
        {
            // 表示のために編成リストを複製しません。
            var crew = actor.Arena.PlayerCrew;
            for (var slot = 0; slot < crew.Count; slot++)
                if (crew[slot] == actor) return slot;
            return 0;
        }

        /* {
            処理: 上位3候補の評価内訳を表示用の文字列にします。
            計算量: 候補数に比例する3回の走査です。開発用表示を有効にした時だけ実行します。
        } */
        private string FormatTopScores()
        {
            // ソート用の一時リストを作らず、同点は元の候補順を保ちます。
            var scores = utility.Scores;
            var previousIndex = -1;
            var previousValue = float.PositiveInfinity;
            var text = string.Empty;
            for (var rank = 0; rank < 3; rank++)
            {
                var bestIndex = -1;
                var bestValue = float.NegativeInfinity;
                for (var index = 0; index < scores.Count; index++)
                {
                    var value = scores[index].Score;
                    if (value > previousValue || value == previousValue && index <= previousIndex || value <= bestValue) continue;
                    bestIndex = index;
                    bestValue = value;
                }
                if (bestIndex < 0) break;
                var score = scores[bestIndex];
                text += $"{score.Candidate.Action}#{score.Candidate.TargetId}: {score.BaseScore:0.0}x{score.LearnedWeight:0.00}x{score.PolicyWeight:0.00}={score.Score:0.0}  ";
                previousIndex = bestIndex;
                previousValue = bestValue;
            }
            return text;
        }

        /* { 処理: 選択船員の目的地までSceneビューに線を表示します。 } */
        private void OnDrawGizmosSelected()
        {
            // 編集中や判断前には目的地がないため描画しません。
            if (utility == null || utility.Decision == null || utility.CurrentAction == AiActionType.Idle) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, new Vector3(utility.Decision.Selected.X, utility.Decision.Selected.Y));
        }
#endif
    }
}
