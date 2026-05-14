using System.Collections.Generic;
using System.Linq;
using Domain.Entities;
using Domain.Enums;
using Newtonsoft.Json.Linq;
using Data.Models;
using App.Network;
using Scenes.InGame.Core;
using UnityEngine;

namespace Effects.Core
{
    /// <summary>
    /// 클라이언트 사이드에서 카드 효과의 실행 가능 여부(주로 비용 지불 가능 여부)를 검증한다.
    /// </summary>
    public static class EffectValidator
    {
        public static bool CanPayOnRevealCost(int cardId, Player actor, CardInstance source)
        {
            var nodes = EffectRegistry.Instance.GetTrigger(cardId, "OnRevealCost");
            if (nodes == null || nodes.Count == 0) return true;

            // 클라이언트용 상태 및 리졸버 준비
            var clientState = new ClientGameStateProvider();
            var targets = new TargetResolver(clientState, null, null);
            var ctx = new TriggerContext(clientState, source, actor);

            foreach (var token in nodes)
            {
                if (!(token is JObject node)) continue;
                var cmdName = node["cmd"]?.ToString();
                if (string.IsNullOrEmpty(cmdName)) continue;

                // 클라이언트에 등록된 명령어가 없으므로 수동으로 필요한 명령어만 체크
                if (cmdName == "Sacrifice")
                {
                    // SacrificeCommand의 CanExecute를 재사용 (Host/Client 공용 로직)
                    var sacrificeCmd = new Commands.Card.SacrificeCommand(null, null, targets, null, null);
                    if (!sacrificeCmd.CanExecute(node, ctx)) return false;
                }
                // 추후 Trade, Starve 등 비용 관련 명령어가 추가되면 여기에 CanExecute 체크 추가
            }

            return true;
        }

        /// <summary>
        /// 카드가 공개(Reveal) 가능한 상태인지 검증한다. (Feat 키워드 등)
        /// owner 인자: 공개를 시도하는 플레이어(seat). Feat 체크는 플레이어별로 카운트한다.
        /// </summary>
        public static bool CanReveal(int cardId, int ownerSeat)
        {
            var baseData = CardCatalog.Instance.Get(cardId);
            if (baseData == null) return true;

            // Feat (IsUniqueReveal) 체크: 본인 필드에 이미 앞면인 동일 ID의 카드가 있는지 검사
            // 플레이어별 카운팅 — 다른 플레이어가 가지고 있어도 본인은 공개 가능
            if (baseData.IsUniqueReveal)
            {
                if (NetworkGameController.Instance != null)
                {
                    bool alreadyExists = NetworkGameController.Instance.SyncCards.Any(c =>
                        c.CardId == cardId
                        && c.Status == (int)CardStatus.FieldFront
                        && c.OwnerSeat == ownerSeat);

                    if (alreadyExists)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Mirror의 SyncCards와 UI 데이터를 활용해 클라이언트 사이드에서 IEffectGameState를 구현한다.
    /// </summary>
    public class ClientGameStateProvider : IEffectGameState
    {
        public bool IsGameEnded => NetworkGameController.Instance != null && NetworkGameController.Instance.IsGameEnded;

        public IEnumerable<CardInstance> GetAllCards()
        {
            if (NetworkGameController.Instance == null) return Enumerable.Empty<CardInstance>();
            return NetworkGameController.Instance.SyncCards.Select(Convert);
        }

        public CardInstance GetCard(int instanceId)
        {
            if (NetworkGameController.Instance == null) return null;
            var data = NetworkGameController.Instance.SyncCards.FirstOrDefault(c => c.InstanceId == instanceId);
            return data.InstanceId != 0 ? Convert(data) : null;
        }

        public IEnumerable<Player> GetAlivePlayers()
        {
            if (NetworkGameController.Instance == null) return Enumerable.Empty<Player>();

            // SyncCards나 다른 동기화 데이터를 통해 생존 플레이어를 판별해야 함.
            // 임시로 모든 플레이어 반환 (필요 시 PlayerState 동기화 추가 필요)
            return new[] { Player.Player1, Player.Player2, Player.Player3 };
        }

        public int GetPlayerStat(Player player, string statKey)
        {
            if (InGameUIManager.Instance == null) return 0;
            var panel = InGameUIManager.Instance.GetPanelBySeat((int)player);
            if (panel == null) return 0;

            return statKey.Trim().ToLowerInvariant() switch
            {
                "cultist" => panel.GetCurrentCultist(),
                "influence" => panel.GetCurrentSymbols()[0],
                "unity" => panel.GetCurrentSymbols()[1],
                "monotheism" => panel.GetCurrentSymbols()[2],
                "polytheism" => panel.GetCurrentSymbols()[3],
                "strength" => panel.GetCurrentSymbols()[4],
                "pantheon" => panel.GetCurrentSymbols()[5],
                _ => 0
            };
        }

        public int GetCurrentRound()
        {
            // 클라이언트 사이드 라운드 정보 (필요 시 동기화 변수 추가)
            return 1; 
        }

        public int GetHistoryCount(Player actor, ActionType type, string scope)
        {
            // 클라이언트 사이드 히스토리 추적은 현재 미구현
            return 0;
        }

        public HashSet<int> GetSectInstanceIds(CardInstance source)
        {
            // 클라이언트 사이드 트리 구조 조회 로직 (필요 시 구현)
            return new HashSet<int>();
        }

        private CardInstance Convert(NetworkDTOs.CardNetData data)
        {
            // CardInstance는 생성자에서 전달받은 CardId를 통해 
            // 내부적으로 CardCatalog에서 BaseData를 자동 참조하므로 SetupCard가 필요 없음.
            var instance = new CardInstance(data.InstanceId, data.CardId, (Player)data.OwnerSeat, (Zone)data.Zone, (CardStatus)data.Status);
            return instance;
        }
    }
}