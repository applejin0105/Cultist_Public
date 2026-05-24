using Domain.Entities;
using Domain.Enums;
using Domain.State;
using Domain.State.Host;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// 플레이어의 스탯(신도, 심볼)을 이벤트 기반으로 갱신하는 시스템.
    ///
    /// 게임 룰 전제:
    ///  - SymbolR(필요 심볼)은 조건일 뿐, 카드 공개 시 소모되지 않는다.
    ///  - SymbolG(기여 심볼)는 카드를 공개할 때 한 번 누적된다.
    ///  - 앞면(FieldFront) 카드는 파괴/추방되지 않으므로 누적된 심볼은 영구.
    ///  - 신도(Cultist)는 face-down 카드 1장당 1회 카운팅된다.
    ///    공개 시 차감, 파괴/추방/희생 시 차감.
    /// </summary>
    public sealed class StatSystem
    {
        private readonly GameState _gameState;
        private GameRuleSystem _gameRuleSystem;

        public StatSystem(GameState gameState)
        {
            _gameState = gameState;
        }

        public void SetGameRuleSystem(GameRuleSystem gameRuleSystem)
        {
            _gameRuleSystem = gameRuleSystem;
        }

        /// <summary>
        /// Play 직후 호출. 카드가 Hand → Field(face-down)로 들어오면서 신도가 추가된다.
        /// </summary>
        public void OnCardPlayedToField(Player player, CardInstance card)
        {
            var pState = GetActive(player);
            if (pState == null || card?.BaseData == null) return;

            pState.AddCultist(card.BaseData.Cultist);
            AfterChange(player, pState, $"+Cultist({card.BaseData.Cultist}) from {card.BaseData.Name}");
        }

        /// <summary>
        /// Reveal 직후 호출. face-down → face-up. 신도가 빠지고 SymbolG가 누적된다.
        /// </summary>
        public void OnCardRevealed(Player player, CardInstance card)
        {
            var pState = GetActive(player);
            if (pState == null || card?.BaseData == null) return;

            pState.AddCultist(-card.BaseData.Cultist);

            var symbolG = card.BaseData.SymbolG;
            for (int i = 0; i < 6; i++)
            {
                pState.AddSymbol(i, symbolG[i]);
            }

            AfterChange(player, pState, $"Reveal {card.BaseData.Name} -Cultist({card.BaseData.Cultist}) +SymbolG");
        }

        /// <summary>
        /// Destroy / Exile / Sacrifice 직후 호출.
        /// 게임 룰상 잃을 수 있는 카드는 모두 신도 카드이므로 -신도만 적용한다.
        /// </summary>
        public void OnCultistCardLost(Player player, CardInstance card)
        {
            var pState = GetActive(player);
            if (pState == null || card?.BaseData == null) return;

            pState.AddCultist(-card.BaseData.Cultist);
            AfterChange(player, pState, $"Lost {card.BaseData.Name} -Cultist({card.BaseData.Cultist})");
        }

        private PlayerState GetActive(Player player)
        {
            var pState = _gameState.GetPlayerStateById(player);
            if (pState == null) return null;

            if (pState.LifeStatus == PlayerLifeStatus.Eliminated)
            {
                Debug.Log($"[StatSystem] <스탯 업데이트 스킵> 플레이어 {player}는 이미 탈락 상태입니다.");
                return null;
            }

            return pState;
        }

        private void AfterChange(Player player, PlayerState p, string reason)
        {
            Debug.Log(
                $"[StatSystem] {player} {reason} → Cultist: {p.Cultist}, " +
                $"Strength: {p.Symbols[(int)Symbols.Strength]}, " +
                $"Unity: {p.Symbols[(int)Symbols.Unity]}");

            _gameRuleSystem?.CheckStatConditions();
        }
    }
}