using System.Threading.Tasks;
using Domain.Enums;
using Effects.Core;
using Newtonsoft.Json.Linq;
using Systems;
using UnityEngine;

namespace Effects.Commands.Card
{
    /// <summary>
    /// 효과에 의해 필드의 뒷면 카드를 강제로 뒤집는 명령.
    /// JSON: {
    ///   "cmd": "Reveal",
    ///   "amount": IntExpr,
    ///   "selectionType": "Manual" | "Auto",
    ///   "from": { ...TargetResolver... }
    /// }
    /// </summary>
    public sealed class RevealCommand : ICommand
    {
        private readonly GameActionSystem _actionSystem;
        private readonly TargetResolver _targets;

        public RevealCommand(GameActionSystem actionSystem, TargetResolver targets)
        {
            _actionSystem = actionSystem;
            _targets = targets;
        }

        public async Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            // 필터에 isCultistCard: true 강제 (뒷면 카드만 뒤집을 수 있으므로)
            var from = node["from"] as JObject;
            if (from != null)
            {
                var filter = from["filter"] as JObject;
                if (filter == null) from["filter"] = filter = new JObject();
                filter["isCultistCard"] = true;
            }

            var finalTargets = await _targets.PickAsync(node, ctx);

            foreach (var t in finalTargets)
            {
                Debug.Log($"[RevealCommand] {t.BaseData.Name}({t.InstanceId}) 강제 공개 실행");
                await _actionSystem.Reveal(t.OwnerSeat, t, RevealReason.Forced);
            }
        }
    }
}
