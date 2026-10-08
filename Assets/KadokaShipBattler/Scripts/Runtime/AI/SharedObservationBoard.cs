using System;
using System.Collections.Generic;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 所属船員が実際に観測した値だけを、有効期限付きで共有します。
        前提: 対象への生存参照を保持しません。報告から消えた対象は、その報告者の共有から直ちに除外します。
        計算量: 更新は報告件数に比例します。統合は変更時または時刻変更時だけ行います。
    } */
    public sealed class SharedObservationBoard
    {
        public const float ObservationLifetime = 2;
        private const int MaximumTargetsPerReporter = 128;
        private static readonly Comparison<VisionObservation> CompareTargets = CompareTargetIds;
        private readonly TeamSide side;
        private readonly Dictionary<int, Dictionary<int, VisionObservation>> reports = new();
        private readonly Dictionary<int, VisionObservation> newestByTarget = new();
        private readonly Dictionary<int, int> latestReporterIds = new();
        private readonly List<VisionObservation> merged = new();
        private readonly IReadOnlyList<VisionObservation> mergedView;
        private bool isDirty = true;
        private float mergedAt = -1;

        /* { 処理: 共有する陣営と再利用する閲覧用リストを設定します。例外: 不明な陣営を拒否します。 } */
        public SharedObservationBoard(TeamSide side)
        {
            // 相手陣営の報告は登録時に拒否します。
            if (side != TeamSide.Player && side != TeamSide.Enemy) throw new ArgumentException("Unknown team.");
            this.side = side;
            mergedView = merged.AsReadOnly();
        }

        /* { 処理: 所属する報告者を登録します。戻り値: 別陣営、未指定、重複はfalseです。 } */
        public bool RegisterReporter(int reporterId, TeamSide reporterSide)
        {
            // 戦闘の接続側が所属を確認した船員だけを受け付けます。
            if (reporterId == 0 || reporterSide != side || reports.ContainsKey(reporterId)) return false;
            reports.Add(reporterId, new Dictionary<int, VisionObservation>());
            return true;
        }

        /* {
            処理: 報告者の現在の観測へ置き換えます。
            引数: observationsは現在の本人の視界から得た不変の値です。SeenAtの期限を延長しません。
            例外: 未登録の報告者や不正な観測は、既存の報告を保持して拒否します。
        } */
        public void Publish(int reporterId, IReadOnlyList<VisionObservation> observations, float now)
        {
            // 全入力を検証してから、既存の報告を変更します。
            ValidateTime(now);
            if (!reports.TryGetValue(reporterId, out var report)) throw new ArgumentException("Reporter not registered.");
            if (observations == null || observations.Count > MaximumTargetsPerReporter) throw new ArgumentException("Invalid observation list.");
            for (var index = 0; index < observations.Count; index++) ValidateObservation(observations[index], now);

            // 辞書は再利用します。既存の観測値の共有には新しい対象参照を作りません。
            report.Clear();
            for (var index = 0; index < observations.Count; index++)
            {
                var observation = observations[index];
                if (observation.SeenAt + ObservationLifetime <= now) continue;
                if (!report.TryGetValue(observation.TargetId, out var previous) || observation.SeenAt > previous.SeenAt)
                    report[observation.TargetId] = observation;
            }
            isDirty = true;
        }

        /* { 処理: 現在の有効な共有観測を取得します。戻り値: 次の統合まで有効な、変更不可の閲覧用リストです。 } */
        public IReadOnlyList<VisionObservation> GetCurrent(float now)
        {
            // 同じフレームの同じ状態を繰り返し統合しません。
            ValidateTime(now);
            if (!isDirty && mergedAt == now) return mergedView;
            newestByTarget.Clear();
            latestReporterIds.Clear();
            foreach (var report in reports)
                foreach (var observation in report.Value.Values)
                {
                    if (observation.SeenAt + ObservationLifetime <= now) continue;
                    if (!newestByTarget.TryGetValue(observation.TargetId, out var previous) || observation.SeenAt > previous.SeenAt ||
                        observation.SeenAt == previous.SeenAt && report.Key < latestReporterIds[observation.TargetId])
                    {
                        newestByTarget[observation.TargetId] = observation;
                        latestReporterIds[observation.TargetId] = report.Key;
                    }
                }
            merged.Clear();
            foreach (var observation in newestByTarget.Values) merged.Add(observation);
            merged.Sort(CompareTargets);
            mergedAt = now;
            isDirty = false;
            return mergedView;
        }

        /* { 処理: 報告者が行動不能や無効になった時に、その観測を解除します。 } */
        public void ClearReporter(int reporterId)
        {
            // 登録は保持するため、操作再開やセンサー再有効化時に再利用します。
            if (!reports.TryGetValue(reporterId, out var report) || report.Count == 0) return;
            report.Clear();
            isDirty = true;
        }

        /* { 処理: 破棄した船員の報告と登録を除去します。 } */
        public void UnregisterReporter(int reporterId)
        {
            // 別のIDで船員を再生成しても、不要な辞書を蓄積しません。
            isDirty |= reports.Remove(reporterId);
        }

        /* { 処理: 取得、死亡、再観測の失敗で判明した無効対象を全ての報告から除きます。 } */
        public void ForgetTarget(int targetId)
        {
            // 他の古い報告から同じ対象がすぐに復活しないようにします。
            foreach (var report in reports.Values) isDirty |= report.Remove(targetId);
        }

        /* { 処理: 終了した戦闘の観測を全て解除します。 } */
        public void Clear()
        {
            // 外部から保持された閲覧リストも空にします。
            foreach (var report in reports.Values) report.Clear();
            newestByTarget.Clear();
            merged.Clear();
            isDirty = true;
        }

        /* { 処理: 同じ候補を決定的な対象ID順で返すため比較します。 } */
        private static int CompareTargetIds(VisionObservation left, VisionObservation right) => left.TargetId.CompareTo(right.TargetId);

        /* { 処理: 観測値と時刻の範囲を検証します。例外: 将来時刻、非有限値、不明な種類や陣営を拒否します。 } */
        private static void ValidateObservation(VisionObservation observation, float now)
        {
            // 不正な報告によって味方の評価状態を破綻させません。
            if (observation == null || observation.TargetId == 0 || (uint)observation.Kind > (uint)ObservedTargetKind.Core ||
                observation.TeamSide.HasValue && observation.TeamSide != TeamSide.Player && observation.TeamSide != TeamSide.Enemy ||
                observation.Kind != ObservedTargetKind.Ammo && !observation.TeamSide.HasValue ||
                !UtilityRules.Finite(observation.X) || !UtilityRules.Finite(observation.Y) ||
                !UtilityRules.Range(observation.Hp, 0, float.MaxValue) || !UtilityRules.Range(observation.AmmoWeight, 0, float.MaxValue) ||
                !UtilityRules.Range(observation.AmmoDamage, 0, float.MaxValue) || !UtilityRules.Range(observation.SeenAt, 0, now))
                throw new ArgumentException("Invalid shared observation.");
        }

        /* { 処理: 有効期限の比較に使う時刻を検証します。 } */
        private static void ValidateTime(float now)
        {
            // NaNによって期限が無期限になることを防ぎます。
            if (!UtilityRules.Range(now, 0, float.MaxValue)) throw new ArgumentOutOfRangeException(nameof(now));
        }
    }
}
