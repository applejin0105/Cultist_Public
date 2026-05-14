using System.Collections.Generic;
using Components.Common.Buttons.Core;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using App.Network;

namespace Scenes.Lobby
{
    [System.Serializable]
    public class PlayerUISlot : MonoBehaviour
    {
        public GameObject slotRoot;
        public Image profileImage;
        public TextMeshProUGUI nameText;

        [Header("Deck Selection")]
        public TMP_Dropdown deckDropdown;

        public bool IsEmpty { get; private set; } = true;
        public ulong SteamId { get; private set; }

        [Header("Ready Button")]
        public CompoundButton readyButton;
        public TextMeshProUGUI readyButtonText;

        private bool _isReady = false;
        private bool _isLocalPlayer = false;

        public bool IsReady => _isReady;

        public async void Setup(ulong steamId, bool isLocalPlayer, bool isHost, List<string> deckNames)
        {
            IsEmpty = false;
            SteamId = steamId;
            _isLocalPlayer = isLocalPlayer;
            slotRoot.SetActive(true);
            
            Friend friend = new Friend(steamId);
            nameText.text = friend.Name;

            var image = await SteamFriends.GetLargeAvatarAsync(steamId);
            if (image.HasValue)
            {
                profileImage.sprite = CreateSpriteFromTexture(image.Value);
            }

            deckDropdown.onValueChanged.RemoveAllListeners();
            deckDropdown.ClearOptions();
            deckDropdown.AddOptions(deckNames);
            deckDropdown.value = 0;
            deckDropdown.RefreshShownValue();

            deckDropdown.gameObject.SetActive(isLocalPlayer);

            if (isLocalPlayer)
            {
                deckDropdown.onValueChanged.AddListener(OnDeckChanged);
                
                if (deckNames.Count > 0)
                {
                    LobbyUIManager.Instance.LocalSelectedDeckName = deckNames[0];

                    if (SteamManager.Instance.CurrentLobby.HasValue)
                    {
                        SteamManager.Instance.CurrentLobby.Value.SetMemberData("SelectedDeck", deckNames[0]);
                    }
                }
            }

            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(!isHost);
                readyButton.IsInteractable = isLocalPlayer;

                readyButton.onLeftClickEvent.RemoveListener(OnReadyClicked);
                readyButton.onLeftClickEvent.AddListener(OnReadyClicked);

                _isReady = isHost;
                UpdateReadyVisual(_isReady);
            }
        }

        public void UpdateReadyState(bool isReady)
        {
            if (readyButtonText != null)
            {
                readyButtonText.text = isReady ? "Ready" : "Waiting";
            }
        }

        private void OnReadyClicked()
        {
            _isReady = !_isReady;

            if (SteamManager.Instance.CurrentLobby.HasValue)
            {
                SteamManager.Instance.CurrentLobby.Value.SetMemberData("IsReady", _isReady.ToString());
            }

            if (Mirror.NetworkManager.singleton.transport is kcp2k.KcpTransport)
            {
                if (Mirror.NetworkClient.localPlayer != null)
                {
                    var lobbyState = Mirror.NetworkClient.localPlayer.GetComponent<LobbyPlayerState>();
                    if (lobbyState != null)
                    {
                        lobbyState.CmdSetReady(_isReady);
                    }
                }
            }

            UpdateReadyVisual(_isReady);
        }

        public void UpdateRemoteReadyState(bool isReady)
        {
            _isReady = isReady;
            UpdateReadyVisual(isReady);
        }

        private void UpdateReadyVisual(bool isReady)
        {
            if (readyButtonText != null)
            {
                readyButtonText.text = isReady ? "Ready" : "Waiting";
            }
        }

        private void OnDeckChanged(int index)
        {
            string selectedDeck = deckDropdown.options[index].text;
            Debug.Log($"선택된 덱: {selectedDeck}");

            if (_isLocalPlayer)
            {
                LobbyUIManager.Instance.LocalSelectedDeckName = selectedDeck;

                if (SteamManager.Instance.CurrentLobby.HasValue)
                {
                    SteamManager.Instance.CurrentLobby.Value.SetMemberData("SelectedDeck", selectedDeck);
                }
            }
        }

        public void UpdateRemoteDeckSelection(string deckName)
        {
            deckDropdown.onValueChanged.RemoveListener(OnDeckChanged);

            int optionIndex = deckDropdown.options.FindIndex(opt => opt.text == deckName);
            if (optionIndex != -1)
            {
                deckDropdown.value = optionIndex;
            }
            else
            {
                deckDropdown.ClearOptions();
                deckDropdown.AddOptions(new List<string> { deckName });
                deckDropdown.value = 0;
            }

            deckDropdown.onValueChanged.AddListener(OnDeckChanged);
        }

        public void Clear()
        {
            IsEmpty = true;
            SteamId = 0;
            _isReady = false;
            nameText.text = "";
            profileImage.sprite = null;
            slotRoot.SetActive(false);
            deckDropdown.onValueChanged.RemoveAllListeners();
        }

        private Sprite CreateSpriteFromTexture(Steamworks.Data.Image image)
        {
            Texture2D texture = GetTextureFromImage(image);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        private Texture2D GetTextureFromImage(Steamworks.Data.Image image)
        {
            Texture2D texture = new Texture2D((int)image.Width, (int)image.Height, TextureFormat.RGBA32, false);
            texture.LoadRawTextureData(image.Data);

            Color32[] pixels = texture.GetPixels32();
            Color32[] flippedPixels = new Color32[pixels.Length];
            int width = (int)image.Width;
            int height = (int)image.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    flippedPixels[(y * width) + x] = pixels[((height - 1 - y) * width) + x];
                }
            }

            texture.SetPixels32(flippedPixels);
            texture.Apply();
            return texture;
        }
    }
}