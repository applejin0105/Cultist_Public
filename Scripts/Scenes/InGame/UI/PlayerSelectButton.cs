using Components.Common.Buttons.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;

namespace Scenes.InGame.UI
{
    public class PlayerSelectButton : MonoBehaviour
    {
        [SerializeField] private CompoundButton selectButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI playerNameText;

        private int _targetSeatIndex;
        public int TargetSeatIndex => _targetSeatIndex;
        private System.Action<int> _onClickCallback;

        public void Setup(string playerName, int seatIndex, ulong steamId, System.Action<int> onClickCallback)
        {
            _targetSeatIndex = seatIndex;
            _onClickCallback = onClickCallback;

            if (playerNameText != null)
            {
                playerNameText.text = playerName;
            }

            selectButton.onLeftClickEvent.RemoveAllListeners();
            selectButton.onLeftClickEvent.AddListener(OnButtonClicked);

            if (steamId != 0) LoadSteamAvatar(steamId);
        }

        public void UpdateData(string newName, ulong steamId)
        {
            Debug.Log(
                $"[PlayerSelectButton] UpdateData 호출됨. 타겟 시트: {_targetSeatIndex}, 수신 이름: {newName}, 참조 상태: {playerNameText != null}");

            if (playerNameText != null)
            {
                playerNameText.text = newName;
                playerNameText.ForceMeshUpdate(); // 강제 UI 렌더링 갱신
                Debug.Log($"[PlayerSelectButton] 텍스트 갱신 완료. 현재 UI 텍스트: {playerNameText.text}");
            }
            else
            {
                Debug.LogError($"[PlayerSelectButton] 에러: 타겟 시트 {_targetSeatIndex}의 playerNameText 참조가 끊어졌습니다.");
            }

            if (steamId != 0) LoadSteamAvatar(steamId);
        }

        private void OnButtonClicked()
        {
            _onClickCallback?.Invoke(_targetSeatIndex);
        }

        private async void LoadSteamAvatar(ulong steamId)
        {
            if (steamId == 0 || !Steamworks.SteamClient.IsValid) return;

            var image = await SteamFriends.GetLargeAvatarAsync(steamId);
            if (image.HasValue)
            {
                iconImage.sprite = CreateSpriteFromTexture(image.Value);
            }
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
            texture.Apply();
            return texture;
        }
    }
}