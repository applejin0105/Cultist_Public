using System.Collections.Generic;
using Domain.Entities;
using Domain.Enums;

namespace Effects.Core
{
    /// <summary>
    /// 효과 시스템(명령어, 타겟 리졸버, 조건문)이 게임 상태에 접근하기 위한 추상화 인터페이스.
    /// Host(Server)와 Client 양쪽에서 동일한 로직을 수행할 수 있도록 한다.
    /// </summary>
    public interface IEffectGameState
    {
        bool IsGameEnded { get; }

        // --- 카드 관련 ---
        IEnumerable<CardInstance> GetAllCards();
        CardInstance GetCard(int instanceId);
        
        // --- 플레이어 및 스탯 관련 ---
        IEnumerable<Player> GetAlivePlayers();
        
        /// <summary>
        /// 특정 플레이어의 스탯(cultist, influence, strength 등) 값을 조회한다.
        /// </summary>
        int GetPlayerStat(Player player, string statKey);
        
        // --- 턴 및 게임 환경 관련 ---
        int GetCurrentRound();
        
        // --- 행동 이력(History) 관련 ---
        /// <summary>
        /// 특정 액션이 발생한 횟수를 조회한다.
        /// </summary>
        int GetHistoryCount(Player actor, ActionType type, string scope);
        
        // --- 필드 구조(트리) 관련 ---
        /// <summary>
        /// 특정 카드와 연결된 모든 계파(Sect) 카드들의 인스턴스 ID 목록을 반환한다.
        /// </summary>
        HashSet<int> GetSectInstanceIds(CardInstance source);
    }
}
