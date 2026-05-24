using System.Collections.Generic;
using UnityEngine;

namespace Systems.Assets
{
    public class CardAssetManager : MonoBehaviour
    {
        public static CardAssetManager Instance { get; private set; }

        private readonly Dictionary<int, Sprite> _cardIllustCache = new();
        private readonly Dictionary<int, AudioClip> _cardSoundCache = new();

        private const string IllustPathFormat = "Images/Illust";
        private const string SoundPathFormat = "Sounds/Card_Sounds";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                PreloadAndMapAssets();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void PreloadAndMapAssets()
        {
            Sprite[] allIllusts = Resources.LoadAll<Sprite>(IllustPathFormat);
            foreach (var illust in allIllusts)
            {
                if (TryParseIdFromFileName(illust.name, out int id))
                {
                    _cardIllustCache[id] = illust;
                }
            }

            AudioClip[] allSounds = Resources.LoadAll<AudioClip>(SoundPathFormat);
            foreach (var sound in allSounds)
            {
                if (TryParseIdFromFileName(sound.name, out int id))
                {
                    _cardSoundCache[id] = sound;
                }
            }

            Debug.Log($"[CardAssetManager] Asset Mapping Completed");
        }

        /// <summary>
        /// /// 파일 명에서 첫 번째 '_' 이전의 문자열을 ID로 파싱
        /// </summary>
        private bool TryParseIdFromFileName(string fileName, out int id)
        {
            id = -1;
            string[] parts = fileName.Split('_');
            if (parts.Length > 0)
            {
                return int.TryParse(parts[0], out id);
            }

            return false;
        }

        public Sprite GetSprite(int id)
        {
            _cardIllustCache.TryGetValue(id, out Sprite sprite);
            return sprite;
        }

        public AudioClip GetAudioClip(int id)
        {
            _cardSoundCache.TryGetValue(id, out AudioClip audioClip);
            return audioClip;
        }

        public void ClearCache()
        {
            _cardIllustCache.Clear();
            _cardSoundCache.Clear();
        }
    }
}