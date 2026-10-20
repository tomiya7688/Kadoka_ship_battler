using System;
using System.Collections.Generic;
using KadokaShipBattler.Characters;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 共有戦術目標を、生存している敵船員の能力に合わせた役割へ分けます。
        前提: 同じ入力から常に同じ割り当てを返します。割り当ては能力で決め、観測や位置は使いません。
    } */
    public static class EnemyRoleAssigner
    {
        public const int MaximumSuppliers = 2;
        private static readonly Comparison<EnemyCrewProfile> CompareBySlot = (left, right) => left.Slot.CompareTo(right.Slot);
        private static readonly Comparison<EnemyCrewProfile> CompareByCarry = CompareCarryThenSlot;

        /* {
            処理: 生存している船員全員に役割を一つ割り当てます。
            引数: aliveCrewは生存中の船員。resultは出力先で、実行前の内容を破棄します。
            例外: 未指定の引数、船員IDの重複を拒否します。拒否時にresultを変更しません。
        } */
        public static void Assign(IReadOnlyList<EnemyCrewProfile> aliveCrew, EnemyTacticalGoal goal,
            EnemyTacticalSituation situation, PlayerTendency tendency, Dictionary<int, EnemyTacticalRole> result)
        {
            // 検証後にまとめて書き込み、不完全な割り当てを残しません。
            if (aliveCrew == null || situation == null || result == null) throw new ArgumentNullException(nameof(aliveCrew));
            if (!Enum.IsDefined(typeof(EnemyTacticalGoal), goal)) throw new ArgumentOutOfRangeException(nameof(goal));
            var ordered = new List<EnemyCrewProfile>(aliveCrew);
            ordered.Sort(CompareBySlot);
            RequireUniqueIds(ordered);
            var roles = new Dictionary<int, EnemyTacticalRole>();
            if (goal == EnemyTacticalGoal.BoardEnemy) AssignBoarding(ordered, situation, tendency, roles);
            else AssignBombardOrDefense(ordered, goal, situation, roles);
            result.Clear();
            foreach (var pair in roles) result.Add(pair.Key, pair.Value);
        }

        /* { 処理: 乗り込み目標で、迎撃要員を残し、乗り込み可能な船員を乗り込み役にします。 } */
        private static void AssignBoarding(List<EnemyCrewProfile> ordered, EnemyTacticalSituation situation,
            PlayerTendency tendency, Dictionary<int, EnemyTacticalRole> roles)
        {
            // 侵入者や強い乗り込み傾向があれば、後方の戦闘員を自船の防衛に残します。
            var reserve = situation.ObservedIntruderCount > 0 ? 2 : tendency.BoardingPressure >= 0.5f ? 1 : 0;
            for (var index = ordered.Count - 1; index >= 0 && reserve > 0; index--)
            {
                if (!ordered[index].CanAttack) continue;
                roles[ordered[index].Id] = EnemyTacticalRole.Defender;
                reserve--;
            }
            foreach (var member in ordered)
            {
                if (roles.ContainsKey(member.Id)) continue;
                roles[member.Id] = member.CanAttack && member.Has(CharacterCapability.BoardEnemyShip) ? EnemyTacticalRole.Boarder : FallbackRole(member);
            }
        }

        /* { 処理: 砲撃または迎撃の目標で、砲手、補給役、迎撃役を割り当てます。 } */
        private static void AssignBombardOrDefense(List<EnemyCrewProfile> ordered, EnemyTacticalGoal goal,
            EnemyTacticalSituation situation, Dictionary<int, EnemyTacticalRole> roles)
        {
            // 侵入者が複数いる迎撃では、砲手も迎撃に回します。
            var gunnerId = FindGunnerId(ordered);
            var gunnerDefends = goal == EnemyTacticalGoal.DefendShip && situation.ObservedIntruderCount >= 2;
            foreach (var member in ordered)
            {
                if (member.Id != gunnerId) continue;
                roles[member.Id] = gunnerDefends && member.CanAttack ? EnemyTacticalRole.Defender : EnemyTacticalRole.Gunner;
            }
            AssignSuppliers(ordered, goal == EnemyTacticalGoal.DefendShip, roles);
            foreach (var member in ordered) if (!roles.ContainsKey(member.Id)) roles[member.Id] = FallbackRole(member);
        }

        /* { 処理: 運搬能力の高い順に、上限までの補給役を割り当てます。迎撃目標では戦闘員を補給に使いません。 } */
        private static void AssignSuppliers(List<EnemyCrewProfile> ordered, bool excludeAttackers, Dictionary<int, EnemyTacticalRole> roles)
        {
            // 戦闘員と能力が重なる場合も、同じ入力から同じ補給役を選びます。
            var candidates = new List<EnemyCrewProfile>();
            foreach (var member in ordered)
            {
                if (roles.ContainsKey(member.Id) || !member.Has(CharacterCapability.CarryAmmo) || member.CarryCapacity <= 0) continue;
                if (excludeAttackers && member.CanAttack) continue;
                candidates.Add(member);
            }
            candidates.Sort(CompareByCarry);
            for (var index = 0; index < candidates.Count && index < MaximumSuppliers; index++)
                roles[candidates[index].Id] = EnemyTacticalRole.Supplier;
        }

        /* { 処理: 砲手を決めます。戦闘・運搬・砲台操作を全て持つ最前スロットを優先し、なければ砲台操作可能な最前スロットです。 } */
        private static int FindGunnerId(List<EnemyCrewProfile> ordered)
        {
            const CharacterCapability FullGunner = CharacterCapability.Combat | CharacterCapability.CarryAmmo | CharacterCapability.OperateCannon;
            foreach (var member in ordered) if (member.Has(FullGunner)) return member.Id;
            foreach (var member in ordered) if (member.Has(CharacterCapability.OperateCannon)) return member.Id;
            return 0;
        }

        /* { 処理: 割り当て対象外の船員の既定役割を返します。攻撃できる船員は迎撃役、その他は役割なしです。 } */
        private static EnemyTacticalRole FallbackRole(EnemyCrewProfile member) =>
            member.CanAttack ? EnemyTacticalRole.Defender : EnemyTacticalRole.None;

        /* { 処理: 運搬容量の大きい順、同値ならスロット順で比較します。 } */
        private static int CompareCarryThenSlot(EnemyCrewProfile left, EnemyCrewProfile right)
        {
            var byCarry = right.CarryCapacity.CompareTo(left.CarryCapacity);
            return byCarry != 0 ? byCarry : left.Slot.CompareTo(right.Slot);
        }

        /* { 処理: 船員IDが重複していないことを確認します。例外: 重複を拒否します。 } */
        private static void RequireUniqueIds(List<EnemyCrewProfile> ordered)
        {
            // 同じ船員へ複数の役割を割り当てません。
            var seen = new HashSet<int>();
            foreach (var member in ordered)
                if (!seen.Add(member.Id)) throw new ArgumentException("Duplicate crew id.");
        }
    }
}
