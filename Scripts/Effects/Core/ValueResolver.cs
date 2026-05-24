using Newtonsoft.Json.Linq;

namespace Effects.Core
{
    /// <summary>
    /// IntExpr 토큰을 정수로 환원.
    /// 지원 형태:
    ///   3                                        ← 정수 리터럴
    ///   { "var": "n" }                           ← TriggerContext.Vars 참조
    ///   { "cardCount": { "ids":[...], "zone":"Field" } }   ← 카드 수 (Step 6에서 구현)
    ///   { "min": 0, "max": 2 }                   ← 범위 (Manual 선택의 amount 등)
    ///
    /// Step 1에서는 정수/var 만 지원, 나머지는 Step 진행에 맞춰 확장.
    /// </summary>
    public static class ValueResolver
    {
        public static int ResolveInt(JToken token, TriggerContext ctx, TargetResolver targets = null,
            int defaultValue = 0)
        {
            if (token == null) return defaultValue;

            // 리터럴 타입 반환
            if (token.Type == JTokenType.Integer)
            {
                return token.ToObject<int>();
            }

            if (token is JObject obj)
            {
                // SetVar, 변수 참조 반환 { "var": "n" }
                var varToken = obj["var"];
                if (varToken != null && ctx != null
                                     && ctx.Vars.TryGetValue(varToken.ToString(), out var v))
                {
                    return v;
                }

                // 인라인 계산, 카드 갯수 카운팅해서 반환 { "type": "cardCount", "from": { ... } }
                if (obj["type"]?.ToString() == "cardCount" && targets != null && ctx != null)
                {
                    var from = obj["from"] as JObject;
                    if (from != null)
                    {
                        var cards = targets.Resolve(from, ctx);
                        return cards.Count;
                    }
                }

                // 인라인 계산, 플레이어 스탯 카운팅해서 반환 { "type": "playerStat", "stat": "strength", "target": "Self" }
                if (obj["type"]?.ToString() == "playerStat" && ctx != null)
                {
                    string stat = obj["stat"]?.ToString();
                    return ctx.GameState.GetPlayerStat(ctx.Actor, stat);
                }

                // 인라인 계산, 플레이어 history 계산해서 반환  { "type": "historyCount", "action": "Trade", "scope": "Turn" }
                if (obj["type"]?.ToString() == "historyCount" && ctx != null)
                {
                    string actionStr = obj["action"]?.ToString();
                    string scope = obj["scope"]?.ToString() ?? "Turn";
                    // ActionType 파싱 및 히스토리 카운트
                    if (System.Enum.TryParse<Domain.Enums.ActionType>(actionStr, out var actionType))
                    {
                        return ctx.GameState.GetHistoryCount(ctx.Actor, actionType, scope);
                    }
                }
            }

            return defaultValue;
        }
        
        // { "min": 0, "max": 2 } 범위 표현을 min, max 형태로 풀어서 반환
        public static (int min, int max) ResolveAmountRange(JToken token, TriggerContext ctx,
            TargetResolver targets = null,
            int defaultMin = 0, int defaultMax = 1)
        {
            if (token == null) return (defaultMin, defaultMax);

            if (token.Type == JTokenType.Integer)
            {
                int v = token.ToObject<int>();
                return (v, v);
            }

            if (token is JObject obj)
            {
                if (obj["min"] != null || obj["max"] != null)
                {
                    int min = ResolveInt(obj["min"], ctx, targets, defaultMin);
                    int max = ResolveInt(obj["max"], ctx, targets, defaultMax);
                    return (min, max);
                }

                int single = ResolveInt(token, ctx, targets, defaultMax);
                return (single, single);
            }

            return (defaultMin, defaultMax);
        }
    }
}