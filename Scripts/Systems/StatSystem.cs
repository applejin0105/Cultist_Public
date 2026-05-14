using System.Linq;
using Domain.Entities;
using Domain.Enums;
using Domain.State;
using Domain.State.Host;
using Effects.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Utils;

namespace Systems
{
    public class StatSystem
    {
        private readonly GameState _gameState;
        private GameRuleSystem _gameRuleSystem;
        private TargetResolver _targetResolver;

        private ConditionRegistry _conditionRegistry;

        public StatSystem(GameState gameState)
        {
            _gameState = gameState;
        }

        public void SetGameRuleSystem(GameRuleSystem gameRuleSystem)
        {
            _gameRuleSystem = gameRuleSystem;
        }

        public void SetConditionRegistry(ConditionRegistry conditionRegistry)
        {
            _conditionRegistry = conditionRegistry;
        }

        public void SetTargetResolver(TargetResolver targetResolver)
        {
            _targetResolver = targetResolver;
        }

        public ConditionRegistry GetConditionRegistry() => _conditionRegistry;

        /// <summary>
        /// 특정 플레이어의 모든 스탯(신도, 심볼)을 현재 필드 상황에 맞춰 재계산
        /// </summary>
        public void UpdatePlayerStats(Player player, bool isCardReveal = false)
        {
            var pState = _gameState.GetPlayerStateById(player);
            if (pState == null) return;

            if (pState.LifeStatus == PlayerLifeStatus.Eliminated)
            {
                Debug.Log($"[StatSystem] <스탯 업데이트 스킵> 플레이어 {player}는 이미 탈락 상태입니다.");
                return;
            }

            Debug.Log($"[StatSystem] <스탯 재계산 시작> Player: {player} (이전 Cultist: {pState.Cultist})");

            // 1. 초기화 (영구 보너스 수치부터 깨끗하게 시작)
            pState.SetCultist(pState.PermanentCultist);
            pState.SetSymbols((int[])pState.PermanentSymbols.Clone());

            // 2. 필드 카드 순회 (중복 방지를 위해 Distinct 사용)
            var fieldCards = CardQueries.GetFieldCards(_gameState, player).Distinct().ToList();

            foreach (var card in fieldCards)
            {
                // A. 뒷면(FieldBack) -> 신도(Cultist) 제공 (카드 자체 수치만큼)
                if (card.CardStatus == CardStatus.FieldBack)
                {
                    pState.AddCultist(card.BaseData.Cultist);
                }
                // B. 앞면(FieldFront) -> 심볼(SymbolG) + 지속 효과(Passive) 제공
                else if (card.CardStatus == CardStatus.FieldFront)
                {
                    AddSymbolsFromCardBase(pState, card.BaseData);
                    ApplyPassives(pState, card);
                }
                // C. 파괴됨(FieldDestroyed) -> 아무것도 주지 않음 (하지만 Junction 구조는 유지됨)
            }

            Debug.Log(
                $"[StatSystem] <스탯 재계산 완료> {player} -> Cultist: {pState.Cultist}, Strength: {pState.Symbols[(int)Symbols.Strength]}, Unity: {pState.Symbols[(int)Symbols.Unity]}");

            if (_gameRuleSystem != null)
            {
                Debug.Log($"[StatSystem] 승패 및 탈락 조건 검사 요청 (_gameRuleSystem.CheckStatConditions)");
                _gameRuleSystem.CheckStatConditions();
            }
        }

        private void AddSymbolsFromCardBase(PlayerState pState, Card cardData)
        {
            var symbolG = cardData.SymbolG;
            for (int i = 0; i < 6; i++)
            {
                pState.Symbols[i] += symbolG[i];
            }
        }

        private void ApplyPassives(PlayerState pState, CardInstance sourceCard)
        {
            var passives = Data.Models.EffectRegistry.Instance?.GetPassives(sourceCard.CardId);
            if (passives == null) return;

            // 패시브 평가를 위한 임시 컨텍스트 (Actor = Owner)
            var ctx = new TriggerContext(_gameState, sourceCard, sourceCard.OwnerSeat);

            foreach (var token in passives)
            {
                if (!(token is JObject node)) continue;

                string type = node["type"]?.ToString();
                if (type == "AddSymbol")
                {
                    string symbolStr = node["symbol"]?.ToString() ?? "";
                    if (System.Enum.TryParse<Symbols>(symbolStr, out var symbol))
                    {
                        // [수정] ValueResolver를 사용하여 동적 수치(cardCount 등) 해결
                        int amount = ValueResolver.ResolveInt(node["amount"], ctx, _targetResolver, 0);
                        pState.Symbols[(int)symbol] += amount;
                    }
                }
            }
        }
    }
}