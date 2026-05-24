using Mirror;
using Scenes.Lobby;
using UnityEngine;

namespace App.Network
{
    public class LobbyPlayerState : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnSteamIdChanged))]
        public ulong steamId;

        [SyncVar(hook = nameof(OnReadyStateChanged))]
        public bool isReady = false;

        [SyncVar(hook = nameof(OnLeaderStateChanged))]
        public bool isLeader = false;

        public override void OnStartServer()
        {
            base.OnStartServer();
            isLeader = (connectionToClient == NetworkServer.localConnection);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (steamId != 0) UpdateLobbyUI(steamId);
        }

        public override void OnStartLocalPlayer()
        {
            StartCoroutine(WaitForReadyAndSetSteamId());
        }

        private System.Collections.IEnumerator WaitForReadyAndSetSteamId()
        {
            yield return new WaitUntil(() => NetworkClient.ready && NetworkClient.connection != null);
            yield return new WaitForSeconds(0.2f);

            ulong myId = 0;
            if (NetworkManager.singleton.transport is kcp2k.KcpTransport)
            {
                myId = 10000000000000000UL + netId;
            }
            else if (Steamworks.SteamClient.IsValid)
            {
                myId = SteamManager.Instance.MySteamID;
            }

            CmdSetSteamId(myId);
        }

        [Command]
        private void CmdSetSteamId(ulong id)
        {
            steamId = id;
            RpcUpdateUI(id);
        }

        [ClientRpc]
        private void RpcUpdateUI(ulong id)
        {
            UpdateLobbyUI(id);
        }

        [Command]
        public void CmdSetReady(bool state)
        {
            isReady = state;
        }

        // [추가] 로컬 환경에서 게임 시작을 동기화하기 위한 RPC
        [Command]
        public void CmdTriggerLocalGameStart()
        {
            RpcTriggerLocalGameStart();
        }

        [ClientRpc]
        private void RpcTriggerLocalGameStart()
        {
            if (LobbyUIManager.Instance != null)
            {
                LobbyUIManager.Instance.ExecuteLocalGameStart();
            }
        }

        private void OnReadyStateChanged(bool oldState, bool newState)
        {
            if (LobbyUIManager.Instance != null && steamId != 0)
            {
                LobbyUIManager.Instance.UpdateReadyUI(steamId, newState);
            }
        }

        private void OnLeaderStateChanged(bool oldState, bool newState)
        {
            UpdateLobbyUI(steamId);
        }

        private void OnSteamIdChanged(ulong oldId, ulong newId)
        {
            UpdateLobbyUI(newId);
        }

        private void UpdateLobbyUI(ulong id)
        {
            if (id == 0 || LobbyUIManager.Instance == null) return;
            LobbyUIManager.Instance.AssignPlayerToSlot(id, isLeader, isLocalPlayer, isReady);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (LobbyUIManager.Instance != null && steamId != 0)
            {
                LobbyUIManager.Instance.ClearSlotBySteamId(steamId);
            }
        }
    }
}