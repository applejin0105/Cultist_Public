using System;
using Components.Common.Buttons.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Domain.Entities;

namespace Scenes.Collection.UI
{
    public class DeckCardUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI cultistText;
        [SerializeField] private TextMeshProUGUI cardNameText;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private CompoundButton deckCardButton;

        private Card _thisCardData;
        public Action<int> OnRemoveClicked;
        public static Action<Card, bool, bool> onGlobalDeckCardHoverOn;
        public static Action onGlobalDeckCardHoverOut;

        private void OnEnable()
        {
            if (deckCardButton != null)
            {
                deckCardButton.onRightClickEvent.AddListener(OnRightClickRemove);
                deckCardButton.onPointerEnterEvent.AddListener(OnHoverOn);
                deckCardButton.onPointerExitEvent.AddListener(OnHoverOut);
            }
        }

        private void OnDisable()
        {
            if (deckCardButton != null)
            {
                deckCardButton.onRightClickEvent.RemoveListener(OnRightClickRemove);
                deckCardButton.onPointerEnterEvent.RemoveListener(OnHoverOn);
                deckCardButton.onPointerExitEvent.RemoveListener(OnHoverOut);
            }
        }

        public void Setup(Card card, int initialCount)
        {
            _thisCardData = card;

            cardNameText.text = card.Name;
            cultistText.text = card.Cultist.ToString();

            UpdateCount(initialCount);
        }

        public void UpdateCount(int count)
        {
            if (count > 1)
            {
                countText.text = $"x{count}";
                countText.gameObject.SetActive(true);
            }
            else
            {
                countText.text = "";
                countText.gameObject.SetActive(false);
            }
        }

        private void OnRightClickRemove()
        {
            OnRemoveClicked?.Invoke(_thisCardData.Id);
        }

        private void OnHoverOn()
        {
            if (_thisCardData == null) return;

            onGlobalDeckCardHoverOn?.Invoke(_thisCardData, false, true);
        }

        private void OnHoverOut()
        {
            onGlobalDeckCardHoverOut?.Invoke();
        }
    }
}