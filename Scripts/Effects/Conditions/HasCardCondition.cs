using System.Linq;
using Domain.Enums;
using Effects.Core;
using Newtonsoft.Json.Linq;

namespace Effects.Conditions
{
    /// <summary>
    /// 플레이어의 필드나 패에 특정 카드가 존재하는지 체크.
    /// JSON: { "type": "HasCard", "cardId": 12, "zone": "Field|Hand|All", "target": "Self|Opponent" }
    /// </summary>
    public class HasCardCondition : ConditionBase
    {
        public override bool Evaluate(JObject node, TriggerContext ctx)
        {
            int cardId = node["cardId"]?.ToObject<int>() ?? -1;
            string zoneStr = node["zone"]?.ToString() ?? "Field";

            Player targetPlayer = ResolveTarget(node, ctx);

            var cards = ctx.GameState.GetAllCards().Where(c => c.OwnerSeat == targetPlayer);

            if (zoneStr == "Field")
            {
                cards = cards.Where(c => c.Zone == Zone.Field);
            }
            else if (zoneStr == "Hand")
            {
                cards = cards.Where(c => c.Zone == Zone.Hand);
            }

            return cards.Any(c => c.CardId == cardId);
        }
    }
}
