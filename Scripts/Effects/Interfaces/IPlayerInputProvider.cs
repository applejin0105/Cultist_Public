using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;

namespace Effects.Interfaces
{
    public interface IPlayerInputProvider
    {
        /// <summary>
        /// 플레이어에게 유효한 후보군 중 타겟을 선택하도록 요청
        Task<List<CardInstance>> SelectTargetsAsync(
            Player player,
            List<CardInstance> candidates,
            int min,
            int max,
            bool singleOwner = false
        );

        Task<DrawPhaseAction> SelectDrawPhaseAsync(Player player, bool canDraw, bool carTrade);

        Task<CardInstance> SelectCardToKeepAsync(Player player, List<CardInstance> cardInstance);

        Task<CardInstance> SelectCardFromTradeAsync(Player player, List<CardInstance> tradeCards);
    }

    public enum DrawPhaseAction
    {
        Draw,
        Trade
    }
}