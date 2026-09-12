using KadokaShipBattler.Characters;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    [RequireComponent(typeof(CrewMember))]
    public sealed class UtilityAiAgent : MonoBehaviour
    {
        [SerializeField] private AiPolicyDefinition teamPolicy;
        [SerializeField] private AiPolicyDefinition characterPolicy;

        private CrewMember crewMember;

        public AiActionType CurrentAction { get; private set; } = AiActionType.Idle;

        private void Awake()
        {
            crewMember = GetComponent<CrewMember>();
        }

        public AiActionType SelectAction()
        {
            var bestAction = AiActionType.Idle;
            var bestScore = float.NegativeInfinity;

            foreach (AiActionType action in System.Enum.GetValues(typeof(AiActionType)))
            {
                if (!CanPerform(action))
                    continue;

                var score = EvaluateBaseScore(action);
                score *= GetPolicyWeight(teamPolicy, action);
                score *= GetPolicyWeight(characterPolicy, action);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestAction = action;
            }

            CurrentAction = bestAction;
            return bestAction;
        }

        private bool CanPerform(AiActionType action)
        {
            return action switch
            {
                AiActionType.CarryAmmo or AiActionType.LoadCannon => crewMember.Can(CharacterCapability.CarryAmmo),
                AiActionType.OperateCannon => crewMember.Can(CharacterCapability.OperateCannon),
                AiActionType.Repair => crewMember.Can(CharacterCapability.Repair),
                AiActionType.DefendShip => crewMember.Can(CharacterCapability.Combat),
                AiActionType.BoardEnemyShip => crewMember.Can(CharacterCapability.BoardEnemyShip),
                AiActionType.SupportAlly => crewMember.Can(CharacterCapability.Support),
                _ => true
            };
        }

        private float EvaluateBaseScore(AiActionType action)
        {
            var definition = crewMember.Definition;
            if (definition == null)
                return 0f;

            return action switch
            {
                AiActionType.CarryAmmo or AiActionType.LoadCannon => definition.CarrySkill,
                AiActionType.DefendShip or AiActionType.BoardEnemyShip => definition.CombatSkill,
                AiActionType.Repair => definition.RepairSkill,
                AiActionType.Idle => 0.1f,
                _ => 1f
            };
        }

        private float GetPolicyWeight(AiPolicyDefinition policy, AiActionType action)
        {
            if (policy == null)
                return 1f;

            if (!policy.IsAvailableFor(crewMember.Definition))
                return 1f;

            return Mathf.Max(0f, policy.GetWeight(action));
        }
    }
}
