using System;
using System.Collections.Generic;
using KadokaShipBattler.Characters;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    [CreateAssetMenu(menuName = "Kadoka Ship Battler/AI Policy", fileName = "AiPolicy")]
    public sealed class AiPolicyDefinition : ScriptableObject
    {
        [Serializable]
        public struct ActionWeight
        {
            public AiActionType action;
            public float weight;
        }

        [SerializeField] private CharacterCapability requiredCapabilities = CharacterCapability.None;
        [SerializeField] private List<ActionWeight> actionWeights = new();

        public CharacterCapability RequiredCapabilities => requiredCapabilities;

        public bool IsAvailableFor(CharacterDefinition character)
        {
            return character != null && character.HasCapability(requiredCapabilities);
        }

        public float GetWeight(AiActionType action)
        {
            for (var i = 0; i < actionWeights.Count; i++)
            {
                if (actionWeights[i].action == action)
                    return actionWeights[i].weight;
            }

            return 1f;
        }
    }
}
