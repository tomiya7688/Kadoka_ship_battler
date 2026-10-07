using System;
using System.Collections.Generic;
using KadokaShipBattler.Characters;

namespace KadokaShipBattler.AI
{
    /* {
        責務: チーム方針と個人方針の定義を一か所にまとめます。
        拡張: 定義は不変です。新しい行動の接続時に対応する方針を追加します。
    } */
    public static class AiPolicyCatalog
    {
        public static IReadOnlyList<AiPolicyProfile> Team { get; } = Array.AsReadOnly(new[]
        {
            new AiPolicyProfile(AiPolicyId.Balanced, AiPolicyScope.Team, "Balanced"),
            new AiPolicyProfile(AiPolicyId.Bombard, AiPolicyScope.Team, "Bombard", carry: 3, load: 2, fire: 3, defense: 0.5f),
            new AiPolicyProfile(AiPolicyId.Defend, AiPolicyScope.Team, "Defend ship", carry: 0.5f, load: 0.75f, fire: 0.5f, defense: 3)
        });
        public static IReadOnlyList<AiPolicyProfile> Individual { get; } = Array.AsReadOnly(new[]
        {
            new AiPolicyProfile(AiPolicyId.Automatic, AiPolicyScope.Individual, "Automatic"),
            new AiPolicyProfile(AiPolicyId.Supply, AiPolicyScope.Individual, "Supply ammo", CharacterCapability.CarryAmmo,
                carry: 3, load: 3, fire: 0.5f, defense: 0.5f),
            new AiPolicyProfile(AiPolicyId.Gunner, AiPolicyScope.Individual, "Operate cannon", CharacterCapability.OperateCannon,
                fire: 4, defense: 0.5f),
            new AiPolicyProfile(AiPolicyId.Guard, AiPolicyScope.Individual, "Guard ship", CharacterCapability.Combat,
                attack: true, carry: 0.5f, load: 0.5f, fire: 0.5f, defense: 4),
            new AiPolicyProfile(AiPolicyId.MobileSupply, AiPolicyScope.Individual, "Mobile supply", CharacterCapability.CarryAmmo,
                mobility: true, carry: 4, load: 3, fire: 0.5f, defense: 0.5f)
        });

        /* {
            処理: 設定対象と識別子が一致する定義を検索します。
            戻り値: 不明な識別子や対象の組合せはnullです。
            計算量: 定義数に比例します。現在は最大5件です。
        } */
        public static AiPolicyProfile Find(AiPolicyScope scope, AiPolicyId id)
        {
            // 別の設定対象に属する識別子を受け付けません。
            if (scope != AiPolicyScope.Team && scope != AiPolicyScope.Individual) return null;
            var profiles = scope == AiPolicyScope.Team ? Team : Individual;
            foreach (var profile in profiles)
                if (profile.Id == id) return profile;
            return null;
        }
    }
}
