using System;
using System.Collections.Generic;

namespace Data.Models
{
    /// <summary>
    /// DeckData를 담고있는 JSON 기반 배열
    /// </summary>
    [Serializable]
    public class PlayerDeck
    {
        public List<DeckData> playerDecks = new();
    }
}