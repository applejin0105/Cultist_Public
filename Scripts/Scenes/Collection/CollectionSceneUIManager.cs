using System.Collections.Generic;
using Components.Common.Buttons.Core;
using Data.Models;
using Data.Repositories;
using Domain.Entities;
using Scenes.Collection.UI;
using TMPro;
using UI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scenes.Collection
{
    public class CollectionSceneUIManager : MonoBehaviour
    {
        private DeckRepository _deckRepository;
        private Dictionary<string, DeckData> _allDecks;

        [Header("Managers")]
        [SerializeField] private DeckEditorController deckEditorController;

        [Header("UI Elements")]
        [SerializeField] GameObject deckList;
        [SerializeField] GameObject deckPrefab;
        [SerializeField] CompoundButton exitDetailButton;

        [Header("Detail Card UI")]
        [SerializeField] GameObject detailCardRaycastPanel;
        [SerializeField] GameObject descriptionPanel;
        [SerializeField] TextMeshProUGUI descriptionText;
        [SerializeField] GameObject detailCard;
        private CardUIManager _detailCardUIManager;

        private bool _isDetailCardActive;

        private void Awake()
        {
            _isDetailCardActive = false;
            _detailCardUIManager = detailCard.GetComponent<CardUIManager>();
        }

        private void OnEnable()
        {
            CardUIManager.onGlobalCardClickedAction += ShowCardDetailUI;

            DeckCardUI.onGlobalDeckCardHoverOn += ShowCardDetailUI;
            DeckCardUI.onGlobalDeckCardHoverOut += HideCardDetailUI;

            DeckRootCardUI.onGlobalDeckRootCardHoverOn += ShowCardDetailUI;
            DeckRootCardUI.onGlobalDeckRootCardHoverOut += HideCardDetailUI;

            if (exitDetailButton != null)
                exitDetailButton.onLeftClickEvent.AddListener(HideCardDetailUI);
        }

        private void OnDisable()
        {
            CardUIManager.onGlobalCardClickedAction -= ShowCardDetailUI;

            DeckCardUI.onGlobalDeckCardHoverOn -= ShowCardDetailUI;
            DeckCardUI.onGlobalDeckCardHoverOut -= HideCardDetailUI;

            DeckRootCardUI.onGlobalDeckRootCardHoverOn -= ShowCardDetailUI;
            DeckRootCardUI.onGlobalDeckRootCardHoverOut -= HideCardDetailUI;

            if (exitDetailButton != null)
                exitDetailButton.onLeftClickEvent.RemoveListener(HideCardDetailUI);
        }

        private async void Start()
        {
            await CardCatalog.InitializeAsync();
            _deckRepository = new DeckRepository(CardCatalog.Instance);

            if (deckEditorController != null)
            {
                deckEditorController.Initialize(_deckRepository);

                deckEditorController.OnDeckSavedSuccessfully += RefreshDeckList;
                deckEditorController.OnExitEditModeAction += HideCardDetailUI;
            }

            RefreshDeckList();
        }

        private async void RefreshDeckList()
        {
            foreach (Transform child in deckList.transform)
            {
                Destroy(child.gameObject);
            }

            _allDecks = await _deckRepository.LoadAllDecksAsync();

            AddDeckToUI();
        }

        private void AddDeckToUI()
        {
            if (_allDecks == null || _allDecks.Count == 0) return;
            AddSampleDeck();
            AddPlayerDeck();
        }

        private void AddSampleDeck()
        {
            foreach (var kvp in _allDecks)
            {
                DeckData deckData = kvp.Value;
                if (deckData.IsSample)
                {
                    CreateDeckUI(deckData);
                }
            }
        }

        private void AddPlayerDeck()
        {
            foreach (var kvp in _allDecks)
            {
                DeckData deckData = kvp.Value;
                if (!deckData.IsSample)
                {
                    CreateDeckUI(deckData);
                }
            }
        }

        private void CreateDeckUI(DeckData deckData)
        {
            GameObject deckInstance = Instantiate(deckPrefab, deckList.transform);

            DeckSlotUI deckSlotUI = deckInstance.GetComponent<DeckSlotUI>();

            if (deckEditorController != null)
            {
                deckSlotUI.SetUpDeckData(deckData);
            }
        }

        private void ShowCardDetailUI(Card card, bool isLeftClick, bool isDeckEdit = false)
        {
            if (isLeftClick || card == null) return;

            if (!isDeckEdit)
            {
                detailCardRaycastPanel.SetActive(true);
            }

            _isDetailCardActive = true;
            detailCard.SetActive(true);
            descriptionPanel.SetActive(true);

            descriptionText.text = card.Description;
            _detailCardUIManager.SetupCard(card);
        }

        public void HideCardDetailUI()
        {
            _detailCardUIManager.CleanUp();
            descriptionText.text = null;

            _isDetailCardActive = false;
            detailCard.SetActive(false);
            descriptionPanel.SetActive(false);

            if (detailCardRaycastPanel.activeSelf)
                detailCardRaycastPanel.SetActive(false);
        }

        public void ExitCollectionScene()
        {
            SceneManager.LoadScene("01_Main");
        }
    }
}