using System;
using KadokaShipBattler.Characters;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 個人方針の選択可否に必要な能力を値として保持します。
        前提: Unityオブジェクトや敵の現在状態を参照しません。
    } */
    public sealed class CrewPolicyAbilities
    {
        private readonly CharacterCapability capabilities;
        private readonly bool canAttack;
        private readonly bool canCarry;
        private readonly bool hasSpecialMobility;

        /* {
            処理: 攻撃、運搬、特殊移動の実行条件を確定します。
            引数: capacityは重量上限、maxCountは保持数上限です。
            例外: 重量上限と保持数の不正値を拒否します。
        } */
        public CrewPolicyAbilities(CharacterCapability capabilities, bool canAttack, float capacity,
            int maxCount, bool canFly, bool canPhase)
        {
            // 運搬フラグだけでは、実際に弾を保持できるとは限りません。
            const CharacterCapability supported = CharacterCapability.Combat | CharacterCapability.CarryAmmo |
                CharacterCapability.OperateCannon | CharacterCapability.BoardEnemyShip | CharacterCapability.Support;
            if ((capabilities & ~supported) != 0 || !UtilityRules.Range(capacity, 0, float.MaxValue) || maxCount < 0)
                throw new ArgumentException("Invalid policy carry limits.");
            this.capabilities = capabilities;
            this.canAttack = canAttack;
            canCarry = capacity > 0 && maxCount > 0;
            hasSpecialMobility = canFly || canPhase;
        }

        /* {
            処理: 選択不可の最初の理由を返します。
            戻り値: 全条件を満たす場合は空文字列です。
        } */
        public string GetUnavailableReason(AiPolicyProfile policy)
        {
            // 表示と設定APIで同じ能力条件を使います。
            if (policy == null) return "Unknown policy";
            if ((capabilities & policy.RequiredCapabilities) != policy.RequiredCapabilities) return "Required ability missing";
            if (policy.RequiresAttack && !canAttack) return "No attack available";
            if ((policy.RequiredCapabilities & CharacterCapability.CarryAmmo) != 0 && !canCarry) return "Cannot carry ammo";
            if (policy.RequiresMobility && !hasSpecialMobility) return "Requires flight or phase";
            return string.Empty;
        }
    }
}
