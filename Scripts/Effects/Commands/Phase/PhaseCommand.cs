using System;
using System.Threading.Tasks;
using Domain.Enums;
using Domain.State;
using Effects.Core;
using Newtonsoft.Json.Linq;
using Systems;
using UnityEngine;

namespace Effects.Commands.Phase
{
    /// <summary>
    /// 페이즈 흐름 변경 명령.
    /// JSON: { "cmd": "Phase", "target": "Phase.Draw.StandBy", "type": "skip"|"repeat", "amount": 1 }
    ///
    /// - skip:   target 페이즈를 1회 건너뜀
    /// - repeat: target 페이즈를 amount만큼 추가 반복
    ///
    /// PhaseSystem의 Reserve* API에 위임.
    /// </summary>
    public sealed class PhaseCommand : ICommand
    {
        private readonly PhaseSystem _phaseSystem;
        private readonly TargetResolver _targets;

        public PhaseCommand(PhaseSystem phaseSystem, TargetResolver targets)
        {
            _phaseSystem = phaseSystem;
            _targets = targets;
        }

        public Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            string targetStr = node["target"]?.ToString();
            string type = node["type"]?.ToString();

            if (!TryParsePhaseState(targetStr, out var target))
            {
                Debug.LogError($"[Phase] 파싱 실패: target={targetStr}");
                return Task.CompletedTask;
            }

            if (type == "skip")
            {
                _phaseSystem.ReserveSkip(target);
                Debug.Log($"[Phase] skip 예약: {targetStr}");
            }
            else
            {
                Debug.LogWarning($"[Phase] 알 수 없는 type: {type}");
            }

            return Task.CompletedTask;
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;

        // "Phase.Draw.StandBy" → PhaseState 변환
        private static bool TryParsePhaseState(string s, out PhaseState result)
        {
            result = PhaseState.StandBy;
            if (string.IsNullOrEmpty(s)) return false;

            var parts = s.Split('.');
            if (parts.Length < 3) return false;

            string mainStr = parts[1];
            string subStr = parts[2];

            try
            {
                if (!Enum.TryParse(mainStr, out Domain.Enums.Phase.Main main)) return false;

                if (main == Domain.Enums.Phase.Main.Draw && Enum.TryParse(subStr, out Domain.Enums.Phase.Draw subDraw))
                {
                    result = PhaseState.From(subDraw);
                    return true;
                }

                if (main == Domain.Enums.Phase.Main.Play && Enum.TryParse(subStr, out Domain.Enums.Phase.Play subPlay))
                {
                    result = PhaseState.From(subPlay);
                    return true;
                }

                if (main == Domain.Enums.Phase.Main.StandBy)
                {
                    result = PhaseState.StandBy;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Phase] 파싱 예외: {e.Message}");
            }

            return false;
        }
    }
}
