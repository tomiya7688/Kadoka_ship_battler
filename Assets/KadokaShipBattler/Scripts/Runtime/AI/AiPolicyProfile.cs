using System;
using KadokaShipBattler.Characters;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 方針の識別子と設定対象を表します。
        前提: 小さな列挙型は、直接使用する定義型と同じファイルに置きます。
    } */
    public enum AiPolicyId { Balanced, Bombard, Defend, Automatic, Supply, Gunner, Guard, MobileSupply }
    /* { 責務: 定義をチーム用と個人用に区別します。前提: 定義に従属する小さな型です。 } */
    public enum AiPolicyScope { Team, Individual }

    /* {
        責務: 方針の表示、必要能力、行動別の重みを不変データとして保持します。
        前提: 重みは有限の0から4。指定しない行動の重みは1です。
    } */
    public sealed class AiPolicyProfile
    {
        public AiPolicyId Id { get; }
        public AiPolicyScope Scope { get; }
        public string DisplayName { get; }
        public CharacterCapability RequiredCapabilities { get; }
        public bool RequiresAttack { get; }
        public bool RequiresMobility { get; }
        private readonly float carryWeight;
        private readonly float loadWeight;
        private readonly float fireWeight;
        private readonly float defenseWeight;

        /* {
            処理: 定義の識別子、表示、重みを検証して保持します。
            引数: capabilitiesは必要能力。attackとmobilityは追加条件です。
            例外: 不正な列挙値、空の表示名、範囲外の重みを拒否します。
        } */
        public AiPolicyProfile(AiPolicyId id, AiPolicyScope scope, string displayName,
            CharacterCapability capabilities = CharacterCapability.None, bool attack = false, bool mobility = false,
            float carry = 1, float load = 1, float fire = 1, float defense = 1)
        {
            // 無効な定義を設定画面や評価関数へ渡しません。
            const CharacterCapability supported = CharacterCapability.Combat | CharacterCapability.CarryAmmo |
                CharacterCapability.OperateCannon | CharacterCapability.BoardEnemyShip | CharacterCapability.Support;
            if (!Enum.IsDefined(typeof(AiPolicyId), id) || !Enum.IsDefined(typeof(AiPolicyScope), scope) ||
                (capabilities & ~supported) != 0 ||
                string.IsNullOrWhiteSpace(displayName) || !UtilityRules.Range(carry, 0, 4) ||
                !UtilityRules.Range(load, 0, 4) || !UtilityRules.Range(fire, 0, 4) || !UtilityRules.Range(defense, 0, 4))
                throw new ArgumentException("Invalid policy definition.");
            Id = id;
            Scope = scope;
            DisplayName = displayName;
            RequiredCapabilities = capabilities;
            RequiresAttack = attack;
            RequiresMobility = mobility;
            carryWeight = carry;
            loadWeight = load;
            fireWeight = fire;
            defenseWeight = defense;
        }

        /* {
            処理: 対象の行動に対応する固定の補正値を返します。
            戻り値: 未対応の行動は中立の1です。能力制限を解除しません。
        } */
        public float GetWeight(AiActionType action)
        {
            // 未実装の行動にも誤った優先順位を付けません。
            return action switch
            {
                AiActionType.CarryAmmo => carryWeight,
                AiActionType.LoadCannon => loadWeight,
                AiActionType.OperateCannon => fireWeight,
                AiActionType.DefendShip => defenseWeight,
                _ => 1
            };
        }
    }
}
