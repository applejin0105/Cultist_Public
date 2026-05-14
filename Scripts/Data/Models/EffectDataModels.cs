// 새 EffectModels (단순화).
// 이 파일은 빅뱅 교체 단계에서 의도적으로 유지된 파일명이다.
// (Unity .meta 파일을 보존하기 위해 기존 파일명을 재활용)

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Data.Models
{
    /// <summary>
    /// cardsEffects.json 루트 형식:
    ///   {
    ///     "0": { "OnHand":   [ { "cmd": "Log", "msg": "Meow!" } ] },
    ///     "1": { "OnReveal": [ { "cmd": "SetNextDraw", ... } ] },
    ///     ...
    ///   }
    ///
    /// CardId(int) → (TriggerName(string) → 명령 시퀀스(JArray))
    /// </summary>
    public sealed class CardEffectMap
    {
        public Dictionary<int, Dictionary<string, JArray>> Effects { get; }
            = new Dictionary<int, Dictionary<string, JArray>>();
    }
}
