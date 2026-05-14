using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Data.Enums;
using Domain.Entities;
using Domain.Enums;
using Domain.Policies;
using Domain.State.Host;
using Domain.Structure.Field;
using Effects.Core;
using Effects.Interfaces;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Utils;

namespace Systems
{
    /// <summary>
    /// Draw, Play, Reveal 규칙 관리 시스템
    /// </summary>
    public class GameActionSystem
    {
        private readonly GameState _gameState;
        private EffectRunner _effectRunner;
        private TargetResolver _targetResolver;
        private GameRuleSystem _gameRuleSystem;

        private readonly IPlayerInputProvider _playerInputProvider;

        public StatSystem StatSystem { get; private set; }

        public GameActionSystem(GameState gameState, IPlayerInputProvider playerInputProvider)
        {
            _gameState = gameState;
            _playerInputProvider = playerInputProvider;

            StatSystem = gameState.StatSystem;
        }

        public void SetGameRuleSystem(GameRuleSystem gameRuleSystem)
        {
            _gameRuleSystem = gameRuleSystem;
            StatSystem.SetGameRuleSystem(_gameRuleSystem);
        }

        private void CheckGameRules()
        {
            if (_gameRuleSystem == null) return;

            Debug.Log("[GameActionSystem] <전역 규칙 검사 실행> Field & Stat conditions");
            _gameRuleSystem.CheckFieldConditions();
            _gameRuleSystem.CheckStatConditions();
        }

        public void SetEffectRunner(EffectRunner effectRunner)
        {
            _effectRunner = effectRunner;
        }

        public void SetTargetResolver(TargetResolver resolver)
        {
            _targetResolver = resolver;
        }

        public async Task<bool> Draw(Player player, DrawRule rule)
        {
            if (!IsPlayerAlive(player))
            {
                Debug.LogWarning($"[GameActionSystem] 탈락자 {player}가 Draw를 시도. 어딜 감히. 喝!");
                return false;
            }

            List<CardInstance> fetchedCards = await FetchCardAsync(player, rule);

            if (fetchedCards == null || fetchedCards.Count == 0)
            {
                Debug.Log($"[GameAction] 드로우 실패. (Player: {player}");
                return false;
            }

            if (rule.Type == DrawType.Draft)
            {
                await ResolveDraftDraw(player, fetchedCards);
            }
            else
            {
                await ResolveSimpleDraw(player, fetchedCards);
            }

            StatSystem.UpdatePlayerStats(player);
            return true;
        }

        // 카드를 확보
        private async Task<List<CardInstance>> FetchCardAsync(Player player, DrawRule rule)
        {
            List<CardInstance> results = new List<CardInstance>();

            if (rule.CardCondition != null)
            {
                if (_targetResolver == null)
                {
                    Debug.LogError("[GameActionSystem] TargetResolver 미주입. CardCondition 기반 검색 불가.");
                    return results;
                }

                Debug.Log($"[Fetch] 조건부 검색 시작 (목표: {rule.Amount}, where={rule.CardCondition})");

                var candidates = _targetResolver.ResolveDeckCardsByFilter(player, rule.CardCondition);
                if (candidates.Count == 0)
                {
                    Debug.Log("[Fetch] 조건 만족 카드 없음");
                    return results;
                }

                int count = Mathf.Min(rule.Amount, candidates.Count);
                results = candidates.Take(count).ToList();
                Debug.Log($"[Fetch] {count}장 확보 (후보 {candidates.Count}장 중)");
                return results;
            }
            else
            {
                var deckState = _gameState.GetDeckStateById(player);
                var drawCardAmount = rule.Amount;

                // 덱이 완전히 비었을 때 사기사 카드를 즉시 드로우 (결과 리스트에 추가)
                if (deckState.DeckCount() == 0)
                {
                    if (_gameState.DrawFourHorseManCard(out CardInstance horseMan, player, Zone.Hand,
                            CardStatus.Hand))
                    {
                        Debug.Log($"[GameAction] {player}의 덱이 비어 사기사 카드를 즉시 뽑음: {horseMan.BaseData.Name}");
                        results.Add(horseMan);
                        return results;
                    }
                }

                int count = Mathf.Min(drawCardAmount, deckState.DeckCount());

                for (int i = 0; i < count; i++)
                {
                    if (deckState.Draw(out CardInstance card))
                    {
                        results.Add(card);
                    }
                }
            }

            return await Task.FromResult(results);
        }

        private async Task ResolveDraftDraw(Player player, List<CardInstance> fetchedCards)
        {
            foreach (var card in fetchedCards)
            {
                card.ChangeCardStatus(CardStatus.Draft);
            }

            if (App.Network.NetworkGameController.Instance != null)
            {
                // [추가] 드래프트 시작 시 드로우 효과음 재생
                var playerComp = App.Network.NetworkGameController.Instance.GetPlayerComponent(player);
                playerComp?.TargetRpc_PlayDrawSound(fetchedCards.Count);

                App.Network.NetworkGameController.Instance.TriggerCardSync();
            }

            Debug.Log($"[Resolve] Draft 모드: {fetchedCards.Count}장 중 택1");

            List<CardInstance> forceSelectedCard = new List<CardInstance>();
            CardInstance selected = null;

            if (fetchedCards.Count == 1)
            {
                selected = fetchedCards[0];
            }
            else
            {
                // [수정] IsForceSelect가 있더라도 무조건 유저 입력을 기다림 (UI에서 다른 카드 선택을 막음)
                selected = await _playerInputProvider.SelectCardToKeepAsync(player, fetchedCards);
            }

            if (selected != null)
            {
                CardMovementSystem.MoveCard(_gameState, selected.InstanceId, player, Zone.Hand, CardStatus.Hand);
                Debug.Log($"[Draft] {selected.BaseData.Name} -> Hand로 이동 완료");
            }

            foreach (var discarded in fetchedCards)
            {
                if (selected != null && discarded.InstanceId == selected.InstanceId) continue;

                CardMovementSystem.MoveCard(_gameState, discarded.InstanceId, Player.Game, Zone.Trade,
                    CardStatus.Trade);
                Debug.Log($"[Draft] {discarded.BaseData.Name} -> 교역소로 이동");
            }

            if (App.Network.NetworkGameController.Instance != null)
            {
                App.Network.NetworkGameController.Instance.TriggerCardSync();
            }

            // OnHand 트리거 (Draft에서 Hand로 들어온 카드)
            if (selected != null && _effectRunner != null)
            {
                await _effectRunner.RunTriggerAsync("OnHand", selected, player);
            }
        }

        private async Task ResolveSimpleDraw(Player player, List<CardInstance> fetchedCards)
        {
            Debug.Log($"[Resolve] Simple 모드: {fetchedCards.Count}장 획득");

            foreach (var card in fetchedCards)
            {
                CardMovementSystem.MoveCard(_gameState, card.InstanceId, player, Zone.Hand, CardStatus.Hand);
            }

            if (App.Network.NetworkGameController.Instance != null)
            {
                var playerComp = App.Network.NetworkGameController.Instance.GetPlayerComponent(player);
                playerComp?.TargetRpc_PlayDrawSound(fetchedCards.Count);
            }

            // OnHand 트리거 (Simple Draw로 들어온 모든 카드)
            if (_effectRunner != null)
            {
                foreach (var card in fetchedCards)
                {
                    await _effectRunner.RunTriggerAsync("OnHand", card, player);
                }
            }
        }

        public async Task<bool> Trade(Player player)
        {
            if (!IsPlayerAlive(player))
            {
                Debug.LogWarning($"[GameActionSystem] 탈락자 {player}가 Trade를 시도. 어딜 감히. 喝!");
                return false;
            }

            Debug.Log($"[GameAction] 교역 시작");

            if (_gameState.TradeDeckCount == 0)
            {
                Debug.LogWarning("교역소에 카드가 없습니다.");
                return false;
            }

            var tradeCards = CardQueries.GetTradeCards(_gameState).ToList();

            CardInstance selected = await _playerInputProvider.SelectCardFromTradeAsync(player, tradeCards);

            if (selected != null)
            {
                CardMovementSystem.MoveCard(_gameState, selected.InstanceId, player, Zone.Hand, CardStatus.Hand);
                Debug.Log($"[Trade] {selected.BaseData.Name} 교역 완료");
            }
            else
            {
                // [수정] 아무것도 선택하지 않고 닫은 경우 (혹은 취소) 실패로 간주
                return false;
            }

            StatSystem.UpdatePlayerStats(player);

            // OnHand 트리거 (Trade로 Hand에 들어온 카드)
            if (selected != null && _effectRunner != null)
            {
                await _effectRunner.RunTriggerAsync("OnHand", selected, player);
            }

            return true;
        }

        public void Starve(Player player, int amount = 1, bool shuffle = true)
        {
            if (!IsPlayerAlive(player)) return;

            var deckState = _gameState.GetDeckStateById(player);
            if (deckState == null) return;

            for (int i = 0; i < amount; i++)
            {
                _gameState.AddStarveCard(player, deckState);
            }

            if (shuffle)
            {
                _gameState.ShuffleDeck(player);
            }

            Debug.Log($"[GameAction] {player}의 덱에 기아 카드 {amount}장 추가 완료. (Shuffle: {shuffle})");
        }

        public async Task Reveal(Player player, CardInstance cardInstance, RevealReason reason = RevealReason.Manual)
        {
            if (!IsPlayerAlive(player))
            {
                Debug.LogWarning($"[GameActionSystem] 탈락자 {player}가 Reveal를 시도. 어딜 감히. 喝!");
                return;
            }

            Debug.Log(
                $"[GameAction] {cardInstance.BaseData.Name}({cardInstance.InstanceId}) Reveal 시작 (사유: {reason}, IsRevealImmediately: {cardInstance.BaseData.IsRevealImmediately})");

            // [규격화] Echo Reveal은 비용/페이즈/Feat 검증을 우회한다.
            // (Echo는 Destroy 명령이 IsEcho 카드를 만났을 때 자동 트리거되는 경로)
            // 단, MoveCard → 사운드 RPC → StatSystem → SyncCards → IsGameEnded → OnReveal 트리거
            // 흐름은 Manual Reveal과 동일하게 유지되어야 한다 (아래 공통 흐름으로 진행).
            if (reason == RevealReason.Echo)
            {
                Debug.Log($"[GameAction] {cardInstance.BaseData.Name} 반향(Echo) Reveal 진입 — 검증 우회, 공통 흐름 진입.");
            }

            // Manual인 경우에만 페이즈/턴 검사 수행
            if (reason == RevealReason.Manual && !CanRevealCard(player, cardInstance))
            {
                Debug.LogWarning("[GameAction] Reveal 불가능: 페이즈/턴 불일치");
                return;
            }

            // Manual인 경우에만 비용 검사 수행 (Auto/Forced는 계시 또는 효과이므로 무조건 성공)
            if (reason == RevealReason.Manual)
            {
                // [Feat] (IsUniqueReveal) 체크: 본인 필드에 이미 앞면인 동일 ID의 카드가 있다면 Reveal 중단
                // 플레이어별 카운팅 — 다른 플레이어가 같은 카드를 앞면으로 가지고 있어도 본인은 공개 가능.
                if (cardInstance.BaseData != null && cardInstance.BaseData.IsUniqueReveal)
                {
                    bool alreadyExists = _gameState.GetAllCards()
                        .Any(c => c.CardId == cardInstance.CardId
                               && c.CardStatus == CardStatus.FieldFront
                               && c.OwnerSeat == player);

                    if (alreadyExists)
                    {
                        Debug.LogWarning(
                            $"[GameAction] {cardInstance.BaseData.Name} Reveal 중단: Feat 제약 조건 (본인 필드에 이미 앞면인 카드 존재)");
                        return;
                    }
                }

                // 1. 기본 자원/조건 체크
                if (!await CheckRevealRequirement(player, cardInstance))
                {
                    Debug.LogWarning($"[GameAction] {cardInstance.BaseData.Name} Reveal 불가능: 요구심볼 부족 / 조건 불만족");
                    return;
                }

                // 2. 특수 비용 처리 (OnRevealCost 트리거)
                if (_effectRunner != null)
                {
                    Debug.Log($"[GameAction] {cardInstance.BaseData.Name} OnRevealCost 실행 대기...");
                    var ctx = await _effectRunner.RunTriggerAsync("OnRevealCost", cardInstance, player);
                    if (ctx != null && ctx.Cancelled)
                    {
                        Debug.LogWarning($"[GameAction] {cardInstance.BaseData.Name} Reveal 중단: 비용 지불 취소됨");
                        return;
                    }
                }
            }

            Debug.Log($"[GameAction] {cardInstance.BaseData.Name} 상태 변경: FieldFront");
            CardMovementSystem.MoveCard(_gameState, cardInstance.InstanceId, player, Zone.Field,
                CardStatus.FieldFront);

            // [추가] 공개 사운드 및 보이스 재생
            if (App.Network.NetworkGameController.Instance != null)
            {
                var playerComp = App.Network.NetworkGameController.Instance.GetPlayerComponent(player);
                if (playerComp != null)
                {
                    // 1. 뒤집기 효과음 (실행 플레이어만)
                    UISoundType flipSfx = (UISoundType)Random.Range((int)UISoundType.Flip1, (int)UISoundType.Flip2 + 1);
                    playerComp.TargetRpc_PlayUISfx(flipSfx);

                    // 2. 카드 보이스 — 전체 방송 여부는 cardDB 플래그(IsRevealImmediately)로 판정.
                    //    현재 cardDB에서 IsRevealImmediately=true 인 카드는 사기사([계시] Revelation)뿐이며,
                    //    모든 플레이어에게 보이스를 들려준다.
                    //    (CardId 11~14 하드코딩 제거 — 데이터 기반 판정으로 이관)
                    CardSoundType voiceType = (CardSoundType)cardInstance.CardId;
                    bool isBroadcastVoice = cardInstance.BaseData != null
                                            && cardInstance.BaseData.IsRevealImmediately;
                    if (isBroadcastVoice)
                    {
                        // [계시] 카드: 모든 플레이어에게 재생
                        playerComp.Rpc_PlayGlobalCardVoice(voiceType);
                    }
                    else
                    {
                        // 일반 카드: 실행 플레이어에게만 재생
                        playerComp.TargetRpc_PlayCardVoice(voiceType);
                    }
                }
            }

            // [중요] 스탯 업데이트 후 즉시 동기화하여 Crusade 등의 효과가 최신 Strength를 참조하게 함
            StatSystem.UpdatePlayerStats(player, true);
            if (App.Network.NetworkGameController.Instance != null)
            {
                App.Network.NetworkGameController.Instance.TriggerCardSync();
            }

            // [추가] 스탯 업데이트만으로 게임이 끝났다면 효과 발동 없이 중단
            if (_gameState.IsGameEnded)
            {
                Debug.Log($"[GameAction] {cardInstance.BaseData.Name} Reveal 중단: 스탯 갱신으로 게임 종료됨.");
                return;
            }

            if (_effectRunner != null)
            {
                Debug.Log($"[GameAction] {cardInstance.BaseData.Name} OnReveal 트리거 발화 (reason: {reason})");

                // [추가] Echo Reveal 인 경우 OnReveal 트리거에 isEcho=1 변수를 주입.
                // cardsEffects.json의 { "var": "isEcho" } 토큰이 이 값을 참조한다.
                System.Collections.Generic.IDictionary<string, int> initVars = null;
                if (reason == RevealReason.Echo)
                {
                    initVars = new System.Collections.Generic.Dictionary<string, int> { ["isEcho"] = 1 };
                }

                await _effectRunner.RunTriggerAsync("OnReveal", cardInstance, player, cause: null, initVars: initVars);
            }

            if (_gameState.IsGameEnded) return;

            Debug.Log($"[GameAction] {cardInstance.BaseData.Name} Reveal 프로세스 종료 및 규칙 검사 실행. (사유: {reason})");

            // 효과 적용이 완료된 후 승패 조건을 검사합니다.
            CheckGameRules();
        }

        public async Task Play(Player player, CardInstance handCardInstance, CardInstance parentCardInstance,
            int slotIndex)
        {
            if (!IsPlayerAlive(player) || _gameState.IsGameEnded) return;

            Debug.Log(
                $"[GameAction] Play 시도: {handCardInstance.BaseData.Name} (IsRevealImmediately: {handCardInstance.BaseData.IsRevealImmediately})");

            if (CanPlayCard(player, handCardInstance, parentCardInstance))
            {
                var fieldTree = _gameState.GetFieldTreeById(player);

                var parentNode = fieldTree.GetNodeByInstanceId(parentCardInstance.InstanceId);
                if (parentNode == null)
                {
                    parentNode = new FieldNode(parentCardInstance.InstanceId);
                    fieldTree.AddNode(parentNode);
                }

                var newNode = new FieldNode(handCardInstance.InstanceId, parentCardInstance.InstanceId);
                _gameState.GetFieldTreeById(player).AddNode(newNode);

                parentNode.InsertChild(slotIndex, handCardInstance.InstanceId);

                CardMovementSystem.MoveCard(_gameState, handCardInstance.InstanceId, player, Zone.Field,
                    CardStatus.FieldBack);

                // [추가] 배치 사운드 재생 (실행 플레이어만)
                if (App.Network.NetworkGameController.Instance != null)
                {
                    var playerComp = App.Network.NetworkGameController.Instance.GetPlayerComponent(player);
                    UISoundType deploySfx =
                        (UISoundType)Random.Range((int)UISoundType.Deploy1, (int)UISoundType.Deploy3 + 1);
                    playerComp?.TargetRpc_PlayUISfx(deploySfx);
                }

                StatSystem.UpdatePlayerStats(player);

                if (_gameState.IsGameEnded) return;

                // [변경] 성공적으로 배치된 경우에만 즉시 공개 체크
                if (handCardInstance.BaseData.IsRevealImmediately)
                {
                    Debug.Log(
                        $"[GameAction] {handCardInstance.BaseData.Name}({handCardInstance.InstanceId}) 즉시 공개 발동 (IsRevealImmediately: true)");
                    await Reveal(player, handCardInstance, RevealReason.Auto);
                }
                else
                {
                    // [변경] 특정 ID 하드코딩 로그 제거. 필요 시 IsRevealImmediately가 false인 모든 카드에 대해 로그를 남기거나 제거.
                    Debug.Log(
                        $"[GameAction] {handCardInstance.BaseData.Name}({handCardInstance.InstanceId}) 배치 완료. 공개 대기 중...");
                }

                if (_gameState.IsGameEnded) return;

                CheckGameRules();
            }
        }

        // [선포] 카드들
        public async Task Use(Player player, CardInstance cardInstance)
        {
            if (!IsPlayerAlive(player))
            {
                Debug.LogWarning($"[GameActionSystem] 탈락자 {player}가 Use를 시도. 어딜 감히. 喝!");
                return;
            }

            if (cardInstance.CardStatus != CardStatus.FieldFront) return;
            if (cardInstance.OwnerSeat != player) return;

            // [규격화] OnClick은 자신의 차례, Play 메인 페이즈에서만 발동 가능.
            // (다른 페이즈/다른 플레이어의 OnClick은 클라이언트 변조 등의 경로로 진입할 수 있으므로 서버에서 차단)
            var activePlayer = _gameState.TurnState.ActivePlayer;
            if (!activePlayer.HasValue || activePlayer.Value != player)
            {
                Debug.LogWarning($"[GameActionSystem] {player}의 OnClick 차단: 본인의 차례가 아님 (active: {activePlayer}).");
                return;
            }
            if (_gameState.TurnState.Phase.Main != Phase.Main.Play)
            {
                Debug.LogWarning($"[GameActionSystem] {player}의 OnClick 차단: Play 페이즈가 아님 (현재: {_gameState.TurnState.Phase.Main}).");
                return;
            }

            _gameState.RecordAction(player, ActionType.Use, player, cardInstance.InstanceId);

            // [추가] 클릭(선택) 사운드 재생
            if (App.Network.NetworkGameController.Instance != null)
            {
                var playerComp = App.Network.NetworkGameController.Instance.GetPlayerComponent(player);
                playerComp?.TargetRpc_PlayUISfx(UISoundType.Select);
            }

            if (_effectRunner != null)
                await _effectRunner.RunTriggerAsync("OnClick", cardInstance, player);
        }

        public async Task Destroy(Player player, CardInstance targetCard)
        {
            if (!IsPlayerAlive(player))
            {
                Debug.LogWarning($"[GameActionSystem] 탈락자 {player}가 Destroy를 시도. 어딜 감히. 喝!");
                return;
            }

            if (targetCard == null) return;

            // [규격화] 게임 룰: '공개된' 카드는 절대 파괴될 수 없다. 오직 뒷면(FieldBack)만 파괴 대상.
            if (targetCard.CardStatus != CardStatus.FieldBack)
            {
                Debug.LogWarning($"[GameAction] {targetCard.BaseData.Name}({targetCard.CardStatus})은 공개된 카드이므로 파괴 불가.");
                return;
            }

            // [Echo] 다른 플레이어가 IsEcho 카드를 파괴하려 하면, 파괴되는 대신 공개된다.
            //  - 자살 방지: Destroy 자체에는 자살 방지 없음. Sacrifice→Destroy(actor==owner) 경로는 actor==owner라 Echo가 안 터짐.
            //  - 교역소 복제본 생성하지 않음 (파괴되지 않으므로).
            if (targetCard.BaseData != null
                && targetCard.BaseData.IsEcho
                && player != targetCard.OwnerSeat)
            {
                Debug.Log($"[GameAction] {targetCard.BaseData.Name} 반향(Echo) 트리거 — 파괴 대신 공개로 전환.");
                await Reveal(targetCard.OwnerSeat, targetCard, RevealReason.Echo);
                return;
            }

            // 파괴 전 트리거 발동
            if (_effectRunner != null)
                await _effectRunner.RunTriggerAsync("OnPreDestroy", targetCard, player);

            if (targetCard.Zone != Zone.Field)
            {
                Debug.Log($"[GameAction] {targetCard.BaseData.Name}의 파괴가 취소됨 (이미 {targetCard.Zone}으로 이동).");
                return;
            }

            // OnPreDestroy 도중 카드가 공개됐다면(예: Echo 분기로 들어갔다면) 파괴 중단
            if (targetCard.CardStatus != CardStatus.FieldBack)
            {
                Debug.Log($"[GameAction] {targetCard.BaseData.Name} 파괴 중단: 진행 중 공개됨.");
                return;
            }

            Player owner = targetCard.OwnerSeat;

            // [규격화] 파괴 처리: 필드에 파괴된 상태로 남음 (CardUIManager에서 파괴면 렌더링)
            CardMovementSystem.MoveCard(
                _gameState,
                targetCard.InstanceId,
                owner,
                Zone.Field,
                CardStatus.FieldDestroyed
            );

            // [추가] 파괴 사운드 재생
            if (App.Network.NetworkGameController.Instance != null)
            {
                var playerComp = App.Network.NetworkGameController.Instance.GetPlayerComponent(player);
                playerComp?.TargetRpc_PlayUISfx(UISoundType.Destroy);
            }

            // [Crisis(제외)가 아닌 경우에만] 새로운 인스턴스를 교역소에 추가
            if (!targetCard.BaseData.IsCrisis)
            {
                _gameState.CreateAndAddCardToTrade(targetCard.CardId);
                Debug.Log($"[GameAction] {targetCard.BaseData.Name} 복제본이 교역소에 추가되었습니다.");
            }
            else
            {
                Debug.Log($"[GameAction] {targetCard.BaseData.Name}은 Crisis 카드이므로 복제본이 생성되지 않습니다.");
            }

            // 기록 저장
            _gameState.RecordAction(player, ActionType.Destroy, owner, targetCard.CardId);

            // 파괴 후 트리거 발동
            if (_effectRunner != null)
                await _effectRunner.RunTriggerAsync("OnDestroyed", targetCard, player);

            Debug.Log($"[Action] {player}가 {owner}의 카드 {targetCard.BaseData.Name} 파괴 완료");

            // 스탯 재계산
            StatSystem.UpdatePlayerStats(owner);

            CheckGameRules();
        }

        public async Task Exile(Player player, CardInstance targetCard)
        {
            if (!IsPlayerAlive(player))
            {
                Debug.LogWarning($"[GameActionSystem] 탈락자 {player}가 Exile를 시도. 어딜 감히. 喝!");
                return;
            }

            if (targetCard == null) return;

            // [규격화] 게임 룰: '공개된' 카드는 절대 파괴/추방될 수 없다.
            if (targetCard.CardStatus != CardStatus.FieldBack)
            {
                Debug.LogWarning($"[GameAction] {targetCard.BaseData.Name}({targetCard.CardStatus})은 공개된 카드이므로 추방 불가.");
                return;
            }

            // [Echo] 추방도 파괴의 한 형태이므로 IsEcho 카드는 추방 대신 공개된다.
            //  - 다른 플레이어가 시도한 경우에만 발동
            //  - 교역소 복제본 미생성은 어차피 Exile의 본래 동작과 일치
            if (targetCard.BaseData != null
                && targetCard.BaseData.IsEcho
                && player != targetCard.OwnerSeat)
            {
                Debug.Log($"[GameAction] {targetCard.BaseData.Name} 반향(Echo) 트리거 — 추방 대신 공개로 전환.");
                await Reveal(targetCard.OwnerSeat, targetCard, RevealReason.Echo);
                return;
            }

            // [규격화] 추방 처리: 파괴와 동일하게 필드에 남되, 복제본 생성을 생략함
            CardMovementSystem.MoveCard(_gameState, targetCard.InstanceId, targetCard.OwnerSeat, Zone.Field,
                CardStatus.FieldDestroyed);

            // [추가] 추방(파괴) 사운드 재생
            if (App.Network.NetworkGameController.Instance != null)
            {
                var playerComp = App.Network.NetworkGameController.Instance.GetPlayerComponent(player);
                playerComp?.TargetRpc_PlayUISfx(UISoundType.Destroy);
            }

            // 기록 저장
            _gameState.RecordAction(player, ActionType.Exile, targetCard.OwnerSeat, targetCard.CardId);

            // 파괴 후 트리거 발동 (추방도 파괴의 한 종류로 취급하여 트리거 공유 가능)
            if (_effectRunner != null)
                await _effectRunner.RunTriggerAsync("OnDestroyed", targetCard, player);

            Debug.Log($"[Action] {player}가 {targetCard.OwnerSeat}의 카드 {targetCard.BaseData.Name} 추방함 (복제본 없음)");

            StatSystem.UpdatePlayerStats(targetCard.OwnerSeat);

            CheckGameRules();
        }

        private async Task<bool> CheckRevealRequirement(Player player, CardInstance cardInstance)
        {
            var pState = _gameState.GetPlayerStateById(player);

            // 1. 자살 방지: 소모될 Cultist가 현재 Cultist보다 "크거나 같으면" 차단 (0이 되는 것을 방지)
            if (pState.Cultist <= cardInstance.BaseData.Cultist)
            {
                Debug.Log(
                    $"[CheckCost] {player} 생명력 부족. (현재: {pState.Cultist}, 요구치: {cardInstance.BaseData.Cultist + 1} 이상 필요)");
                return false;
            }

            // 2. 기본 심볼 조건 체크 (SymbolR)
            var requiredSymbols = cardInstance.BaseData.SymbolR;
            for (int i = 0; i < 6; i++)
            {
                int required = requiredSymbols[i];
                int current = pState.Symbols[i];

                if (current < required)
                {
                    Debug.Log($"[CheckCost] {player} 심볼 부족. Idx:{i}, Need:{required}, Has:{current}");
                    return false;
                }
            }

            // 3. [추가] JSON 정의 특수 조건 체크 (RevealCondition)
            var conditionNode = Data.Models.EffectRegistry.Instance?.GetTrigger(cardInstance.CardId, "RevealCondition");
            if (conditionNode != null && conditionNode.Count > 0)
            {
                var ctx = new TriggerContext(_gameState, cardInstance, player);
                foreach (var token in conditionNode)
                {
                    if (token is JObject node)
                    {
                        string type = node["type"]?.ToString();
                        var cond = _gameState.StatSystem.GetConditionRegistry()?.Get(type); // ConditionRegistry 접근 필요
                        if (cond != null && !cond.Evaluate(node, ctx))
                        {
                            Debug.Log($"[CheckCost] 특수 공개 조건 미충족 ({type})");
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private bool CanPlayCard(Player player, CardInstance cardInstance, CardInstance parentCardInstance)
        {
            if (cardInstance.OwnerSeat != player) return false;
            if (cardInstance.CardStatus != CardStatus.Hand) return false;
            if (_gameState.TurnState.ActivePlayer != player) return false;
            if (_gameState.TurnState.Phase.Main != Phase.Main.Play ||
                _gameState.TurnState.Phase.Sub != (int)Phase.Play.Play) return false;

            var fieldTree = _gameState.GetFieldTreeById(player);
            var parentNode = fieldTree.GetNodeByInstanceId(parentCardInstance.InstanceId);

            // 부모 노드 정보가 없다면 Root로 간주하거나 오류 처리 (기본적으로 Setup에서 Root는 등록됨)
            if (parentNode == null)
            {
                Debug.LogWarning(
                    $"[Server Junction] 부모 {parentCardInstance.BaseData.Name} 노드를 찾을 수 없습니다. (Root 여부 확인 필요)");
                return true; // 안전하게 허용 (Root는 필드 트리의 최상단)
            }

            int currentChildCount = parentNode.ChildrenInstanceIds.Count;

            int junctionLimit = (parentCardInstance.CardStatus == CardStatus.FieldFront)
                ? parentCardInstance.BaseData.Junction
                : 1;

            Debug.Log(
                $"<color=yellow>[Server Junction Check]</color> 부모 {parentCardInstance.BaseData.Name}({parentCardInstance.InstanceId}) -> 현재 자식 수: {currentChildCount} / 카드 한계: {junctionLimit}");

            // 1. 카드 개별 한계 체크
            if (currentChildCount >= junctionLimit)
            {
                Debug.LogWarning(
                    $"<color=red>[Server Junction Block]</color> 카드 한계 도달! ({currentChildCount}/{junctionLimit})");
                return false;
            }

            // 2. 플레이어 전체 분기(Extra Junction) 체크
            // 분기(2개 이상의 자식)를 만들 때만 체크합니다.
            if (currentChildCount >= 1)
            {
                int extraJunctionSum = 0;
                foreach (var node in fieldTree.Nodes.Values)
                {
                    extraJunctionSum += Mathf.Max(0, node.ChildrenInstanceIds.Count - 1);
                }

                var pState = _gameState.GetPlayerStateById(player);

                Debug.Log(
                    $"<color=orange>[Server Field Check]</color> 플레이어 {player} 필드 -> 현재 Extra Junction 합계: {extraJunctionSum} / MaxJunction 한계: {pState.MaxJunction}");

                if (extraJunctionSum >= pState.MaxJunction)
                {
                    Debug.LogWarning($"<color=red>[Server Field Block]</color> 플레이어 한계 도달! 분기 배치 차단.");
                    return false;
                }
            }

            return true;
        }

        private bool CanRevealCard(Player player, CardInstance cardInstance)
        {
            if (cardInstance.OwnerSeat != player) return false;
            if (cardInstance.CardStatus != CardStatus.FieldBack) return false;
            if (_gameState.TurnState.ActivePlayer != player) return false;

            return _gameState.TurnState.Phase is { Main: Phase.Main.Play, Sub: (int)Phase.Play.Play };
        }

        private bool CanUseCard(Player player, CardInstance cardInstance)
        {
            if (cardInstance.OwnerSeat != player) return false;
            if (cardInstance.CardStatus != CardStatus.FieldFront) return false;
            if (_gameState.TurnState.ActivePlayer != player) return false;

            return _gameState.TurnState.Phase is { Main: Phase.Main.Play, Sub: (int)Phase.Play.Play };
        }

        private bool IsPlayerAlive(Player player)
        {
            var pState = _gameState.GetPlayerStateById(player);
            return pState != null && pState.LifeStatus == PlayerLifeStatus.Alive;
        }
    }
}