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
    /// cardsEffects.json을 로드·보관하는 레지스터.
    /// </summary>
    public sealed class EffectRegistry
    {
        public static EffectRegistry Instance { get; private set; }

        // <CardId, trigger<트리거 이름, 명령 배열>>
        // CardId → 트리거 이름 → 명령 배열(JArray)
        // _effects[cardId][triggerName] = 명령 배열(JArray)
        private Dictionary<int, Dictionary<string, JArray>> _effects
            = new Dictionary<int, Dictionary<string, JArray>>();

        /// <summary>
        /// 앱 시작 시 GameBootstrapper에서 1회 호출. 멱등.
        /// 실패 시 Instance가 null로 유지되어 다음 호출이 자연스럽게 재시도한다.
        /// </summary>
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
                // 딕셔너리 값 존재 안하면 그냥 null
                _effects = JsonConvert.DeserializeObject<Dictionary<int, Dictionary<string, JArray>>>(json)
                           ?? new Dictionary<int, Dictionary<string, JArray>>();
            }
            catch (JsonException e)
            {
                // 슬픈 상황...
                // 그래도 꺾이지 않고 null로 할당
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

    }
}