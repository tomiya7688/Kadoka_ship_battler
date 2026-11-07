using KadokaShipBattler.Characters;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: チーム方針と選択船員の個人方針を変更する画面を表示します。
        前提: Pまたは画面ボタンで開閉します。戦闘を停止せず、次回AI評価に反映します。
    } */
    public sealed class AiPolicyPanel : MonoBehaviour
    {
        private CrewPolicyDirector director;
        private int selectedSlot;
        public bool IsOpen { get; set; }
        public PlayerCrewController SelectedCrew => director != null && director.Arena != null &&
            selectedSlot >= 0 && selectedSlot < director.Arena.PlayerCrew.Count ? director.Arena.PlayerCrew[selectedSlot] : null;

        /* { 処理: 同じGameObjectの方針状態を初期化後に取得します。 } */
        private void Start() => director = GetComponent<CrewPolicyDirector>();

        /* { 処理: Pキーで方針画面の開閉を切り替えます。 } */
        private void Update()
        {
            // 表示の切替だけを行い、船員の操作選択を変更しません。
            if (Input.GetKeyDown(KeyCode.P)) IsOpen = !IsOpen;
        }

        /* { 処理: 個人方針を編集する船員の枠を変更します。戻り値: 未初期化と範囲外はfalseです。 } */
        public bool TrySelectCrew(int slot)
        {
            // 画面の船員選択は、戦闘の直接操作を切り替えません。
            if (director == null || director.Arena == null || slot < 0 || slot >= director.Arena.PlayerCrew.Count) return false;
            selectedSlot = slot;
            return true;
        }

        /* { 処理: 画面から指定されたチーム方針を設定します。戻り値: 拒否時はfalseです。 } */
        public bool TrySelectTeamPolicy(AiPolicyId policyId) => director != null && director.TrySetTeamPolicy(policyId);

        /* { 処理: 選択船員へ画面で指定した個人方針を設定します。戻り値: 拒否時はfalseです。 } */
        public bool TrySelectIndividualPolicy(AiPolicyId policyId) =>
            director != null && director.TrySetIndividualPolicy(SelectedCrew, policyId);

        /* { 処理: 開閉ボタンと開いている方針画面を描画します。前提: 戦闘生成前は描画しません。 } */
        private void OnGUI()
        {
            // 戦闘の初期化完了を待って、画面内に収まる領域を作ります。
            if (director == null || director.Arena == null || director.Arena.PlayerCrew.Count == 0) return;
            if (GUI.Button(new Rect(Mathf.Max(10, Screen.width - 160), 10, 150, 30), "AI policies [P]")) IsOpen = !IsOpen;
            if (!IsOpen) return;
            var width = Mathf.Min(470, Screen.width - 20);
            GUILayout.BeginArea(new Rect(Mathf.Max(10, Screen.width - width - 10), 50, width, 410), GUI.skin.box);
            DrawTeamPolicies();
            DrawCrewSelection();
            DrawIndividualPolicies();
            GUILayout.Label("Applied on the next AI decision.\nDirect control and learned weights are preserved.");
            GUILayout.EndArea();
        }

        /* { 処理: チーム共通方針の選択ボタンを描画します。 } */
        private void DrawTeamPolicies()
        {
            // 終了後の戦闘設定を変更しません。
            GUILayout.Label("Team policy");
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !director.Arena.IsFinished;
            GUILayout.BeginHorizontal();
            foreach (var profile in AiPolicyCatalog.Team)
            {
                var label = (director.TeamPolicy == profile.Id ? "* " : "") + profile.DisplayName;
                if (GUILayout.Button(label)) TrySelectTeamPolicy(profile.Id);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;
        }

        /* { 処理: 方針を編集する船員を選びます。前提: 選択は直接操作する船員と独立です。 } */
        private void DrawCrewSelection()
        {
            // 死亡した船員の設定も表示できますが、変更は拒否します。
            GUILayout.Label("Individual policy - choose crew");
            var crew = director.Arena.PlayerCrew;
            selectedSlot = Mathf.Clamp(selectedSlot, 0, crew.Count - 1);
            GUILayout.BeginHorizontal();
            for (var slot = 0; slot < crew.Count; slot++)
            {
                var label = (selectedSlot == slot ? "* " : "") + crew[slot].GetComponent<CrewMember>().Definition.DisplayName;
                if (GUILayout.Button(label)) TrySelectCrew(slot);
            }
            GUILayout.EndHorizontal();
        }

        /* { 処理: 個人方針の選択可否と拒否理由を表示します。前提: 状態側と同じ判定APIを使います。 } */
        private void DrawIndividualPolicies()
        {
            // 禁止された方針を単に隠さず、理由を表示します。
            var actor = director.Arena.PlayerCrew[selectedSlot];
            var previousEnabled = GUI.enabled;
            foreach (var profile in AiPolicyCatalog.Individual)
            {
                var reason = director.GetUnavailableReason(actor, profile.Id);
                var label = (director.GetIndividualPolicy(actor) == profile.Id ? "* " : "") + profile.DisplayName;
                if (reason.Length != 0) label += " - " + reason;
                GUI.enabled = previousEnabled && reason.Length == 0;
                if (GUILayout.Button(label)) TrySelectIndividualPolicy(profile.Id);
            }
            GUI.enabled = previousEnabled;
        }
    }
}
