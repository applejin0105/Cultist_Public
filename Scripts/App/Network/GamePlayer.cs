using System.Collections.Generic;
using Core.Data.Enums;
using Core.Managers;
using Domain.Enums;
using Mirror;
using UnityEngine;
using Scenes.Lobby;
using Scenes.InGame.Core;
using Scenes.InGame.UI;

namespace App.Network
{
    public class GamePlayer : NetworkBehaviour
    {
        [SyncVar] public int seatIndex;
        [SyncVar] public string steamName;
        [SyncVar] public ulong steamId;
        [SyncVar] public int maxJunction;

        private NetworkGameController _controller;

        public event System.Action<List<int>> OnTradeCardRequested;
        public event System.Action<List<int>, int, int, bool> OnTargetSelectionRequested;

        private bool _hasSubmittedDeck = false;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "05_InGame")
            {
                string myName = "Leader";
                ulong mySteamId = 0;

                if (NetworkManager.singleton.transport is kcp2k.KcpTransport)
                {
                    myName = isServer ? "Local_Host" : $"Local_Client_{Random.Range(1000, 9999)}";
                    mySteamId = 0;
                }
                else if (Steamworks.SteamClient.IsValid)
                {
                    myName = Steamworks.SteamClient.Name;
                    mySteamId = Steamworks.SteamClient.SteamId;
                }

                CmdSetPlayerInfo(myName, mySteamId);
                Invoke(nameof(SubmitDeckData), 1.5f);
            }
        }

        [Command]
        private void CmdSetPlayerInfo(string name, ulong id)
        {
            steamName = name;
            steamId = id;
        }

        private void Update()
        {
            // 네트워크 문제로 혹시나 사라지면 바로 찾아서 재할당
            if (isLocalPlayer && _controller == null && NetworkClient.active)
            {
                _controller = FindFirstObjectByType<NetworkGameController>();
            }
        }

        private void SubmitDeckData()
        {
            if (!isLocalPlayer || _hasSubmittedDeck) return;
            _hasSubmittedDeck = true;

            var myDeck = MatchSessionManager.Instance.MyDeckData;
            if (myDeck != null)
            {
                CmdSubmitDeckData(netId, myDeck.rootCardId, myDeck.cardIds.ToArray());
            }
            else
            {
                Debug.LogError("[InGame] 치명적 오류: 씬 전환 후에도 덱 데이터가 없습니다.");
            }
        }

        [Command]
        private void CmdSubmitDeckData(uint playerNetId, int rootCardId, int[] cardIds)
        {
            var controller = FindFirstObjectByType<NetworkGameController>();
            if (controller != null) controller.RegisterPlayer(this);
            InGameSessionManager.Instance.RegisterPlayerDeck(playerNetId, rootCardId, cardIds);
        }
        
        [TargetRpc]
        public void TargetRpc_RequestSelectTargets(List<int> candidateIds, int min, int max, bool singleOwner)
        {
            Debug.Log($"[Client] 타겟 선택 요청 받음. 후보:{candidateIds.Count}개, 요구:{min}~{max}개, SingleOwner:{singleOwner}");
            OnTargetSelectionRequested?.Invoke(candidateIds, min, max, singleOwner);
        }

        [TargetRpc]
        public void TargetRpc_RequestDrawAction(bool canDraw, bool canTrade)
        {
            Debug.Log($"[Client] 드로우/교역 선택 요청 받음. Draw가능:{canDraw}, Trade가능:{canTrade}");
            if (InGameUIManager.Instance != null) InGameUIManager.Instance.ShowDrawTradeChoice(canDraw, canTrade);
        }

        [TargetRpc]
        public void TargetRpc_RequestSelectCardToKeep(int[] instanceIds, int[] cardIds)
        {
            Debug.Log($"[Client] Keep할 카드 선택 요청 받음 ({instanceIds.Length}장)");
            if (DraftUIManager.Instance != null) DraftUIManager.Instance.ShowDraft(instanceIds, cardIds);
            else Debug.LogError("[Client] DraftUIManager를 씬에서 찾을 수 없습니다.");
        }

        [TargetRpc]
        public void TargetRpc_RequestSelectCardFromTrade(List<int> tradeCards)
        {
            Debug.Log($"[Client] 교역소 카드 선택 요청 받음 ({tradeCards.Count}장)");
            if (TradeUIManager.Instance != null) TradeUIManager.Instance.OpenTradePanel();
            OnTradeCardRequested?.Invoke(tradeCards);
        }

        [Command]
        public void Cmd_SubmitKeepCard(int selectedId)
        {
            if (_controller == null) _controller = FindFirstObjectByType<NetworkGameController>();
            if (_controller != null) _controller.OnClientSubmitKeepCard(selectedId);
        }

        [Command]
        public void Cmd_SubmitTradeSelect(int selectedId)
        {
            if (_controller == null) _controller = FindFirstObjectByType<NetworkGameController>();
            if (_controller != null) _controller.OnClientSubmitTradeSelect(selectedId);
        }

        [Command]
        public void Cmd_SubmitTargets(int[] selectedIds)
        {
            if (_controller == null) _controller = FindFirstObjectByType<NetworkGameController>();
            if (_controller != null) _controller.OnClientSubmitTargets(selectedIds);
        }

        [Command]
        public void Cmd_SubmitDrawAction(int actionType)
        {
            if (_controller == null) _controller = FindFirstObjectByType<NetworkGameController>();
            if (_controller != null) _controller.OnClientSubmitDrawAction(actionType);
        }

        public void RequestAdvancePhaseOrEndTurn()
        {
            if (!isLocalPlayer) return;
            if (_controller == null) _controller = FindFirstObjectByType<NetworkGameController>();

            if (_controller == null || _controller.currentActivePlayerSeat != seatIndex)
            {
                Debug.LogWarning("[Client] 당신의 차례가 아닙니다.");
                return;
            }

            // Play 페이즈 처리
            if (_controller.currentPhaseMain == (int)Phase.Main.Play)
            {
                int handCount = 0;
                foreach (var card in _controller.SyncCards)
                {
                    if (card.Zone == (int)Zone.Hand && card.OwnerSeat == seatIndex) handCount++;
                }

                if (handCount > 0)
                {
                    Debug.LogWarning($"[Client] 패에 {handCount}장의 카드가 남아있어 사이클을 종료할 수 없습니다! (Seat: {seatIndex})");
                    return;
                }

                Debug.Log($"[Client] 모든 행동 완료 확인 (Hand: 0). 사이클 마감 요청 전송. (Seat: {seatIndex})");
                CmdAdvancePhase();
            }
            else
            {
                Debug.LogWarning($"[Client] 현재 페이즈(Main: {_controller.currentPhaseMain})에서는 턴을 종료할 수 없습니다.");
            }
        }

        [TargetRpc]
        public void TargetRpc_PlayDrawSound(int cardCount)
        {
            if (SoundManager.Instance != null)
            {
                Debug.Log($"[Client] {cardCount}장의 카드를 뽑았습니다. 사운드 재생!");
                UISoundType playSoundType = cardCount switch
                {
                    1 => UISoundType.Draw1T,
                    2 => UISoundType.Draw2T,
                    3 => UISoundType.Draw3T,
                    4 => UISoundType.Draw4T,
                    _ => UISoundType.Draw1T
                };
                SoundManager.Instance.PlaySfx(playSoundType);
            }
        }

        [TargetRpc]
        public void TargetRpc_PlayUISfx(UISoundType type)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySfx(type);
            }
        }

        [TargetRpc]
        public void TargetRpc_PlayCardVoice(CardSoundType type)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayCardSound(type);
            }
        }

        [ClientRpc]
        public void Rpc_PlayGlobalCardVoice(CardSoundType type)
        {
            if (SoundManager.Instance != null)
            {
                Debug.Log($"[Global Audio] 사기사 강림 보이스 재생: {type}");
                SoundManager.Instance.PlayCardSound(type);
            }
        }

        [Command]
        private void CmdAdvancePhase()
        {
            if (_controller != null && _controller.currentActivePlayerSeat == seatIndex)
            {
                _controller.ExecuteAdvancePhase(seatIndex);
            }
        }

        [Command]
        public void Cmd_PlayCardOnField(int handCardInstanceId, int targetFieldCardInstanceId, int slotIndex)
        {
            if (_controller == null) _controller = FindFirstObjectByType<NetworkGameController>();
            if (_controller != null)
                _controller.ExecutePlayCard(seatIndex, handCardInstanceId, targetFieldCardInstanceId, slotIndex);
        }

        [Command]
        public void Cmd_RevealCard(int cardInstanceId)
        {
            if (_controller == null) _controller = FindFirstObjectByType<NetworkGameController>();
            if (_controller != null) _controller.ExecuteRevealCard(seatIndex, cardInstanceId);
        }

        [Command]
        public void Cmd_UseCard(int cardInstanceId)
        {
            if (_controller == null) _controller = FindFirstObjectByType<NetworkGameController>();
            if (_controller != null) _controller.ExecuteUseCard(seatIndex, cardInstanceId);
        }
    }
}