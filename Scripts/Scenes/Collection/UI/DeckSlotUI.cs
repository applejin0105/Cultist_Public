using System;
using Components.Common.Buttons.Core;
using Data.Models;
using TMPro;
using UnityEngine;

namespace Scenes.Collection.UI
{
    public class DeckSlotUI : MonoBehaviour
    {
        [SerializeField] private CompoundButton compoundButton;

        [SerializeField] private GameObject sampleText;
        [SerializeField] private TextMeshProUGUI deckNameText;

        private DeckData _thisDeckData;
        public static Action<DeckData> OnDeckSelected;

        private void OnEnable()
        {
            compoundButton.onLeftClickEvent.AddListener(OnDeckClick);
        }

        private void OnDisable()
        {
            compoundButton.onLeftClickEvent.RemoveListener(OnDeckClick);
        }

        public void SetUpDeckData(DeckData thisDeckData)
        {
            _thisDeckData = thisDeckData;
            SetDeckUI();
        }

        private void SetDeckUI()
        {
            sampleText.SetActive(_thisDeckData.IsSample);
            deckNameText.text = _thisDeckData.deckName;
        }

        public void OnDeckClick()
        {
            OnDeckSelected?.Invoke(_thisDeckData);
        }
    }
}