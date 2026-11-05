using System;
using System.Collections.Generic;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 一つのチームの共通方針と船員ごとの方針を所有します。
        前提: 船員IDは戦闘内で一意です。設定と評価は同じ登録能力を使います。
    } */
    public sealed class AiPolicyState
    {
        /* {
            責務: 登録船員の能力と選択方針を関連付けます。
            前提: この状態の内部専用型のため同じファイルに置きます。
        } */
        private sealed class CrewSelection
        {
            public CrewPolicyAbilities Abilities;
            public AiPolicyProfile Policy = AiPolicyCatalog.Find(AiPolicyScope.Individual, AiPolicyId.Automatic);
        }

        private readonly Dictionary<int, CrewSelection> crewSelections = new();
        private AiPolicyProfile teamPolicy = AiPolicyCatalog.Find(AiPolicyScope.Team, AiPolicyId.Balanced);
        public AiPolicyId TeamPolicy => teamPolicy.Id;

        /* {
            処理: 船員を能力とともに一度だけ登録します。
            戻り値: 重複登録とnullの能力はfalseです。既存方針を保持します。
        } */
        public bool Register(int crewId, CrewPolicyAbilities abilities)
        {
            // 再接続によって個人設定を初期化しません。
            if (abilities == null || crewSelections.ContainsKey(crewId)) return false;
            crewSelections.Add(crewId, new CrewSelection { Abilities = abilities });
            return true;
        }

        /* { 処理: 共通方針を検証して変更します。戻り値: 未定義の方針はfalseです。 } */
        public bool TrySetTeamPolicy(AiPolicyId policyId)
        {
            // チームの指示は、全員が同じ能力を持つことを要求しません。
            var profile = AiPolicyCatalog.Find(AiPolicyScope.Team, policyId);
            if (profile == null) return false;
            teamPolicy = profile;
            return true;
        }

        /* { 処理: 個人方針の選択不可理由を求めます。戻り値: 選択可能なら空文字列です。 } */
        public string GetUnavailableReason(int crewId, AiPolicyId policyId)
        {
            // 未登録船員や別の設定対象に属する方針も拒否します。
            if (!crewSelections.TryGetValue(crewId, out var selection)) return "Crew not registered";
            return selection.Abilities.GetUnavailableReason(AiPolicyCatalog.Find(AiPolicyScope.Individual, policyId));
        }

        /* { 処理: 能力条件を満たす個人方針だけを設定します。戻り値: 拒否時は既存設定を保持してfalseです。 } */
        public bool TrySetIndividualPolicy(int crewId, AiPolicyId policyId)
        {
            // 表示側を経由しない呼出しにも同じ制限を適用します。
            if (GetUnavailableReason(crewId, policyId).Length != 0) return false;
            crewSelections[crewId].Policy = AiPolicyCatalog.Find(AiPolicyScope.Individual, policyId);
            return true;
        }

        /* { 処理: 登録船員の現在方針を取得します。例外: 未登録IDは拒否します。 } */
        public AiPolicyId GetIndividualPolicy(int crewId) => GetSelection(crewId).Policy.Id;

        /* {
            処理: チーム、個人、戦況の補正を合成します。
            引数: hullFractionは自船の残存率。hasObservedInvaderは当人が観測した自船内の敵です。
            戻り値: 0から16。緊急時にも能力や候補の生成条件を解除しません。
        } */
        public float GetWeight(int crewId, AiActionType action, float hullFraction, bool hasObservedInvader)
        {
            // 隠れた敵情報を読むことなく、自船の危機だけを補正に使います。
            if (!UtilityRules.Range(hullFraction, 0, 1)) throw new ArgumentOutOfRangeException(nameof(hullFraction));
            if ((uint)action > (uint)AiActionType.SupportAlly) throw new ArgumentOutOfRangeException(nameof(action));
            var selection = GetSelection(crewId);
            var weight = teamPolicy.GetWeight(action) * selection.Policy.GetWeight(action);
            if (action != AiActionType.DefendShip) return weight;
            var severity = hasObservedInvader ? 1 : Math.Max(0, (0.25f - hullFraction) / 0.25f);
            return Math.Min(16, weight * (1 + severity * 2));
        }

        /* { 処理: 評価対象の登録情報を取得します。例外: 未登録船員の評価を拒否します。 } */
        private CrewSelection GetSelection(int crewId)
        {
            // 中立方針に黙って置き換え、設定の欠落を隠すことを避けます。
            if (!crewSelections.TryGetValue(crewId, out var selection)) throw new ArgumentException("Crew not registered.");
            return selection;
        }
    }
}
