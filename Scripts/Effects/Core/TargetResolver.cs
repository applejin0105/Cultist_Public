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

        // === 핵심 진입점: 필터링된 카드 목록 반환 ========================================

        public List<CardInstance> Resolve(JObject from, TriggerContext ctx, bool excludeSource = false)
        {
            if (from == null) return new List<CardInstance>();

            var ownerToken = from["owner"];
            var players = ResolvePlayers(ownerToken, ctx).ToList();

            string zoneStr = from["zone"]?.ToString() ?? "Field";
            var filter = from["filter"] as JObject;

            var result = new List<CardInstance>();
            foreach (var p in players)
            {
                IEnumerable<CardInstance> cards = GetCardsInZone(p, zoneStr);
                if (filter != null) cards = ApplyFilter(cards, filter, ctx);
                
                if (excludeSource && ctx.Source != null)
                {
                    cards = cards.Where(c => c.InstanceId != ctx.Source.InstanceId);
                }

                result.AddRange(cards);
            }
            return result;
        }

        // === 고도화 진입점: 필터링 + 수량에 따른 최종 선택 (Manual/Auto 대응) =============

        public async Task<List<CardInstance>> PickAsync(JObject node, TriggerContext ctx, bool singleOwner = false, bool excludeSource = false)
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
                Debug.Log($"[TargetResolver] Manual 선택 요청: {ctx.Actor}, {actualMin}~{actualMax}, SingleOwner: {singleOwner}, ExcludeSource: {excludeSource}");
                var picked = await _input.SelectTargetsAsync(ctx.Actor, candidates, actualMin, actualMax, singleOwner);
                return picked ?? new List<CardInstance>();
            }

            // Auto: 무작위 선택
            return candidates.OrderBy(_ => _rng.Next()).Take(actualMax).ToList();
        }

        public async Task<List<CardInstance>> ManualPickOneOrDoneAsync(Player actor, List<CardInstance> candidates, bool singleOwner, bool excludeSource = false)
        {
            if (_gameState.IsGameEnded) return new List<CardInstance>();
            return await _input.SelectTargetsAsync(actor, candidates, 0, 1, singleOwner);
        }

        // === Player 타겟팅 로직 ========================================================

        public IEnumerable<Player> ResolvePlayers(JToken token, TriggerContext ctx)
        {
            var alive = _gameState.GetAlivePlayers().ToList();

            if (token == null) return new[] { ctx.Source.OwnerSeat };

            if (token.Type == JTokenType.String)
            {
                return token.ToString() switch
                {
                    "Self"     => new[] { ctx.Source.OwnerSeat },
                    "Opponent" => alive.Where(p => p != ctx.Source.OwnerSeat).ToList(),
                    "All"      => alive,
                    _          => new[] { ctx.Source.OwnerSeat }
                };
            }

            if (token is JObject obj)
            {
                // [추가] 변수 기반 플레이어 참조 지원: { "var": "targetP" }
                if (obj["var"] != null)
                {
                    int val = ValueResolver.ResolveInt(token, ctx, this);
                    return new[] { (Player)val };
                }

                string type = obj["type"]?.ToString();
                string statKey = obj["stat"]?.ToString();

                switch (type)
                {
                    case "PlayerLowestStat":     return RankByStat(alive, statKey, lowest: true);
                    case "PlayerHighestStat":    return RankByStat(alive, statKey, lowest: false);
                    case "OpponentLowerStat":    return CompareToSelf(alive, statKey, ctx.Source.OwnerSeat, lower: true);
                    case "OpponentHigherStat":   return CompareToSelf(alive, statKey, ctx.Source.OwnerSeat, lower: false);
                    default: return Enumerable.Empty<Player>();
                }
            }

            return new[] { ctx.Source.OwnerSeat };
        }

        private IEnumerable<Player> RankByStat(List<Player> alive, string statKey, bool lowest)
        {
            if (alive.Count == 0) return Enumerable.Empty<Player>();
            var pairs = alive.Select(p => (p, v: GetPlayerStat(p, statKey))).ToList();
            int target = lowest ? pairs.Min(x => x.v) : pairs.Max(x => x.v);
            return pairs.Where(x => x.v == target).Select(x => x.p).ToList();
        }

        private IEnumerable<Player> CompareToSelf(List<Player> alive, string statKey, Player self, bool lower)
        {
            int selfStat = GetPlayerStat(self, statKey);
            return alive.Where(p => p != self).Where(p => {
                int v = GetPlayerStat(p, statKey);
                return lower ? v < selfStat : v > selfStat;
            }).ToList();
        }

        private int GetPlayerStat(Player player, string statKey)
        {
            return _gameState.GetPlayerStat(player, statKey);
        }

        // === 카드 필터링 고도화 ========================================================

        private IEnumerable<CardInstance> GetCardsInZone(Player player, string zone) => zone switch
        {
            "Field" => _gameState.GetAllCards().Where(c => c.Zone == Zone.Field && c.OwnerSeat == player),
            "Hand"  => _gameState.GetAllCards().Where(c => c.Zone == Zone.Hand && c.OwnerSeat == player),
            "Deck"  => _gameState.GetAllCards().Where(c => c.Zone == Zone.Deck && c.OwnerSeat == player),
            _       => Enumerable.Empty<CardInstance>()
        };

        private IEnumerable<CardInstance> ApplyFilter(IEnumerable<CardInstance> cards, JObject filter, TriggerContext ctx = null)
        {
            if (filter == null) return cards;

            // 1. 상태 필터
            if (filter["isCultistCard"]?.ToObject<bool?>() == true)
                cards = cards.Where(c => c.CardStatus == CardStatus.FieldBack);

            if (filter["isRevealed"]?.ToObject<bool?>() == true)
                cards = cards.Where(c => c.CardStatus == CardStatus.FieldFront);

            // 2. 종파(Sect) 필터
            if (ctx != null && filter["inSect"]?.ToObject<bool?>() == true)
            {
                var sectIds = GetSectInstanceIds(ctx.Source);
                cards = cards.Where(c => sectIds.Contains(c.InstanceId));
            }

            if (ctx != null && ctx.Cause != null && filter["inSectOfCause"]?.ToObject<bool?>() == true)
            {
                var sectIds = GetSectInstanceIds(ctx.Cause);
                cards = cards.Where(c => sectIds.Contains(c.InstanceId));
            }

            // 3. 속성 필터 (ID, 신도수)
            var cardIdsTok = filter["cardIds"];
            if (cardIdsTok is JArray ids && ids.Count > 0)
            {
                var set = new HashSet<int>(ids.Select(t => t.ToObject<int>()));
                cards = cards.Where(c => set.Contains(c.CardId));
            }

            var cultistTok = filter["cultist"];
            if (cultistTok != null)
            {
                // 1) 정수 리터럴 — 기존 호환: { "cultist": 5 } → ==
                if (cultistTok.Type == JTokenType.Integer)
                {
                    int cv = cultistTok.ToObject<int>();
                    cards = cards.Where(c => c.BaseData != null && c.BaseData.Cultist == cv);
                }
                // 2) op/value 연산자: { "cultist": { "op": ">=", "value": 3 } }
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
            "=="  => lhs == rhs,
            "!="  => lhs != rhs,
            ">="  => lhs >= rhs,
            "<="  => lhs <= rhs,
            ">"   => lhs >  rhs,
            "<"   => lhs <  rhs,
            _     => false,
        };

        private HashSet<int> GetSectInstanceIds(CardInstance source)
        {
            return _gameState.GetSectInstanceIds(source);
        }

        public List<CardInstance> ResolveDeckCardsByFilter(Player player, JObject filter)
        {
            IEnumerable<CardInstance> cards = _gameState.GetAllCards().Where(c => c.Zone == Zone.Deck && c.OwnerSeat == player);
            return ApplyFilter(cards, filter).ToList();
        }
    }
}
