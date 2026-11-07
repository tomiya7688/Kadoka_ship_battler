using System;
using System.Collections.Generic;
using KadokaShipBattler.Characters;

namespace KadokaShipBattler.AI
{
    public sealed class UtilityContext
    {
        public CharacterCapability Capabilities { get; }
        public float CombatSkill { get; }
        public float CarrySkill { get; }
        public float HpFraction { get; }
        public float HullFraction { get; }
        public float WaitingFraction { get; }
        public bool HasAmmo { get; }
        public bool CannonLoaded { get; }
        public bool CanAttack { get; }
        public UtilityContext(CharacterCapability capabilities, float combatSkill, float carrySkill, float hpFraction,
            float hullFraction, float waitingFraction, bool hasAmmo, bool cannonLoaded, bool canAttack)
        {
            if (!UtilityRules.Range(combatSkill, 0, 10) || !UtilityRules.Range(carrySkill, 0, 10) ||
                !UtilityRules.Range(hpFraction, 0, 1) || !UtilityRules.Range(hullFraction, 0, 1) || !UtilityRules.Range(waitingFraction, 0, 1))
                throw new ArgumentException("Invalid utility context.");
            Capabilities = capabilities; CombatSkill = combatSkill; CarrySkill = carrySkill; HpFraction = hpFraction;
            HullFraction = hullFraction; WaitingFraction = waitingFraction; HasAmmo = hasAmmo; CannonLoaded = cannonLoaded; CanAttack = canAttack;
        }
    }
    // Values sampled from perception or own known map. No live target references.
    public sealed class UtilityCandidate
    {
        public AiActionType Action { get; }
        public int TargetId { get; }
        public float X { get; }
        public float Y { get; }
        public float Distance { get; }
        public float Danger { get; }
        public float AmmoDamage { get; }
        public float AmmoWeight { get; }
        public float TargetHp { get; }
        public UtilityCandidate(AiActionType action, int targetId = 0, float x = 0, float y = 0, float distance = 0,
            float danger = 0, float ammoDamage = 0, float ammoWeight = 0, float targetHp = 0)
        {
            if (!Enum.IsDefined(typeof(AiActionType), action) || !UtilityRules.Finite(x) || !UtilityRules.Finite(y) ||
                !UtilityRules.Range(distance, 0, float.MaxValue) || !UtilityRules.Range(danger, 0, 1) ||
                !UtilityRules.Range(ammoDamage, 0, float.MaxValue) || !UtilityRules.Range(ammoWeight, 0, float.MaxValue) ||
                !UtilityRules.Range(targetHp, 0, float.MaxValue)) throw new ArgumentException("Invalid utility candidate.");
            Action = action; TargetId = targetId; X = x; Y = y; Distance = distance; Danger = danger;
            AmmoDamage = ammoDamage; AmmoWeight = ammoWeight; TargetHp = targetHp;
        }
    }
    [Serializable] public sealed class UtilityWeightData { public AiActionType action; public float weight; }
    [Serializable] public sealed class UtilityLearningData { public UtilityWeightData[] weights; }
    public sealed class UtilityLearningState
    {
        private readonly Dictionary<AiActionType, float> weights = new();
        public float GetWeight(AiActionType action) => weights.TryGetValue(action, out var weight) ? weight : 1;
        public void SetWeight(AiActionType action, float weight)
        {
            if (!UtilityRules.IsExecutable(action) || !UtilityRules.Range(weight, 0.25f, 4)) throw new ArgumentException("Invalid learned action weight.");
            weights[action] = weight;
        }
        public void RecordOutcome(AiActionType action, float reward, float learningRate = 0.15f)
        {
            if (!UtilityRules.Range(reward, -1, 1) || !UtilityRules.Range(learningRate, 0, 1)) throw new ArgumentException("Invalid learning update.");
            var target = reward >= 0 ? 1 + reward * 3 : 1 + reward * 0.75f;
            SetWeight(action, GetWeight(action) + learningRate * (target - GetWeight(action)));
        }
        public UtilityLearningData Export()
        {
            var entries = new List<UtilityWeightData>();
            foreach (AiActionType action in Enum.GetValues(typeof(AiActionType)))
                if (UtilityRules.IsExecutable(action)) entries.Add(new UtilityWeightData { action = action, weight = GetWeight(action) });
            return new UtilityLearningData { weights = entries.ToArray() };
        }
        public void Import(UtilityLearningData data)
        {
            if (data?.weights == null) throw new ArgumentException("Learning weights are required.");
            var replacement = new UtilityLearningState(); var unique = new HashSet<AiActionType>();
            foreach (var entry in data.weights)
            {
                if (entry == null || !unique.Add(entry.action)) throw new ArgumentException("Null or duplicate learning weight.");
                replacement.SetWeight(entry.action, entry.weight);
            }
            weights.Clear(); foreach (var entry in replacement.weights) weights.Add(entry.Key, entry.Value);
        }
    }
    public sealed class UtilityScore
    {
        public UtilityCandidate Candidate { get; }
        public float BaseScore { get; }
        public float LearnedWeight { get; }
        public float PolicyWeight { get; }
        public float Score { get; }
        public UtilityScore(UtilityCandidate candidate, float score, float learned, float policy)
        { Candidate = candidate; BaseScore = score; LearnedWeight = learned; PolicyWeight = policy; Score = score * learned * policy; }
    }
    public sealed class UtilityDecision
    {
        public UtilityCandidate Selected { get; }
        public IReadOnlyList<UtilityScore> Scores { get; }
        public UtilityDecision(UtilityCandidate selected, IReadOnlyList<UtilityScore> scores) { Selected = selected; Scores = scores; }
    }
    public static class UtilityRules
    {
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Range(float value, float min, float max) => Finite(value) && value >= min && value <= max;
        public static bool IsExecutable(AiActionType action) => action == AiActionType.Idle || action == AiActionType.CarryAmmo ||
            action == AiActionType.LoadCannon || action == AiActionType.OperateCannon || action == AiActionType.DefendShip;
        private static bool CanPerform(UtilityContext context, AiActionType action)
        {
            bool Has(CharacterCapability capability) => (context.Capabilities & capability) == capability;
            return context.HpFraction > 0 && (action switch
            {
                AiActionType.Idle => true,
                AiActionType.CarryAmmo => Has(CharacterCapability.CarryAmmo),
                AiActionType.LoadCannon => Has(CharacterCapability.CarryAmmo) && context.HasAmmo && !context.CannonLoaded,
                AiActionType.OperateCannon => Has(CharacterCapability.OperateCannon) && context.CannonLoaded,
                AiActionType.DefendShip => Has(CharacterCapability.Combat) && context.CanAttack,
                _ => false
            });
        }
        public static UtilityDecision Evaluate(UtilityContext context, IEnumerable<UtilityCandidate> candidates,
            UtilityLearningState learning, Func<AiActionType, float> policyWeight = null)
        {
            if (context == null || candidates == null || learning == null) throw new ArgumentNullException();
            var scores = new List<UtilityScore>(); UtilityCandidate best = new UtilityCandidate(AiActionType.Idle);
            var bestScore = 0f;
            foreach (var candidate in candidates)
            {
                if (candidate == null) throw new ArgumentException("Null action candidate.");
                if (!CanPerform(context, candidate.Action)) continue;
                var distance = 1 + candidate.Distance * 0.2f;
                var baseScore = candidate.Action switch
                {
                    AiActionType.Idle => 0.1f,
                    AiActionType.CarryAmmo => (2 + context.CarrySkill) * (1 + Math.Min(candidate.AmmoDamage, 100) / 100) *
                        (0.5f + context.WaitingFraction * 0.5f) * (1 - candidate.Danger * 0.65f) * (0.5f + context.HpFraction * 0.5f) /
                        (distance * (1 + candidate.AmmoWeight * 0.06f)),
                    AiActionType.LoadCannon => (14 + context.CarrySkill) * (1 - candidate.Danger * 0.3f) / distance,
                    AiActionType.OperateCannon => 26 * (1 - candidate.Danger * 0.3f) / distance,
                    AiActionType.DefendShip => (3 + context.CombatSkill) * (1 + (1 - context.HullFraction) * 0.8f) *
                        (1 + 0.6f / (1 + candidate.TargetHp * 0.02f)) * (0.4f + context.HpFraction * 0.6f) / distance,
                    _ => 0
                };
                var policy = policyWeight == null ? 1 : policyWeight(candidate.Action);
                if (!Range(policy, 0, 16)) throw new ArgumentException("Invalid policy weight.");
                var score = new UtilityScore(candidate, baseScore, learning.GetWeight(candidate.Action), policy); scores.Add(score);
                if (score.Score > bestScore || score.Score == bestScore &&
                    ((int)candidate.Action < (int)best.Action || candidate.Action == best.Action && candidate.TargetId < best.TargetId))
                { best = candidate; bestScore = score.Score; }
            }
            return new UtilityDecision(best, scores.AsReadOnly());
        }
    }
}
