using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;
using Domain.State;
using Domain.State.Host;
using Effects.Interfaces;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Utils;

namespace Effects.Core
{
    /// <summary>
    /// 카드 후보 풀 구성 및 최종 타겟 선택(Manual/Auto)을 담당.
    /// </summary>
    public sealed class TargetResolver
    {
        private readonly IEffectGameState _gameState;
        private readonly IPlayerInputProvider _input;
        private readonly IRandomSource _rng;

        public TargetResolver(IEffectGameState gameState, IPlayerInputProvider input, IRandomSource rng)
        {
            _gameState = gameState;
            _input = input;
            _rng = rng;
        }

        /// <summary>
        /// 공개 API 둘(Resolve / ResolveDeckCardsByFilter)이 공유하는 핵심 로직.
        /// 플레이어 목록 & zone → GetCardsInZone → ApplyFilter → excludeSource 처리까지를 한 곳에 모은다.
        /// 두 진입점은 *받는 입력의 모양*(JSON vs 코드)만 다르고 내부 처리는 동일하다.
        /// </summary>
        private List<CardInstance> ApplyZoneAndFilter(IEnumerable<Player> players, string zone, JObject filter,
            TriggerContext ctx, bool excludeSource)
        {
            var result = new List<CardInstance>();
            foreach (var p in players)
            {
                IEnumerable<CardInstance> cards = GetCardsInZone(p, zone);
                if (filter != null) cards = ApplyFilter(cards, filter, ctx);

                if (excludeSource && ctx?.Source != null)
                {
                    cards = cards.Where(c => c.InstanceId != ctx.Source.InstanceId);
                }

                result.AddRange(cards);
            }

            return result;
        }

        // 필터링된 카드 목록 반환

        public List<CardInstance> Resolve(JObject from, TriggerContext ctx, bool excludeSource = false)
        {
            if (from == null) return new List<CardInstance>();

            var players = ResolvePlayers(from["owner"], ctx).ToList();
            string zoneStr = from["zone"]?.ToString() ?? "Field";
            var filter = from["filter"] as JObject;

            return ApplyZoneAndFilter(players, zoneStr, filter, ctx, excludeSource);
        }

        public List<CardInstance> ResolveDeckCardsByFilter(Player player, JObject filter)
        {
            return ApplyZoneAndFilter(new[] { player }, "Deck", filter, ctx: null, excludeSource: false);
        }
        
        // 필터링 + 수량에 따른 최종 선택 (Manual/Auto 대응)
        // 실제로 카드를 골라내는 함수
        public async Task<List<CardInstance>> PickAsync(JObject node, TriggerContext ctx, bool singleOwner = false,
            bool excludeSource = false)
        {
            if (_gameState.IsGameEnded) return new List<CardInstance>();

            var from = node["from"] as JObject;
            var candidates = Resolve(from, ctx, excludeSource);

            if (candidates.Count == 0) return candidates;

            string selectionType = node["selectionType"]?.ToString() ?? "Auto";
            var amountTok = node["amount"];

            if (amountTok?.ToString().Equals("All", StringComparison.OrdinalIgnoreCase) == true)
            {
                return candidates;
            }

            (int min, int max) = ValueResolver.ResolveAmountRange(amountTok, ctx, this);
            int actualMax = Mathf.Min(max, candidates.Count);
            int actualMin = Mathf.Min(min, actualMax);

            if (selectionType == "Manual")
            {
                if (_gameState.IsGameEnded) return new List<CardInstance>();
                Debug.Log(
                    $"[TargetResolver] Manual 선택 요청: {ctx.Actor}, {actualMin}~{actualMax}, SingleOwner: {singleOwner}, ExcludeSource: {excludeSource}");
                var picked = await _input.SelectTargetsAsync(ctx.Actor, candidates, actualMin, actualMax, singleOwner);
                return picked ?? new List<CardInstance>();
            }

            // Auto: 조건에 맞는다면 무작위 선택
            return candidates.OrderBy(_ => _rng.Next()).Take(actualMax).ToList();
        }

        public async Task<List<CardInstance>> ManualPickOneOrDoneAsync(Player actor, List<CardInstance> candidates,
            bool singleOwner, bool excludeSource = false)
        {
            if (_gameState.IsGameEnded) return new List<CardInstance>();
            return await _input.SelectTargetsAsync(actor, candidates, 0, 1, singleOwner);
        }

        // Player 타겟팅 로직

        public IEnumerable<Player> ResolvePlayers(JToken token, TriggerContext ctx)
        {
            var alive = _gameState.GetAlivePlayers().ToList();

            if (token == null) return new[] { ctx.Source.OwnerSeat };

            if (token.Type == JTokenType.String)
            {
                // Self: 자기 자신
                // Opponent: 나 자신을 제외한 살아있는 플레이어 전부
                // All: 나 자신을 포함한 살아있는 플레이어 전부
                return token.ToString() switch
                {
                    "Self" => new[] { ctx.Source.OwnerSeat },
                    "Opponent" => alive.Where(p => p != ctx.Source.OwnerSeat).ToList(),
                    "All" => alive,
                    _ => new[] { ctx.Source.OwnerSeat }
                };
            }

            if (token is JObject obj)
            {
                string type = obj["type"]?.ToString();
                string statKey = obj["stat"]?.ToString();

                switch (type)
                {
                    case "PlayerLowestStat": return FindLowestStat(alive, statKey);
                    case "OpponentLowerStat": return FindLowerThanSelf(alive, statKey, ctx.Source.OwnerSeat);
                    default: return Enumerable.Empty<Player>();
                }
            }

            return new[] { ctx.Source.OwnerSeat };
        }

        private IEnumerable<Player> FindLowestStat(List<Player> alive, string statKey)
        {
            if (alive.Count == 0) return Enumerable.Empty<Player>();
            var pairs = alive.Select(p => (p, v: GetPlayerStat(p, statKey))).ToList();
            int target = pairs.Min(x => x.v);
            return pairs.Where(x => x.v == target).Select(x => x.p).ToList();
        }

        private IEnumerable<Player> FindLowerThanSelf(List<Player> alive, string statKey, Player self)
        {
            int selfStat = GetPlayerStat(self, statKey);
            return alive.Where(p => p != self).Where(p =>
            {
                int v = GetPlayerStat(p, statKey);
                return v < selfStat;
            }).ToList();
        }

        private int GetPlayerStat(Player player, string statKey)
        {
            return _gameState.GetPlayerStat(player, statKey);
        }

        // 카드 필터링 고도화

        private IEnumerable<CardInstance> GetCardsInZone(Player player, string zone) => zone switch
        {
            "Field" => _gameState.GetAllCards().Where(c => c.Zone == Zone.Field && c.OwnerSeat == player),
            "Hand" => _gameState.GetAllCards().Where(c => c.Zone == Zone.Hand && c.OwnerSeat == player),
            "Deck" => _gameState.GetAllCards().Where(c => c.Zone == Zone.Deck && c.OwnerSeat == player),
            _ => Enumerable.Empty<CardInstance>()
        };

        private IEnumerable<CardInstance> ApplyFilter(IEnumerable<CardInstance> cards, JObject filter,
            TriggerContext ctx = null)
        {
            if (filter == null) return cards;

            /* 상태 필터 */

            // 신도카드
            if (filter["isCultistCard"]?.ToObject<bool?>() == true)
                cards = cards.Where(c => c.CardStatus == CardStatus.FieldBack);

            // 공개된 카드
            if (filter["isRevealed"]?.ToObject<bool?>() == true)
                cards = cards.Where(c => c.CardStatus == CardStatus.FieldFront);

            // 종파(Sect) 필터 — 정의는 FieldTree.GetSectInstanceIds 참조
            if (ctx != null && filter["inSect"]?.ToObject<bool?>() == true)
            {
                var sectIds = _gameState.GetSectInstanceIds(ctx.Source);
                cards = cards.Where(c => sectIds.Contains(c.InstanceId));
            }

            if (ctx != null && ctx.Cause != null && filter["inSectOfCause"]?.ToObject<bool?>() == true)
            {
                var sectIds = _gameState.GetSectInstanceIds(ctx.Cause);
                cards = cards.Where(c => sectIds.Contains(c.InstanceId));
            }

            // 속성 필터 (ID, 신도수)
            var cardIdsTok = filter["cardIds"];
            if (cardIdsTok is JArray ids && ids.Count > 0)
            {
                var set = new HashSet<int>(ids.Select(t => t.ToObject<int>()));
                cards = cards.Where(c => set.Contains(c.CardId));
            }

            var cultistTok = filter["cultist"];
            if (cultistTok != null)
            {
                // 정수 리터럴 — 기존 호환: { "cultist": 5 } → ==
                if (cultistTok.Type == JTokenType.Integer)
                {
                    int cv = cultistTok.ToObject<int>();
                    cards = cards.Where(c => c.BaseData != null && c.BaseData.Cultist == cv);
                }
                // op/value 연산자: { "cultist": { "op": ">=", "value": 3 } }
                //    지원 연산자: "==", "!=", ">=", "<=", ">", "<"
                else if (cultistTok is JObject cultistObj
                         && cultistObj["op"] != null && cultistObj["value"] != null)
                {
                    string op = cultistObj["op"].ToString();
                    int v = cultistObj["value"].ToObject<int>();
                    cards = cards.Where(c => c.BaseData != null && CompareInt(c.BaseData.Cultist, op, v));
                }
            }

            return cards;
        }

        /// <summary>정수 비교 헬퍼. ApplyFilter의 op/value 연산자 비교에 사용.</summary>
        private static bool CompareInt(int lhs, string op, int rhs) => op switch
        {
            "==" => lhs == rhs,
            "!=" => lhs != rhs,
            ">=" => lhs >= rhs,
            "<=" => lhs <= rhs,
            ">" => lhs > rhs,
            "<" => lhs < rhs,
            _ => false,
        };
    }
}