using System;
using System.Collections.Generic;
using Domain.Enums;
using Domain.State;
using UnityEngine;

namespace Systems
{
    public class PhaseSystem
    {
        public PhaseState CurrentPhaseState { get; set; }

        private HashSet<PhaseState> _skipPhase = new HashSet<PhaseState>();

        public event Action<PhaseState> OnPhaseChanged;

        public void Initialize()
        {
            CurrentPhaseState = PhaseState.StandBy;
            _skipPhase.Clear();
        }

        public void ReserveSkip(PhaseState targetPhase)
        {
            if (!_skipPhase.Contains(targetPhase))
            {
                _skipPhase.Add(targetPhase);
                Debug.Log($"[PhaseSystem] 예약됨: {targetPhase} 스킵");
            }
        }

        public void ClearFlowModifiers()
        {
            _skipPhase.Clear();
        }

        public void AdvancePhase()
        {
            PhaseState next = CalculateNextPhase(CurrentPhaseState);
            Debug.Log($"[PhaseSystem] next -> {next}");
            ChangePhase(next);
        }

        public void ChangePhase(PhaseState newPhaseState)
        {
            if (CurrentPhaseState == newPhaseState) return;

            PhaseState prevState = CurrentPhaseState;
            CurrentPhaseState = newPhaseState;

            Debug.Log($"[PhaseSystem] State 갱신: {prevState} -> {CurrentPhaseState}");
            OnPhaseChanged?.Invoke(CurrentPhaseState);
        }

        private PhaseState CalculateNextPhase(PhaseState current)
        {
            PhaseState nextCandidate = GetStandardNextPhase(current);

            // 스킵 로직 (예: Deer Man의 드로우 스킵)
            while (_skipPhase.Contains(nextCandidate))
            {
                Debug.Log($"[PhaseSystem] 스킵 실행: {nextCandidate} 건너 뜀");
                _skipPhase.Remove(nextCandidate);

                PhaseState tempNext = GetStandardNextPhase(nextCandidate);
                if (tempNext == PhaseState.StandBy)
                {
                    nextCandidate = tempNext;
                    break;
                }
                nextCandidate = tempNext;
            }

            return nextCandidate;
        }

        private PhaseState GetStandardNextPhase(PhaseState current)
        {
            if (current == PhaseState.StandBy) return PhaseState.From(Phase.Draw.StandBy);

            if (current == PhaseState.From(Phase.Draw.StandBy)) return PhaseState.From(Phase.Draw.Draw);
            
            // Draw/Trade 이후에는 무조건 Play로
            if (current == PhaseState.From(Phase.Draw.Draw) || current == PhaseState.From(Phase.Draw.Trade))
                return PhaseState.From(Phase.Play.Play);

            // [규격화] Play 이후에는 사이클 마감(StandBy)으로 이동. 
            // 진짜 턴 종료 여부는 TurnSystem에서 RemainingCycles를 보고 판단함.
            if (current == PhaseState.From(Phase.Play.Play)) return PhaseState.StandBy;

            return PhaseState.StandBy;
        }
    }
}