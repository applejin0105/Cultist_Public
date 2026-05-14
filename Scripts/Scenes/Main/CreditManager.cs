using Components.Effects.UI.Core;
using UnityEngine;

namespace Scenes.Main
{
    public class CreditManager : MonoBehaviour
    {
        [SerializeField] private EffectSequence sequence;

        public void OpenCreditUIEffect()
        {
            StartCoroutine(sequence.PlaySequenceRoutine());
        }

        public void CloseCreditUIEffect()
        {
            sequence.StopAll();
        }
    }
}