using System.Collections.Generic;
using Newtonsoft.Json;

namespace Data.Models
{
    /// <summary>
    /// JSON 기반 기본 카드 덱 데이터
    /// </summary>
    [System.Serializable]
    public class DeckData
    {
        public string deckName;
        public int rootCardId;
        public List<int> cardIds;

        [JsonIgnore] public bool IsSample { get; set; } = false;
    }
}