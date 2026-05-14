using System.Text.RegularExpressions;
using Data.Models;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Effects.Keywords
{
    /// <summary>
    /// cardDB의 효과 텍스트 키워드([희생], [반향] 등)를 보고
    /// EffectRegistry에 자동으로 트리거 시퀀스를 주입하고, 데이터-텍스트 일관성을 검증한다.
    /// EffectRegistry.InitializeAsync 직후 1회 호출.
    ///
    /// 처리 범위:
    ///   - [반향] / [Echo] : IsEcho 플래그가 SoT. 텍스트와 플래그 불일치 시 경고 로그.
    ///     실제 Echo 동작은 GameActionSystem.Destroy/Exile에서 IsEcho 플래그를 직접 보고 처리한다.
    ///   - [희생: N] / [Sacrifice: N] : 효과 텍스트에 비용 표기가 있으나 cardsEffects.json에
    ///     OnRevealCost(또는 OnClick) 단계의 Sacrifice 명령이 누락된 경우 자동 주입한다.
    ///     이미 정의돼 있으면 덮어쓰지 않는다 (수동 정의가 우선).
    ///
    /// 주의: [분열]/[위기]/[계시] 는 cardDB의 junction / IsCrisis / IsRevealImmediately
    /// 플래그가 SoT이므로 expander가 처리하지 않는다.
    /// </summary>
    public static class KeywordExpander
    {
        // [희생: 1], [Sacrifice: 2], [희생], [Sacrifice] 등 모두 매칭
        private static readonly Regex SacrificePattern = new Regex(
            @"\[\s*(?:희생|Sacrifice)\s*(?::\s*(?<n>\d+))?\s*\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EchoPattern = new Regex(
            @"\[\s*(?:반향|Echo)\s*\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static void Expand(EffectRegistry registry)
        {
            if (registry == null)
            {
                Debug.LogWarning("[KeywordExpander] EffectRegistry == null. 스킵.");
                return;
            }
            if (CardCatalog.Instance == null)
            {
                Debug.LogWarning("[KeywordExpander] CardCatalog == null. 스킵.");
                return;
            }

            int echoValidated = 0;
            int sacrificeInjected = 0;
            int sacrificeSkipped = 0;

            foreach (var card in CardCatalog.Instance.GetAllCards())
            {
                if (card == null) continue;

                // 1) Echo 키워드 ↔ IsEcho 플래그 일관성 검증
                bool hasEchoText = !string.IsNullOrEmpty(card.Effect) && EchoPattern.IsMatch(card.Effect);
                if (hasEchoText && !card.IsEcho)
                {
                    Debug.LogWarning(
                        $"[KeywordExpander] CardId {card.Id}({card.Name}) 효과 텍스트에 [Echo]가 있으나 IsEcho=false. cardDB 데이터를 확인하세요.");
                }
                else if (!hasEchoText && card.IsEcho)
                {
                    Debug.LogWarning(
                        $"[KeywordExpander] CardId {card.Id}({card.Name}) IsEcho=true 이지만 효과 텍스트에 [Echo] 키워드 없음.");
                }
                if (card.IsEcho) echoValidated++;

                // 2) Sacrifice 비용 자동 주입
                //   조건: 효과 텍스트에 [희생: N] 또는 [Sacrifice: N] 이 있고,
                //         cardsEffects.json 에 OnRevealCost 가 아직 없을 때만 주입한다.
                //   기존 정의가 있으면(수동) 덮어쓰지 않는다.
                if (!string.IsNullOrEmpty(card.Effect))
                {
                    var match = SacrificePattern.Match(card.Effect);
                    if (match.Success)
                    {
                        int amount = 1;
                        if (match.Groups["n"].Success && int.TryParse(match.Groups["n"].Value, out var parsed))
                        {
                            amount = parsed;
                        }

                        bool hasExistingCost = registry.GetTrigger(card.Id, "OnRevealCost") != null;
                        bool hasOnClickSacrifice = HasSacrificeInTrigger(registry, card.Id, "OnClick");

                        if (hasExistingCost || hasOnClickSacrifice)
                        {
                            sacrificeSkipped++;
                            continue;
                        }

                        var node = new JObject
                        {
                            ["cmd"] = "Sacrifice",
                            ["amount"] = amount,
                            ["selectionType"] = "Manual",
                            ["from"] = new JObject
                            {
                                ["zone"] = "Field",
                                ["filter"] = new JObject { ["isCultistCard"] = true }
                            }
                        };
                        registry.AddTrigger(card.Id, "OnRevealCost", new JArray { node });
                        sacrificeInjected++;
                        Debug.Log(
                            $"[KeywordExpander] CardId {card.Id}({card.Name}) OnRevealCost 자동 주입: Sacrifice {amount}.");
                    }
                }
            }

            Debug.Log(
                $"[KeywordExpander] 완료. Echo 검증 {echoValidated}장, Sacrifice 자동 주입 {sacrificeInjected}장, 기존 정의로 건너뜀 {sacrificeSkipped}장.");
        }

        private static bool HasSacrificeInTrigger(EffectRegistry registry, int cardId, string trigger)
        {
            var nodes = registry.GetTrigger(cardId, trigger);
            if (nodes == null) return false;
            foreach (var token in nodes)
            {
                if (token is JObject obj && obj["cmd"]?.ToString() == "Sacrifice") return true;
            }
            return false;
        }
    }
}
