using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Utils;

namespace Data.Models
{
    /// <summary>
    /// 단일 cardsEffects.json 만 로드한다.
    /// 옛 commands.json / conditions.json / triggers.json 은 폐지 (코드에 명시 등록).
    /// </summary>
    public sealed class EffectRegistry
    {
        public static EffectRegistry Instance { get; private set; }

        private Dictionary<int, Dictionary<string, JArray>> _effects
            = new Dictionary<int, Dictionary<string, JArray>>();

        public static async Task InitializeAsync()
        {
            if (Instance != null) return;

            var registry = new EffectRegistry();
            await registry.LoadAsync();
            Instance = registry;

            Debug.Log($"[EffectRegistry] 카드 효과 {registry._effects.Count}장 로드");
        }

        private async Task LoadAsync()
        {
            string path = PathConstants.CardEffectsTargetFilePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[EffectRegistry] 파일 없음: {path}");
                return;
            }

            string json = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                _effects = JsonConvert.DeserializeObject<Dictionary<int, Dictionary<string, JArray>>>(json)
                           ?? new Dictionary<int, Dictionary<string, JArray>>();
            }
            catch (JsonException e)
            {
                Debug.LogError($"[EffectRegistry] 파싱 실패: {e.Message}");
                _effects = new Dictionary<int, Dictionary<string, JArray>>();
            }
        }

        /// <summary>cardId의 trigger에 매칭되는 명령 시퀀스를 반환. 없으면 null.</summary>
        public JArray GetTrigger(int cardId, string trigger)
        {
            if (_effects.TryGetValue(cardId, out var triggers)
                && triggers.TryGetValue(trigger, out var nodes))
            {
                return nodes;
            }
            return null;
        }

        /// <summary>카드에 정의된 패시브 목록을 반환.</summary>
        public JArray GetPassives(int cardId)
        {
            return GetTrigger(cardId, "Passive");
        }

        /// <summary>KeywordExpander가 자동 주입할 때 사용.</summary>
        public void AddTrigger(int cardId, string trigger, JArray nodes)
        {
            if (!_effects.TryGetValue(cardId, out var triggers))
            {
                triggers = new Dictionary<string, JArray>();
                _effects[cardId] = triggers;
            }
            triggers[trigger] = nodes;
        }
    }
}
