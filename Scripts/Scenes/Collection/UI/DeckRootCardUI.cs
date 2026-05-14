using System;
using Components.Common.Buttons.Core;
using Domain.Entities;
using TMPro;
using UnityEngine;

namespace Scenes.Collection.UI
{
    public class DeckRootCardUI : MonoBehaviour
    {
        private Card _thisCardData;
        [SerializeField] private CompoundButton compoundButton;
        [SerializeField] private TextMeshProUGUI cardTitleText;

        public static Action<Card, bool, bool> onGlobalDeckRootCardHoverOn;
        public static Action onGlobalDeckRootCardHoverOut;

        private void Awake()
        {
            _thisCardData = null;
        }

        private void OnEnable()
        {
            compoundButton.onPointerEnterEvent.AddListener(OnHoverOn);
            compoundButton.onPointerExitEvent.AddListener(OnHoverOut);
        }

        private void OnDisable()
        {
            compoundButton.onPointerEnterEvent.RemoveListener(OnHoverOn);
            compoundButton.onPointerExitEvent.RemoveListener(OnHoverOut);
        }

        public bool IsRootCardExists()
        {
            return _thisCardData == null;
        }

        public void Setup(Card card)
        {
            _thisCardData = card;
            cardTitleText.text = _thisCardData.Name;
        }

        public void CleanUp()
        {
            _thisCardData = null;
            cardTitleText.text = "Root Card";
        }

        private void OnHoverOn()
        {
            if (_thisCardData == null) return;
            onGlobalDeckRootCardHoverOn?.Invoke(_thisCardData, false, true);
        }

        private void OnHoverOut()
        {
            if (_thisCardData == null) return;
            onGlobalDeckRootCardHoverOut?.Invoke();
        }
    }
}