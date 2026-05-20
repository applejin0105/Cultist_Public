<img width="3840" height="1240" alt="Hero" src="https://github.com/user-attachments/assets/e353bb25-8070-4cdc-b088-83a3f2142d8c" />

# Cultist - 3인 멀티플레이 카드 게임

<img width="7680" height="4320" alt="Main" src="https://github.com/user-attachments/assets/21e34a03-98b6-4ef0-b5c1-35966d6a78d1" />

## Ⅰ. 프로젝트 개요 (Overview)

### 게임 소개 및 장르

게임 **Cultist**는 고대 지중해와 중동의 종교적, 인류학적 역사를 배경으로 하는 3인 멀티플레이 전략 카드 게임입니다. 플레이어는 정형화된 덱 기반 전투를 넘어, 카드들이 마치 뿌리처럼 뻗어나가며 자유롭게 분기하는 트리 구조(Tree Structure) 형태의 유기적인 필드 시스템을 통해 자신만의 교리와 세력을 확장해 나갑니다. 이 치열한 수싸움 속에서 시간의 시련을 견디는 거대한 종교로 거듭날 수도, 역사의 뒤안길로 사라진 유물 속 흔적으로 남을 수도 있습니다.

[![Steam](https://img.shields.io/badge/Steam-000000?style=for-the-badge&logo=steam&logoColor=white)](https://store.steampowered.com/app/4696600)
[![YouTube](https://img.shields.io/badge/YouTube-FF0000?style=for-the-badge&logo=youtube&logoColor=white)](https://www.youtube.com/watch?v=ZR8SCa53bXo)

### 개발 환경 및 기술 스택 그리고 개발 관점

**Client (Game Engine)**

* **Engine:** Unity 6000.3.8f1 (URP 17.3.0 / Universal Render Pipeline)
* **Language:** C# (.NET / Unity Scripting Runtime)
* **Architecture/Pattern:**
  * 레이어드 아키텍처 (Domain → Data → Systems → Effects → Network → Scene/UI, 단방향 의존)
  * Component-Based (Unity MonoBehaviour 기반)
  * Host-Authoritative 네트워킹 (서버 권위 모델)
  * Command + Interpreter 패턴 (JSON DSL 카드 효과 시스템)
  * 보조: Registry, State Machine, Event Bus(Observer), Repository
* Key Libraries:
  * Mirror — 고수준 네트워킹 (NetworkManager / SyncVar / Command·Rpc)
  * FizzyFacepunch + Facepunch.Steamworks — Steam P2P 트랜스포트 & 로비/인증
  * kcp2k — 로컬 IP 트랜스포트 (로컬 멀티플레이 제공)
  * DOTween / DOTween Pro (Demigiant) — UI·카메라 애니메이션 (Components/Effects)
  * Newtonsoft.Json — cardDB.json / cardsEffects.json DSL 파싱
  * Unity Input System 1.18.0 / TextMesh Pro / Unity UI (uGUI)

**Tools & Collaboration**
* **Version Control:** Git, GitHub
* **IDE:** Rider

<br>

> 이 프로젝트에서 프로그래머인 저의 역할은, 기획자가 머릿속에 그린 게임을 한 픽셀도 타협 없이 코드로 옮기는 것에 있습니다.
> 
> 따라서, **모든 구현의 완료 기준은 '동작한다'가 아닌, '기획 의도와 일치한다'에 두었습니다.**

## Ⅱ. 시스템 아키텍처 (Architecture)

멀티 플레이 카드 게임에서의 핵심은, 동기화입니다. 내가 카드를 냈다면, 상대도 그것을 인지할 수 있어야합니다. 멀티플레이에 대한 개념은 언리얼 엔진을 사용하면서, 그리고 유니티 Photon을 사용하면서 기초를 다져두었기에, 자세한 내용은 아래 포스팅으로 대체하겠습니다.

그러면 기본적으로, 다음의 과정을 통해 멀티플레이는 진행된다는 것을 대략적으로 생각해볼 수 있습니다.
1. 2~3대의 컴퓨터를 연결한다. (네트워크 소켓 열기)
2. "교역", "드로우", "카드 효과 발동"과 같은 정보를 바이트로 변환해서 보낸다. (직렬화)
3. 받은 바이트를 다시 해석한다. (역직렬화)
4. 패킷이 유실되면 재전송한다.
5. 외부 접속을 막는 문제(NAT)가 있다면 해결해야한다.
6. 중간에 누군가 '구라'를 쳤는지 검증해야한다.

다만, 이건 지금의 실력으로 전부 구현하는건 불가능하므로, 이 저수준(low-level) 네트워킹을 지원해주는 Mirro와 Steam Facepunch를 사용했습니다.
1~4번 과정은 일부는 Mirror가, 일부는 Transport가 담당하고, 5번은 Steam에서 담당하고, 6번 검증은 Mirror의 권위 모델과 제가 설계한 서버 코드에서 담당하게 됩니다.

### Mirror
Mirror는 Unity용 고수준(High-level) 네트워킹 라이브러리입니다. 앞선 6개의 과정을 `[SyncVar]` 키워드 하나로 깔끔하게 해결해줍니다. 만일
``` [SyncVar] public int CurrentRound; ``` 이렇게 필드를 선언했다면, 서버에서 이 값이 변경되면 Mirror 가 알아서 모든 클라이언트들에게 전파합니다.

이 프로젝트에서도 사용중인, Mirror에서 제공하는 메타 그리고 cs는 다음과 같습니다.

`NetworkManager`:	네트워크 전체를 켜고 끄는 총괄 매니저. 당신의 GameNetworkManager가 이걸 상속
`NetworkBehaviour`:	네트워크로 동기화될 수 있는 특별한 MonoBehaviour". GamePlayer, NetworkGameController가 이걸 상속
`[SyncVar]`: 이 변수를 모든 클라와 자동 동기화
`SyncList<T>`: 변수 대신 리스트(컬렉션)를 자동 동기화
`[Command]`: 클라에서 호출했지만 서버에서 실행되는 메서드
`[ClientRpc]` / `[TargetRpc]`:	서버에서 호출했지만 클라에서 실행되는 메서드
`NetworkServer.Spawn()`: 이 게임오브젝트를 모든 클라에도 똑같이 생성

`[Command]`의 경우 다음과 같이 작동하게 됩니다.
```[Command] public void Cmd_RevealCard(int cardInstanceId) { ... }```
이렇게 코드가 되어있을 때, Mirror에서는 다음과 같이 작동합니다.
```
작성한 코드
    ↓ C# 컴파일
컴파일된 DLL
    ↓ Mirror Weaver가 가로채서 코드 주입
"Cmd_RevealCard를 호출하면 → 실제로는 인자를 바이트로 직렬화 → 네트워크로 서버에 전송 → 서버에서 원본 메서드 실행"
하는 코드로 자동 변환
```
이때, Mirror에서 컴파일된 코드를 사용하는 것을 *위빙(Weaving)*이라고 합니다. 즉, **직렬화를 Mirror가 위빙을 통해 컴파일 시점에 자동 생성해줍니다.**

다만 이런 Mirror가 하지 않는 일도 있습니다. Mirror는 SyncVar, RPC, 직렬화와 같이 '무엇을 보낼지'는 다 해줍니다. 그런데... 실제로 그 바이트를 인터넷 어딘가 머나먼 그곳으로 보내는 것은 하지 않습니다. 이때 사용되는 것이 Transport입니다.

Transport는 **갈아끼울 수 있는 플러그인**입니다.
- KCP(`kcp2k`): UDP/IP 기반. 일반적인 인터넷 연결. (현재 프로젝트에서 로컬플레이 전용으로 사용중입니다.)
- Telepathy: TCP/IP 기반.
- FizzyFacepunch: Steam P2P 기반. 현재 프로젝트에서 실제 배포용으로 사용하고 있습니다.

즉, Mirror는 IP로 보내든 Steam으로 보내든 1도 신경 안씁니다. 접속 방식을 바꾸고 싶다면, Transport만 바꾸어 끼면서 로컬로할지 스팀으로 할지만 결정됩니다. 이 또한, 내부 코드로 구현되어있습니다.

### Facepunch

Facepunch.Steamworks는 C++ Steamworks SDK를 C#에서 편하게 부르도록 도와주는 C# 래퍼입니다. 유니티는 (내부 엔진 들어가면 C++) C#으로 게임을 짜기 때문에 C#에서 C++ DLL을 직접 부르는 건 매우 번거롭습니다. Facepunch.Steamworks는 (RUST를 개발한 게임사에서 만든 그 개쩌는 회사 Facepunch), C++로 만들어진 Steamworks SDK를 C# 코드에서 쉽게 접근이 가능하도록 다음과 같이 도와줍니다.

```
내부 SteamManager 코드의 작동 방식
   SteamClient.Init(Steam App ID)          ← Facepunch.Steamworks의 C# 함수
   SteamMatchmaking.CreateLobbyAsync()
        ↓ (래퍼가 내부적으로 변환)
   steam_api64.dll                    ← Valve의 진짜 C++ 라이브러리
        ↓
   Steam 클라이언트 / Valve 서버
```

이렇게 연결된 Steam P2P는 IP 주소 기반이 아닌, SteamID 하나로 상대를 찾고, NAT 통과(양쪽 공유기를 뚫는 기술을 Steam이 대신 해줍니다.), Relay(중계), 신원보증까지 해주며 무료로 해줍니다.

그러면 이제, 이 두 가지를 다음과 같이 분류해볼 수 있습니다.
- Mirror: "바이트를 보내줘잉" 라고 Transport에 시킴
- Facepunch.Steamworks: Steam P2P로 바이트를 보낼 줄 앎

그러면 이 둘을 연결하는 부품이 바로
FizzyFacepunch의 `FizzyFacepunch.cs`입니다.

> 요약하면 Mirror에서 고수준(SyncVar·Command·Rpc·직렬화·게임상태 동기화) 네트워킹을 담당하고, Tranport에서 저수준(실제 바이트 전송)을 담당합니다. 동기화해야 할 데이터가 존재하면 이를 Transport 계층으로 전송해서 동기화를 유지합니다.
>
> Steam은 인증(누구인지) + 로비/매치메이킹(방) + P2P 연결(NAT 통과·Relay)을 담당합니다.

그럼 이제, 본격적으로 내부 코드를 설명하며 이러한 Mirror와 FizzyFacepunch를 어떻게 멀티플레이 카드 게임에 적용하고 활용했는지 보여드리겠습니다.

### 참고자료

[네트워크 기초](https://waterglass0105.tistory.com/106)

[언리얼 엔진의 프레임 워크와 기초 그리고 네트워크](https://waterglass0105.tistory.com/91)

[유니티에서 Photon 사용하기](https://waterglass0105.tistory.com/135)

[언리얼 엔진과 네트워크](https://waterglass0105.tistory.com/105)

Photon까지 공부해보고 왜 Mirror + Steam을 사용했을까 라고 물어보실 수 있습니다. 본격적인 멀티플레이 구성을 진입하기 전, 저는 다음과 같은 기준들을 세워보았습니다.
1. 운영 비용 구조: 출시 전 검증되지 않은 단계에서, CCU(동시 접속자) 기반 반복 결제는 리스크이다.
2. 플랫폼 적합성: 타겟 플랫폼이 Steam이무로, 친구 초대·오버레이 참여가 네이티브여야 한다.
3. 로컬 플레이 보장: *기획자*의 요구는 '하나의 컴퓨터에서 여러개 쉽게 돌릴 수 있도록 테스트 환경을 제공해달라'이다.
4. 의존성 확인: 특정 클라우드 벤더에 게임의 연결 계층 전체를 묶는 것은 아무래도 좀 부담된다.

*다만 이렇게 되면 결국, 나중에 로비 기반 게임으로 업그레이드 해야할 때 다시금 코드를 뜯어 고쳐야하고, 그리고 무엇보다 편의성 측면에서 트레이드 오프가 생겨버렸습니다.* Photon이 주는 검증된 안정성과 편의(매니지드 인프라, 호스트 마이그레이션 등)를 포기하고, 트랜스포트 결선과 연결 수명주기를 직접 책임져야했고, 호스트 권위 P2P의 약점 (호스트가 나가면 세션 종료, 전용 서버 대비 보안·확장성 한계 -> 지금도 게임 중간에 나가면 그냥 끝나버립니다.)를 인지하고, 이를 감수하고있습니다.

전용 서버 자체로 만들어서 로비 구조는 쉽게 확장할 수 있습니다. 애초에 스켈레톤 코드를 구성할 때 이를 염두해 두고 작성했습니다. `NetworkGameController.OnStartServer()`가 `GameState`·모든 시스템·이펙트를 생성하고, 게임 로직은 로컬 클라이언트와 완전히 분리돼 있습니다. 하지만 이를 위해서는, 전용 서버 로직을 구현한다면 `SteamManager`에서 호스트 `SteamID` 기반 접속 로직을 수정해야합니다. 거기에 추가로 `RpcInitializeGameUI` → `InitUIRoutine`처럼 "호스트에도 로컬 플레이어가 있다"를 가정한 코드들을 전부 뜯어 고쳐야합니다.

**추후 로비 시스템 기반으로 게임을 확장하겠지만, 현재까지는 이러한 트레이드 오프를 감수하여 진행했습니다. 추후 확장할 로비 시스템은 개념만 다음과 같이 잡아두었습니다.**

#### 추후, 로비 생성 및 헤드리스 서버 확장: Steam Game Server API로 포팅
- Steam에는 클라이언트 API와 별개로 게임 서버 전용 API([SteamGameServer](https://partner.steamgames.com/doc/api/isteamgameserver?language=english)) 가 있고, 이건 사람 로그인 없이 익명 로그인!!!!!
- 트랜스포트를 이 API 기반으로 교체/포팅하면, 헤드리스 서버에서도 Steam 네트워킹이 돌고 친구 플레이도 유지
- 호스팅: 어느 옵션이든 Mirror 패키지에 들어 있는 Edgegap(서버 호스팅·오케스트레이션) 연동을 출발점으로 사용 가능

### 데이터 흐름 및 통신 방식
> 다이어그램과 함께, 데이터 흐름 및 통신 방식에 대해 설명하겠습니다.

1. 계층 스택 (정적 구조)
> 네트워크 코드는 6개의 계층으로 쌓여 있고, 각 계층은 바로 아래만 알기에 Transport 계층만 교체하면 통신 방식 전체가 변경됩니다.

<img width="1240" height="4045" alt="계층 스택" src="https://github.com/user-attachments/assets/641f54ef-276b-42d1-be03-32d8b957515c" />

코드의 정적 구조에 대해 먼저 설명하겠습니다. 앞서 설명한 Mirror와 Facepunch의 연장선입니다. 위로 갈수록 게임에 가깝고(고수준, Mirror), 아래로 갈수록 하드웨어에 가깝습니다.(저수준, Transport). 게임 코드는 Mirror만, Mirror는 `Transport` 추상 타입까지만, `FizzyFacepunch`는 `Facepunch.Steamworks`까지만 알고 그 아래는 알지 못합니다. 이러한 분리를 통해 `Transport` 자리에 무엇을 꽂느냐에 따라 통신 방식이 결정됩니다.

이 프로젝트에서는 이러한 `Transport`의 방식을 이용하여 FizzyFacepunch(Steam P2P)와 kcp2k(IP/로컬) 모두를 지원하고 있습니다.

2. 연결 수립 흐름 (게임 시작 전)
> 물리적으로 떨어진 PC들이 Steam에서 인증·로비를 거쳐, SteamID를 주소 삼아 P2P로 하나의 네트워크 세션에 묶습니다.

<img width="5697" height="5360" alt="2  Game Networking-2026-05-1 연결 수립 흐름9-112013" src="https://github.com/user-attachments/assets/833df760-405f-47a4-a567-9589d50722ba" />

각 PC는 독립적으로 부팅하여 `SteamClient.Init`으로 Steam에 인증하고 각자의 SteamID를 얻습니다. 이후 모든 연결의 *주소*는 **IP가 아닌 SteamID**로 설정됩니다. 호스트는 `SteamMatchmaking.CreateLobbyAsync(3)`로 로비를 만들고, `StartHost()`로 Mirror를 호스트모드(서버+플레이어 겸임)로 띄웁니다. `GameNetworkManager`의 `OnStartServer()`가 `NetworkGameController`를 스폰하고, 스폰된 NetworkGameController 자신의 OnStartServer()가 InitializeServerLogic()을 호출해 서버에만 존재할 게임의 실질적 두뇌(`GameState`·시스템·이펙트 등록)를 생성합니다. 클라이언트는 친구창에서 참가 시 `OnLobbyEntered`에서 `networkAddress`에 호스트 SteamID를 넣고 `StartClient()`를 호출하며, Transport가 이를 SteamID로 해석해 Steam P2P가 NAT을 뚫거나 Relay로 우회해 연결을 성사시킵니다. 연결되면 `OnServerAddPlayer()`가 플레이어 프리팹(`GamePlayer` + `LobbyPlayerState`)을 스폰하고, 이 객체가 양쪽 PC에 복제되며 연결된 PC들이 하나의 세션으로 구성됩니다.

현재 이러한 방식은, 호스트가 서버+플레이어 이므로 호스트 측에서 게임 데이터를 조작하거나 수정해버리면 이를 방지할 방법이 구축되어 있지 않으며, 호스트가 게임에서 나갈 경우 게임 세션이 강제 종료되는 한계가 존재합니다.

3. 로비 → 인게임 세션 셋업
> 모든 플레이가(호스트 제외) 준비가 끝나면 서버 주도로 인게임 씬으로 전환하고, 덱 제출·좌석 배정을 거쳐 게임 로직이 초기화됩니다.

<img width="4662" height="3770" alt="3  로비에서 인게임 세션 셋업" src="https://github.com/user-attachments/assets/c133682d-9143-4240-a5b9-1363428c727a" />

로비에서 각 플레이어는 `CmdSetReady(true)`로 준비 상태를 알리고 그 값은 `SyncVar`로 공유됩니다. 호스트를 제외한 전원이 준비 되었다면, 그리고 호스트가 Start 버튼을 눌렀다면 서버가 `ServerChangeScene("05_InGame")`으로 모든 클라이언트를 동시에 인게임 씬으로 전환합니다. **이때 씬 전환은 서버가 주도합니다!!!** 인게임 진입 후, 각 클라이언트들은 `CmdSubmitDeckData()`로 덱을 제출하고, 서버는 이때 `RegisterPlayer()`로 좌석 번호를 배정하며(`SyncVar`) 덱을 저장합니다. 서버는 `WaitForPlayersToStartGame` 코루틴으로 모든 덱이 도착할 때까지 기다린 뒤 `StartGameLogic()`을 실행해 `ServerGameState`를 초기화하고, 카드 상태(`SyncFullGameState`)와 UI 초기화(`RpcInitializeGameUI`)를 전원에 전파한 다음 루트 카드를 공개하고 첫 턴을 시작합니다.

4. 인게임 통신: 상태 동기화 vs 원격 호출
> 인게임 통신은 "변수를 감시하는 상태 동기화"와 "함수를 원격 실행하는 원격 호출(RPC)" 두 메커니즘으로 나뉩니다.

게임 진행 중의 모든 통신은 성격이 다른 두 메커니즘 중 하나에 속하여 진행됩니다. 편의상 메커니즘 A와 메커니즘 B로 부르겠습니다.

<img width="6472" height="4110" alt="4-1  통신 메커니즘" src="https://github.com/user-attachments/assets/116f2e25-3198-47c0-a1ea-9265ef15a950" />

메커니즘 A; 상태 동기화: `SyncVar`·`SyncList`는 변수·리스트를 **감시**하는 장치입니다. 서버가 `CurrentRound`나 `SyncCards`의 값이 바뀌면 Mirror가 자동으로 dirty 처리해 전파합니다. 함수 호출이 아닌, 값의 변화 자체가 통신이라 **수동적이고, 언제나 서버->전체 단방향**으로 이루어집니다. 클라이언트에서는 hook과 Callback이 발화해 화면을 갱신합니다.

<img width="1693" height="2940" alt="4-2  게임 진행 순환" src="https://github.com/user-attachments/assets/aabbabf0-010d-4459-936b-090e57356ad4" />

메커니즘 B; 원격 호출(RPC): 원격 컴퓨터의 함수를 실제로 실행하는 능동적 통신이며 방향에 따라 셋으로 나뉩니다.
  B-1 | `Command`: 클라이언트 -> 서버, 행동 요청
  B-2 | `ClientRpc`: 서버 -> 전체, 일회성 통보
  B-3 | `TargetRpc`: 서버 -> 특정 1명, 개별 지정

플레이어의 입력은 B-1로 서버에 도달하고, 서버가 검증·처리해 `ServerGameState`를 바꾸면 그 결과는 메커니즘 A로 전원에 자동 반영됩니다. 만일 일회성이라면 B-2를, 특정 플레이어의 선택이라면 B-3으로 끼워넣습니다. 따라서 **모든 결정은 서버에서만 내려지고 클라이언트는 요청(B-1)과 표시(A)만 담당합니다.**

5. 비동기 입력 브릿지
> TaskCompletionSource로 RPC 왕복을 await 한 줄로 바꿔, 서버 이펙트가 플레이어 입력을 기다렸다가 정확히 그 지점에서 재개합니다.

<img width="7232" height="3760" alt="5  비동기 입력 브릿지" src="https://github.com/user-attachments/assets/b83e708e-fb3d-4d21-b678-a23d8ed4ace7" />

서버의 카드 효과 실행기(`EffectRunner`)는 `async/await`로 한 줄씩 진행됩니다. 이는, 카드 효과 중 '파괴/희생할 카드를 선택'과 같이 플레이어가 *타겟*을 골라야 계속 진행되는 지점이 있기 때문입니다. 당연히 해당 플레이어는 다른 PC에 존재하고, 응답 시점을 알 수 없으므로 서버 로직은 **해당 플레이어의 입력이 올 때 까지 멈췄다가 응답이 오면 재개해야 합니다.**

이를 작동하게 만들어주는 것이 `TaskCompletionSource`로, 완료 시점을 직접 제어하는 Task입니다. 작동 방식은 다음과 같습니다.
  1. 서버 효과가 `await SelectTargetsAsync()`를 호출하면 `TaskCompletionSource`가 생성되어 플레이어별 상태에 저장됩니다.
  2. 해당 플레이어에게만 `TargetRpc` (B-3)가 발사됩니다.
  3. 서버는 이 시점에서 `await`으로 일시정지하고, B-3를 받은 플레이어는 선택 UI가 띄워지고, 이를 플레이어가 고르면 `Cmd_SubmitTargets()` (B-1)로 응답합니다.
  4. 서버는 이를 받아 `ReceiveTargetResponse()`에서 `tcs.TrySetResult()`를 호출하고, 이 순간 서버는 일시정지를 해제합니다.

카드 효과 코드를 짤 때, 개발자는 `await SelectTargetsAsync(...)` 한 줄만 쓰면 되고, 그 뒤의 네트워크 처리는 신경 쓸 필요가 없게 설계했습니다. 특히 이 부분에서 공을 많이 들였는데, 이유는 '확장성'과 '개발 편의성'에 있습니다. 기획자가 새로이 요구하는 새로운 효과들을 구현할 때, 네트워크에 엮여있으면 기능 하나를 구현하는데 시간이 많이 걸릴 것은 불보듯 뻔했습니다. 그래서, 이 부분에서는 적어도 카드 효과를 작성할때는 네트워크를 전혀 의식하지 않을 수 있게 구현했습니다.

6. 게임종료
> 승패 판정 결과를 SyncVar 두 개로 전원에 전파하고, 서버 주도로 로비에 복귀합니다.

<img width="3855" height="2750" alt="6  게임 종료" src="https://github.com/user-attachments/assets/275c5288-4fdf-4600-a01f-fcf5f0038d9b" />

서버의 `GameRuleSystem`은 게임의 상태가 바뀔 때마다 승리조건을 판정합니다. 조건이 충족되면 `TriggerGameEnd(winnerSeat)`가 호출되고, 여기서 종료 전파를 `SyncVar` 두 개(`WinnerSeat`·`IsGameEnded`)로 처리합니다. 서버가 이 값을 설정하면 Mirror가 모든 플레이어에게 자동으로 전파하고, 각 클라이언트의 `OnGameEndedHook()`이 발화해 게임 종료 UI와 승/패 사운드를 재생합니다. 마지막으로 서버는 5초 후 `ServerChangeScene("03_Lobby")`로 전원을 로비로 되돌립니다.

## Ⅲ. 핵심 기능 및 구현 로직 (Core Features)

### 인게임 로직 구현

#### Chapter 1. 게임 상태 모델
> 불변 데이터 `Card`, 런타임 `CardInstance` 그리고 서버 주도 `GameState`

##### [`Card.cs`](./Scripts/Domain/Entities/Card.cs)
> DB에서 로드되는 불변 카드 정의(이름·상징·효과 텍스트 등)

실질적인 카드의 데이터를 담고 있습니다. 카드가 가져야 하는 **모든 데이터**를 가지고있습니다.


##### [`CardInstance.cs`](./Scripts/Domain/Entities/CardInstance.cs)
> 게임 중 생성되는 런타임 카드 - 소유자·존·상태를 가짐

게임 중 생성되는 런타임 카드입니다. 어떤 카드 종류인지(`CardId`)와 게임 내 고유 식별자(`InstanceId`)를 함께 가지며, 서버는 이 `InstanceId`로 카드 한 장 한 장을 통제합니다.

설계상 `CardInstance`는 무거운 Card 객체를 직접 보관하지 않고 가벼운 `int`인 `CardId`만 들고 있습니다. 실제 카드 정의가 필요할 때만 `BaseData` 프로퍼티가 `CardCatalog`(`CardId` → `Card` 조회)를 통해 지연 조회합니다. 덕분에 네트워크 동기화 시에도 무거운 객체 대신 `int` ID만 오가게 되어, 통신·로직 부담을 최소화했습니다.

```csharp
        public int InstanceId { get; }
        public int CardId { get; private set; }
        public Player OwnerSeat { get; private set; }

        public Zone Zone { get; private set; }
        public CardStatus CardStatus { get; private set; }

        public Card BaseData => CardCatalog.Instance.Get(CardId);
```

##### [`IdGenerator.cs`](./Scripts/Utils/IdGenerator.cs)
> 덱 데이터를 인스턴스 ID가 부여된 `CardInstance`로 변환

앞에서 설명했듯, 카드는 그 자체만으로 서버에서 사용되기에는 무겁고, '누구의' 카드인지 구별할 수 없기에 `CardInstance`를 사용합니다. 그리고 이 `CardInstance`가 가지는 고유한, 겹치면 안되는 고유값인 InstanceId를 생성해주는 `static class`입니다. 각 플레이어의 Index를 굳이 나눈 이유는 여기에 있습니다. 플레이어 1이라면 100부터, 2라면 200부터, 3이라면 300부터 시작하는 ID 값을 가지게 됩니다. 100 200 300을 각각 root 카드로 설정하고, 남은 카드들에 고유 ID를 붙여 관리하고 있습니다.

##### [`GameState.cs`](./Scripts/Domain/State/Host/GameState.cs)
> 서버가 단독으로 소유·관리하는 게임 전체 상태

게임의 모든 상태가 이 한 클래스에 저장되어 있습니다. 카드 인스턴스, 플레이어 상태, 필드 상태, 각종 덱(플레이어 개별 덱·교역·사기사), 라운드별 행동 기록까지 **게임이 지금 상태**를 기록하는 클래스입니다. 이 객체는 오직 **서버(호스트)에서만 생성**되며, 클라이언트는 그 복제본만 받습니다. 서버 권위 모델의 중심이 되는 아주 중요한 클래스입니다.

이 클래스는 다음 두 가지에 초점을 두어 설계했습니다.
1. **모든 카드를 `InstanceId`로 찾을 수 있는 중앙 등록소**를 두었습니다. 카드가 덱에 있든 필드에 있든 교역소에 있든, 생성 시점에 `_cards` 사전에 등록되므로 `GetCard(instanceId)` 한 번이면 O(1)로 어떤 카드든 찾습니다. `CardInstance`에서 언급한 "서버는 `InstanceId`로 카드를 통제한다"의 실체가 바로 이 사전입니다. (카드 *종류* 조회인 `CardCatalog`와는 별개의 경로입니다.)
2.  **어떤 카드·플레이어가 게임에 존재하는지를 외부에서 함부로 바꾸지 못하게 캡슐화**했습니다. 내부 컬렉션은 전부 `private`이고, 외부에는 `IReadOnlyList`·`IReadOnlyDictionary` 또는 조회 메서드로만 노출합니다. 컬렉션 구성을 바꾸려면 반드시 `AddPlayer`·`RegisterCard`·`RecordAction` 같은 정해진 메서드를 거쳐야 합니다. 이는 만일 클라이언트가 쉽게 접근 가능한 자신의 DB json을 수정해서 게임을 망칠 수 있기에, 상태의 원본은 통제된 경로로만 변한다는 원칙을 코드 구조로 강제한 것입니다.

또한 카드 효과 시스템에는 `GameState` 전체가 아니라 `IEffectGameState` 인터페이스만 노출합니다. 이를 통해 상태를 *읽고 질문*할 수 있어도(`GetPlayerStat`, `GetHistoryCount` 등) 내부 구조에 직접 손대지는 못합니다.

```csharp
// 모든 카드의 중앙 등록소 — InstanceId로 O(1) 조회
private readonly Dictionary<int, CardInstance> _cards = new();
public IReadOnlyDictionary<int, CardInstance> Cards => _cards;   // 외부엔 읽기 전용 뷰만

public CardInstance? GetCard(int instanceId)
    => _cards.GetValueOrDefault(instanceId);

// 플레이어 상태도 동일 — 내부는 private, 외부는 읽기 전용
private readonly List<PlayerState> _players = new();
public IReadOnlyList<PlayerState> Players => _players;
```

##### [`PlayerState.cs`](./Scripts/Domain/State/PlayerState.cs)
> 플레이어별 상태(신도·심볼·필드·손패·생존 여부)

각 플레이어의 상태를 나타냅니다. 이 플레이어가 가질 수 있는 최대 종파 개수를 정의하며, 플레이어의 *다음 카드 가져오기 단계 규칙*에 대한 정보를 가지고 있습니다.

TurnSystem은 GameState에서 턴 주인의 PlayerState를 조회하고, 그 데이터를 바탕으로 게임을 진행합니다.

```csharp
        public void SetNextDrawRule(DrawRule drawRule)
        {
            NextDrawRule = drawRule;
        }

        public DrawRule ConsumeDrawRule()
        {
            var rule = NextDrawRule ?? DrawRule.Standard;
            NextDrawRule = null;
            return rule;
        }
```

##### [`DeckCollection.cs`](./Scripts/Domain/Structure/Deck/DeckCollection.cs)
> 덱 카드 순서를 다루는 LIFO 컬렉션

오직 Deck을 다루는 LIFO 컬렉션입니다. `IEnumerable<CardInstance>` 인터페이스를 통해 CardInstance들을 foreach로 순회할 수 있다는 계약을 명시하고있습니다. 이를 통해 `DeckCollection`에서 `foreach`를 통해 CardInstacne를 호출할 수 있습니다.

    - foreach 사용 가능 — foreach (var c in deck)
    - LINQ 전체 사용 가능 — .Where(), .Select(), .Count(), .FirstOrDefault(), .Any() … 이 모든 LINQ 메서드는 IEnumerable<T>에 대한 확장 메서드라서, 구현하는 순간 전부 켜집니다.

`DeckCollection`은 이렇게 `CardInstance`들을 LIFO로 관리하고, 카드 게임에서 덱 사용에 필요한 모든 로직을 수행하고 있습니다. 하지만, 게임의 System들에서 이를 직접 호출하고 사용하지 않습니다.

`DeckCollection`은 기본적으로 C++에서 제공하는 algorithm 자료형을 본따서 만들었습니다. 즉, `DeckCollection`을 직접적으로 사용하는게 아니라, 이 자료구조를 바탕으로 이어서 나올 `DeckState`에서 이를 사용하고 있습니다.

##### [`DeckState.cs`](./Scripts/Domain/State/DeckState.cs)
> 플레이어별 덱 - DeckCollection을 감싸는 façade

플레이어의 덱은 `DeckCollection`이라는 LIFO 컬렉션으로 관리되며, `DeckState`는 그 컬렉션을 감싸 플레이어 단위 덱 상태를 표현합니다.

Root 카드를 설정하고, `GameActionSystem`과 `TurnSystem` 그리고 `DrawCommand`에서 `DeckState`의 내부 함수를 호출해 카드를 뽑거나, 추가하는 등의 로직을 수행합니다.

##### [`FieldTree.cs`](./Scripts/Domain/Structure/Field/FieldTree.cs)
> 필드를 다루는 Tree 컬렉션

오직 PlayerField를 다루는 Tree 컬렉션입니다. 이 Tree의 경우, 다음의 구조 규칙을 가지고 있습니다.
- 모든 노드는 부모가 하나다
- `ChildrenInstanceIds` 목록과 실제 부모-자식 관계가 일치해야 한다
- `Nodes` 딕셔너리(`InstanceId` → `FieldNode`)이 트리 실제 구성과 어긋나면 안 된다
이 때문에 `FieldTree`의 `AddNode`·`GetAncestors`·`GetDescendants`는 이 규칙이 항상 참이라고 믿고 동작합니다. 그런데 누군가 `FieldTree`를 상속해 AddNode를 오버라이드하면(메서드가 virtual이 아니어도 new로 가리거나 부분 재정의 시) 이 규칙을 깰 수 있고, 그렇게 되면 이를 읽는 핵심 코드들인 `StatSystem`부터 시작해서, `NetworkGameController.SyncFullGameState`의 모든 계산이 틀린 값을 내게 됩니다. 이 때문에 이를 원천 차단하기 위해 `sealed`로 구현했습니다. 또한, 필드는 앞으로도 여러 종류가 없고, 무엇보다 `sealed`를 통해 오버라이드가 없음이 보장되므로 **JIT가 메서드 호출을 디버추얼라이즈·인라인** 할 수 있습니다. 물론, 지금 프로젝트 규모에서는 미미하지만 그래도 어느정도 최적화 이점입니다!

동일한 이유로, 대부분의 도메인 상태·자료구조의 경우 거의 다 `seald` 처리해두었습니다.

자료구조가 가져야 할 기본 덕목들은, 거기에 이 프로젝트에서 필요한 연산은 모두 구현해두었습니다. 기본적으로 필드 트리를 생성하고, 노드를 추가하거나 받아오고, 추후 Sect 조회가 필요한 경우 사용할 탐색 로직들을 구현했습니다.

##### [`FieldNode.cs`](./Scripts/Domain/Structure/Field/FieldNode.cs)
> 필드 트리의 노드(부모·자식 관계)

FieldTree에 들어가는 Node입니다. 자기 자신의 `InstanceId`와 자신의 부모 `ParentInstanceId`, 자식인 `ChildrenInstanceIds`를 가지고 이를 관리합니다.

##### [`FieldState.cs`](./Scripts/Domain/State/FieldState.cs)
> 플레이어별 필드 - FieldTree를 감싸는 façade

플레이어가 필드에 펼친 카드는 `FieldTree`라는 트리 구조로 관리되며, FieldState는 그 트리를 감싸 플레이어 단위 필드 상태를 표현합니다. 이 클래스는 초기 설계를 한 번 바로잡은 부분입니다.

처음에는 `FieldState`를 façade로 의도했지만, 실제로는 `GetFieldTree()`로 내부 `FieldTree`를 그대로 반환하고 있었습니다. 래퍼가 자기 내부 객체를 외부에 넘기는 순간 캡슐화는 이름뿐이 됩니다. 트리를 받은 쪽은 무엇이든 할 수 있고, 실제로 대부분의 시스템이 `FieldState`를 건너뛰고 raw FieldTree를 직접 조작했습니다. `FieldState` 자신의 메서드는 호출되지 않는 죽은 코드가 됐습니다.

```csharp
// Before
        public FieldTree GetFieldTree()
        {
            return PlayerFieldTree;
        }
```

원인은 같은 도메인의 `DeckState`와 비교했을 때 분명해졌습니다. `DeckState`는 내부 `DeckCollection`을 `private`으로 끝까지 숨겨, 누구도 자료구조에 직접 닿지 못하게 합니다. 두 클래스가 같은 의도(State가 자료구조를 감싼다)였는데, 한쪽만 그 의도를 지키고 있었던 것입니다.

그래서 `FieldState`를 진짜 façade로 재설계했습니다. `GetFieldTree()`를 제거해 `FieldTree`를 완전히 내부로 숨기고, 호출자에게 실제로 필요한 연산만 의도가 드러나는 메서드로 노출했습니다. `GameState`에서도 raw 트리를 넘기던 `GetFieldTreeById()`를 걷어내고 `GetFieldStateById()`로 경로를 일원화했습니다. 이제 필드 조작은 반드시 `FieldState`를 거치며, `DeckState`와 캡슐화 수준이 대칭을 이룹니다.

이렇게 다시금 리팩토링을 하는 과정에서, *래퍼 계층은 "존재 이유"를 벌어야 한다. 내부 객체를 그대로 반환하는 래퍼는 아무것도 보호하지 못한다. State는 자료구조를 숨기고, 외부에는 의도가 드러나는 좁은 API만 줄 때 비로소 façade가 된다.* 라는 중요한 사실을 학습할 수 있었습니다.

현재는 이와 연계된 모든 코드를 수정 및 관련 오류를 해결했습니다.

##### [`Phase.cs`](./Scripts/Domain/Enums/Phase.cs)
> 메인/서브 페이즈 열거형 정의

플레이어는 게임에서 크게 3개의 페이즈를 가집니다.

- `StandBy`: 턴이 돌아오기를 기다리는 상태
- `Draw`: 카드 가져오기 단계
- `Play`: 카드 내려놓기 단계

```csharp
        public enum Main
        {
            StandBy,
            Draw,
            Play,
        }
```

`Draw`는 다시 다음과 같이 3개의 상태를 가집니다.
- `StandBy`: 플레이어의 입력을 기다리는 상태
- `Draw`: 덱에서 카드를 가져옴
- `Trade`: 교역소에서 카드를 가져옴

`Play`는 카드를 내려놓거나 뒤집을 수 있는 공통 상태인 `Play` 상태만을 가집니다.

##### [`PhaseState.cs`](./Scripts/Domain/State/PhaseState.cs)
> 메인/서브 페이즈 값

플레이어의 Phase는 앞선 `Phase.cs` 내부의 enum들을 바탕으로 `PhaseState`에서 관리합니다. 페이즈 시스템의 골조는 보드게임에서 가져왔습니다. 특히 '엘드리치 호러'라는 게임을 팀원들과 플레이하며, 설명서를 여러 번 읽으며 기반을 다졌습니다.

물론 이 프로젝트는 엘드리치 호러를 비롯한 여타 보드게임과 다르게 페이즈가 복잡하지 않습니다. 하지만 추후 확장성과 페이즈 자체에 영향을 주는 카드도 존재함에 따라, `MainPhase.SubPhase` 형태로 접근이 가능하도록 — `GameState`를 비롯한 게임의 System이 플레이어의 현재 동작을 이 페이즈로 구분할 수 있도록 구현했습니다.

`PhaseState`의 한 인스턴스는 *현재 페이즈*를 `(Main, Sub)` 좌표 하나로 표현합니다. 가령 지금이 Draw 단계의 Trade 스텝이라면 내부적으로 `Main = Draw, Sub = 2`로 다뤄집니다.

`PhaseState`는 `class`가 아니라 **`readonly struct`** 입니다. 값 타입이라 가볍고, 불변이라 페이즈를 바꾸려면 새 인스턴스로 교체해야 합니다. 의도치 않은 수정이 일어날 여지를 구조적으로 차단했습니다.

```csharp
// 불가능 — 컴파일 에러
turnState.Phase.Sub = 2;

// 가능 — 새 인스턴스로 교체
turnState.Phase = PhaseState.From(Phase.Draw.Trade);
```

`Sub`를 enum이 아니라 `int`로 둔 이유는 — 단계마다 하위 스텝의 enum 타입이 다르기 때문입니다. `Phase.Draw`(StandBy/Draw/Trade)와 `Phase.Play`(Play)는 서로 다른 타입이라 한 필드에 같이 담을 수 없었습니다. 그래서 두 enum의 공통 표현인 `int`로 통합하고, 외부에서 `PhaseState`를 만드는 길은 `From` 메서드 오버로딩으로만 열어 잘못된 값이 들어올 길을 입구에서 막았습니다.

```csharp
public static PhaseState From(Phase.Draw step) => new PhaseState(Phase.Main.Draw, (int)step);
public static PhaseState From(Phase.Play step) => new PhaseState(Phase.Main.Play, (int)step);
```

생성자는 `private`이라 외부에서 `new PhaseState(...)`를 직접 호출할 수 없고, 다음 세 경로만 허용됩니다.

```csharp
PhaseState.StandBy                       // (Main=StandBy, Sub=-1)
PhaseState.From(Phase.Draw.Trade)        // (Main=Draw,   Sub=2)
PhaseState.From(Phase.Play.Play)         // (Main=Play,   Sub=0)
```

`From`은 인자의 enum 타입으로 `Main`을 자동 결정합니다 — `Phase.Draw`를 넘기면 Main=Draw, `Phase.Play`를 넘기면 Main=Play. 호출자는 `Main`을 명시할 필요가 없고, `(Main=Draw, Sub=Phase.Play.Play값)` 같은 모순된 조합은 만들 길이 없습니다.

`StandBy`는 하위 스텝이 없으므로 `Sub = -1`을 센티넬로 사용합니다.

`PhaseState`는 빈번하게 비교됩니다. 자연스러운 `==` 사용과 `Dictionary` 키 활용을 위해 비교 메서드들을 직접 구현했습니다.

```csharp
public bool Equals(PhaseState other) => Main == other.Main && Sub == other.Sub;
public override int GetHashCode() => HashCode.Combine(Main, Sub);
public static bool operator ==(PhaseState a, PhaseState b) => a.Equals(b);
public static bool operator !=(PhaseState a, PhaseState b) => !a.Equals(b);
```

마지막으로, `PhaseState`는 *"지금 어디인가"* 만 표현할 뿐 페이즈를 *전환*하는 책임은 `PhaseSystem`에 있습니다. 도메인 데이터와 시스템 로직의 분리 원칙을 일관되게 따랐습니다.

부수효과로, `Sub`가 이미 `int`라 `NetworkGameController`가 `SyncVar`로 페이즈를 전 클라이언트에 보낼 때 `(Main, Sub)`를 두 `int`로 그대로 쪼개 보낼 수 있습니다.

##### [`TurnState.cs`](./Scripts/Domain/State/TurnState.cs)
> 활성 플레이어·라운드·턴 순서

'자신의 턴'이 활성화된 플레이어, 현재 라운드, 턴 순서를 보관하는 클래스입니다. 대부분은 단순 상태값이지만, RemainingCycles 하나에는 설계 판단이 담겨 있습니다.

이 게임은 보드게임을 베이스로 해 '행동의 반복'이 잦습니다 — "카드 가져오기를 n번 반복", "교역을 한 번 더 진행" 같은 카드 효과가 많습니다. 초기에는 이를 게임 `Phase`를 되돌리는 방식으로 구현했는데, 페이즈가 꼬이고 드로우·교역이 막히며 라운드 카운팅 버그가 반복됐습니다. 원인은 하나였습니다. `Phase`는 "턴 안에서 지금 어디인가" 를 나타내는 값인데, 거기에 "턴을 몇 번 더 반복하는가" 라는 별개의 개념까지 떠맡긴 것이었습니다. 한 메커니즘이 두 책임을 겸하니 충돌이 났습니다.

그래서 반복 횟수를 `RemainingCycles`라는 독립된 값으로 분리했습니다. 한 턴에 수행 가능한 사이클(Draw → Play)이 몇 번 남았는지를 뜻하며, 표준은 1회, Stonehenge 같은 효과가 이 값을 늘립니다. 이제 `Phase`는 "턴 내 위치"만, `RemainingCycles`는 "반복"만 책임지므로 두 로직이 서로 간섭하지 않습니다. 이 분리는 뒤에 설명할 DrawRule과도 잘 맞물립니다.

##### [`GameActionRecord.cs`](./Scripts/Domain/History/GameActionRecord.cs)
> 행동 로그 - 조건 판정 시 과거 기록 조회용

GameActionRecord 시스템은 라운드별 행동 로그를 보관해, '한 라운드에 N번 무언가 했을 때'라는 조건을 기획자가 JSON으로 표현할 수 있게 합니다. 32번 카드는 'Destroy 1회 이상'을 통해 정상 작동했지만, 31번 카드의 'Trade 3회 이상' 조건은 `GameActionSystem.Trade()`에 `RecordAction` 호출이 빠져 있어 카드가 기획 의도대로 발동하지 않는 상태였습니다. 코드를 점검하며 발견한 갭으로, 한 줄 추가로 해결했습니다.

```csharp
if (selected != null)
{
    CardMovementSystem.MoveCard(_gameState, selected.InstanceId, player, Zone.Hand, CardStatus.Hand);
    _gameState.RecordAction(player, ActionType.Trade, targetPlayer: null, cardId: selected.CardId);  // ← 추가
    Debug.Log($"[Trade] {selected.BaseData.Name} 교역 완료");
}
```

##### [`DrawRule.cs`](./Scripts/Domain/Policies/DrawRule.cs)
> 드로우 방식 정책(표준/지정 등)

`Chapter 1`에서 설명하는 코드 중, Phase 시스템과 더불어 가장 공들여 만든 코드입니다. 초기 제작 단계에서 Effect 효과를 구현하는 과정에서 가장 어려웠던 것은 게임의 순서와 엮인 카드의 효과를 해결하는 것이었습니다. 앞선 Phase에서 보면 Draw Phase 다음으로 Play Phase가 진행되는데, 그렇다면 Play Phase에서 '카드 가져오기 단계를 한번 더 진행합니다' 와 같이 '다음 카드 가져오기 단계'를 수정한다면 해당 정보를 조금 더 체계적으로 가져올 필요가 있었습니다. 그렇지 않으면 `GameState`에서 별도로 '이 플레이어만 1회 더 진행해'라고 진행한다면 (초기에는 이렇게 진행했습니다) 예기치 못한 상황으로 필드의 페이즈와 턴 시스템이 꼬여버려 **한 플레이어의 턴이 무한히 반복되거나 '선택'이라는 개념의 드로우/교역이 멈춰버리는 현상이 지속적으로 발생했습니다.**

이를 해결하기위해 여러 방법을 모색하던 중, OOP의 기초를 다시금 머릿속으로 생각해보았습니다. 답은 간단했습니다. 복잡한 Draw 로직 자체를 클래스로 만들어서, Rule로 설계하고 턴이 시작될 때 System이 플레이어의 DrawRule을 읽게해서 관리한다면? 이 과정에서 '카드 가져오기 단계를 생략하고 신도가 n인 카드를 뽑는다'와 같이, 플레이어의 선택을 스킵하고 원하는 로직을 진행시킬 수 있게 구성하였습니다.

```csharp
        public static DrawRule Standard => new DrawRule
        {
            Type = DrawType.Draft,
            SkipSelection = false,
            Amount = 3,
            CardCondition = null
        };
```

##### `RevealReason.cs`(./Scripts/Domain/Enums/RevealReason.cs)
> 카드 공개 호출 사유

카드의 공개 조건을 나타내는 enum입니다. 추후 카드 효과가 추가되면 이곳에 추가 가능합니다. 추후 Effect 관련 로직에서 자주 사용합니다.

##### [`DeterministicTreeLayout.cs`](./Scripts/Domain/Structure/Field/DeterministicTreeLayout.cs)
> 필드 시각화 및 드롭존 처리

<img width="624" height="626" alt="Screenshot 2026-05-20 221608" src="https://github.com/user-attachments/assets/24a87722-d3ec-4df4-8ecd-d4f9cb78a08b" />

[라인 테스트 영상](https://youtu.be/THyzJLCemfU)

초기 기획 의도는 '마치 나무 뿌리가 무질서하게 뻗어나가는 느낌으로 필드를 채워 넣고, 그걸 멀리서 바라봤을 때의 심미적인 효과'를 요구했습니다. 이에 맞추어 랜덤하게 카드를 생성하면서도 겹치지 않는 로직을 구현했으나, 이는 기각되었고 결국 '정형화된 카드 필드'와 함께 '카드를 놓는 방식에 따라 모양이 바뀌는 구조'로 정립되었습니다.

우선 이를 구현하기 위한 조건을 생각하며, 스스로 대답해보았습니다.
1. Field는 일정한 간격을 가져야한다.
   Q. 그럼 Vertical Layout Group을 사용할 수 있을까?
   A. 안 된다. VLG는 한 줄로 쌓는 1차원 컴포넌트인데, 필드는 한 부모 밑에 자식이 여러 갈래로 펼쳐지는 트리 구조라 표현이 안 된다. 자식이 1명이면 부모 위로 일직선, 2명 이상이면 좌우 대칭... 이런 트리 특유의 배치 규칙은 자동 LayoutGroup으로 흉내 낼 수 없다.
   그러면? root 카드를 content 영역 중앙에 고정시키고, 그 위로 카드가 추가될 때마다 각 노드의 서브트리 폭을 계산해 일정한 간격으로 깔끔하게 배치되도록 직접 좌표를 결정하면 된다.
2. 그럼 Tree의 레이아웃은 얼마만큼 '자주' 갱신되어야할까?
   카드를 내려놓으면 DropZone이 생성되어 '어디에 카드를 놓을 지' 결정해야한다. 이 때, 전체 트리 모양이 어떻게 변경되는지 미리보기가 되어야한다. -> 기획 의도
   그럼 Tree가 갱신되는 조건은?
   - Dropzone 이 생성될 때 -> 다른 주변 모든 카드는, 해당 드롭존에 맞춰서 '넓어'져야함.
   - 취소해서 카드가 다시 Hand로 돌아오고 DropZone이 사라질때 -> 취소하고 원상복구
   - DropZone을 선택해서 카드가 해당 위치에 부착될 때 → 슬롯이 사라지고 새 카드 한 장이 남으므로, 미리보기 상태보다는 좁아지고 원래 상태보다는 한 자식 폭만큼 넓어진 모양으로 일관되게 재정렬되어야 함.

그럼 메서드 단위로 이를 분석하고 정리해보겠습니다.

- `CalculatePositions(...)`: 전체 레아아웃 계산을 관리하는 Entry Point입니다. 전달받은 트리를 저장하고, 계산용 딕셔너리를 초기화합니다.
  - Bottom-Up: `CalculateSubtreeWidth`를 호출, 자식 노드부터 부모 노드 방향으로 각 서브트리가 차지하는 전체 너비를 계산
  - Top-Down: `AssignPositions`를 호출하여 부모 노드부터 자식 노드 방향으로 실제 좌표를 할당
  이를 통해, 최종적으로 모든 노드의 좌표가 담긴 딕셔너리를 반환합니다.

- `CalculateSubtreeWidth(int nodeId)`: 특정 노드를 루트로 하는 서브트리(Subtree)가 가로로 얼마만큼의 공간을 차지하는지 재귀적으로 계산합니다.
  - 자식이 없는 경우(Leaf Node): 자기 자신의 너비(`_cardWidth`)만 반환
  - 자식이 1명인 경우: 자식 노드 위로 직진해서 올라가므로 자식의 너비를 그대로 가져와 반환
  - 자식이 2명 이상인 경우: 모든 자식들의 서브트리 너비를 합산하고, 그 사이사이에 들어갈 여백 `_paddingX`을 더한 총합을 자신의 너비로 결정. 계산된 값은 추후 재연산을 막기 위해 `_subtreeWidths` 캐시에 저장

- `AssignPositions(int nodeId, Vector2 pos)`: 앞서 계산된 서브트리 너비 데이터를 바탕으로, 각 노드의 최종 2D 좌표를 부여합니다.
  - 현재 노드(`nodeId`)에 전달받은 좌표(`pos`)를 최종 타겟 좌표로 저장
    - 자식이 1명인 경우: 분기할 필요가 없으므로 부모와 동일한 X축을 유지한 채 Y축 방향으로만 이동하여 자식을 배치
    - 자식이 2명 이상인 경우: 현재 노드에 할당된 전체 공간(`totalWidth`)의 가장 왼쪽 지점(`currentX`)을 계산. 이후 자식들을 순회하며 각 자식이 가진 고유 너비의 '절반' 위치에 중심점을 잡아 균등하고 대칭적으로 자식들을 배치. 하나를 배치할 때마다 `currentX`를 이동시켜 다음 자식의 시작 위치를 갱신.

##### [`UICurvedLine.cs`](./Scripts/Domain/Structure/Field/UICurvedLine.cs)
> 필드에 존재하는 카드를 연결하는 CurvedLine

카드를 소환했으니, 그 카드가 '연결' 되어있다는 느낌을 주기 위해서는, 이 '트리'가 정상적으로 연결되어있음을 표시하기 위해서는 '선'이 필요합니다. 이 선은 '곡선' 형태여야하고, 부드럽게 연결되어야합니다.

그럼 메서드 단위로 이를 분석하고 정리해보겠습니다.

- `Awake`: 현재 객체의 `RectTransform`을 가져와 크기(`sizeDelta`)를 가로세로 20000이라는 매우 큰 값으로 설정합니다. 이는 UI 요소가 화면 밖으로 나갔다고 판단되어 Unity의 UI 시스템에 의해 렌더링이 잘리는(`Culling`) 현상을 방지하기 위해 강제적으로 설정했습니다.

- `DrawCurve(Vector2 startLocalPos, Vector2 endLocalPos)`: 곡선의 시작점과 끝점을 갱신하고 다시 그리기를 요청하는 메서드입니다. 이전 좌표와 새로 입력된 좌표의 차이(`SqrMagnitude`)가 0.1f 미만이면, 변경 사항이 없다고 판단하여 연산을 취소(`return`)하여 성능을 최적화합니다. 만일 좌표가 변경되었다면, 새로운 점들을 저장하고 `SetVerticesDirty()`를 호출합니다.

- `OnPopulateMesh(VertexHelper vh)`: UI 그래픽의 실제 정점(`Vertex`) 데이터를 구성하는 핵심 메서드입니다. 동작은 다음과 같습니다.
  1. `vh.Clear()`를 통해 기존 메쉬 데이터를 초기화합니다.
  2. 곡선을 만들기 위한 4개의 Control Point를 설정합니다.
     - p0: 시작점
     - p1: 시작점에서 수직(curveVerticalForce)으로 뻗어 나가는 제어점
     - p2: 끝점에서 수직 방향 아래로 내려오는 제어점
     - p3: 끝점
  3. 이 4개의 점을 바탕으로 설정된 `segments` 개수만큼 반복문을 돌며 곡선 위의 중간 점(Points)들을 계산하여 리스트에 담습니다.
  4. 계산된 점들을 순회하며 `CreateLineSegment`를 호출해 실제 선분을 그립니다.

- `CalculateCubicBezierPoint(...)`: 3차 베지어 곡선 공식에 따라 진행도 $t$ ($0 \le t \le 1$)에 위치한 2D 좌표를 계산합니다.
  - $P(t) = (1-t)^3 P_0 + 3(1-t)^2 t P_1 + 3(1-t) t^2 P_2 + t^3 P_3$

<img width="10200" height="14039" alt="img007 (2)" src="https://github.com/user-attachments/assets/7bf2432b-7b32-4e6d-982a-a13bbc1da30f" />

~~오랜만에 풀어봐서 즐거웠다~~

- `CreateLineSegment(...)`: 두 개의 점(start, end)을 연결하는 두께를 가진 사각형 메쉬(Quad)를 생성합니다. 각각의 방향 벡터(`direction`)를 구한 뒤, 이를 90도 회전시켜 선분의 두께를 결정할 법선 벡터(`normal`)를 계산합니다. 하나의 선분을 그리기 위해 4개의 정점(Vertex)을 생성합니다. 각 정점의 위치는 중심선에서 법선 벡터를 더하거나 빼서 구하고, 텍스처 매핑을 위한 UV 좌표와 색상을 할당합니다. 마지막으로 `vh.AddTriangle()`을 두 번 호출하여 4개의 점을 2개의 삼각형으로 이어 사각형(Quad)을 완성합니다.

<img width="760" height="540" alt="법선1" src="https://github.com/user-attachments/assets/2c118bfa-9f77-49c2-a60a-dbea9431555a" />

<img width="750" height="560" alt="법선2" src="https://github.com/user-attachments/assets/dcc8a70e-541b-47bc-bf36-6bc5ddceec64" />

[이 Curve Line은 대학생때 배운 Computer Animation에서 Laplician Editing 개념을 상기하며 구성해보았습니다.](https://waterglass0105.tistory.com/67)

#### Chapter 2. 시스템
> 카드 이동, 카드 액션, 카드 필드 그리고 덱 생성과 불러오기

##### [`CardMovementSystem.cs`](./Scripts/Systems/CardMovementSystem.cs)
> 카드가 이동하는 단일 경로(출발지 제거 → 도착지 추가)

카드의 이동을 담당하는 시스템입니다. 카드 게임, 특히 핸드, 덱, 필드, 교역소로의 이동이 매우 활발하고 자주 이루어지기 때문에 이동간의 에러가 발생하거나 문제가 생기는 경우를 사전에 방지하고자 구현한 시스템입니다.

게임 내에서 지원하는 모든 카드 이동을, 각각에 맞게 모두 지원하고있습니다.

##### [`GameActionSystem.cs`](./Scripts/Systems/GameActionSystem.cs)
> Draw, Play, Reveal 규칙 관리 시스템

게임의 규칙을 관리하는 시스템입니다.

[셋업]
- `GameActionSystem(GameState, IPlayerInputProvider)`: `GameState`와 입력 제공자를 주입받아 보관. `StatSystem`도 `GameState`로부터 가져옴.
- `SetGameRuleSystem`: 승패 판정 시스템 주입. StatSystem에도 같이 전달 (순환 참조 피하려고 사후 주입)
- `SetEffectRunner(EffectRunner)`: 이펙트 실행기 주입. `OnReveal`·`OnHand`·`OnDestroyed` 같은 트리거를 발화시킬 때 사용
- `SetTargetResolver(TargetResolver)`: 타겟 해석기 주입. `CardCondition` 기반 드로우 등에서 후보 카드 검색에 사용
- `CheckGameRules`: 행동 후 GameRuleSystem의 필드/스탯 조건 검사 호출. 승패 갱신 트리거

[드로우]
- `Draw(Player, DrawRule)`: Draw Rule을 기반으로 플레이어의 Draw를 처리
- `FetchCardAsync(Player, DrawRule)`: 조건부 검색이거나 일반 덱 pop을 통해 카드를 확보. 덱이 비어있다면 사기사 카드 자동 드로우
- `ResolveDraftDraw(Player, List<CardInstance>)`: N장 보여주고 1장 선택. 선택된 건 손패로, 나머진 교역소로
- `ResolveSimpleDraw(Player, List<CardInstance>)`: Card Effect 중 OnHand 트리거 발화 시 작동. 가져온 카드 전부 손패로.

[교역·기아]
- `Trade(Player)`: 교역소에서 카드 1장 선택해 손패로. OnHand 트리거. (앞에서 `RecordAction(ActionType.Trade)` 누락이 이 부분이었습니다. 흑흑... 어쩐지...)
- `Starve(Player, amount, shuffle)`: 플레이어 덱에 기아 카드 N장 추가하고 셔플

[공개]
- `Reveal(Player, CardInstance, RevealReason)`: 카드 공개의 풀 파이프라인. 카드 효과에 따라 검증을 건너뛰는(Echo) 분기까지 모두 처리
- `CheckRevealRequirement(Player, CardInstance)`: 공개 비용 검증
  - 자살 방지(Cultist가 충분한가?) &&
  - 요구 심볼 충족?(SymbolR) &&
  - JSON 정의 RevealCondition 충족?
- `CanRevealCard(Player, CardInstance)`: 공개 가능한 상태 검증
  - 본인 차례 &&
  - Play 페이즈 &&
  - FieldBack 상태인지

`Reveal`의 내부 단계는 대략적으로 다음과 같습니다.
  1. 사유별 검증 우회 (Echo는 비용 우회, Manual은 전부 검사)
  2. IsUniqueReveal 같은 Feat 제약 검사
  3. CheckRevealRequirement (비용·조건)
  4. OnRevealCost 트리거 (비용 지불, Cancel 가능)
  5. CardMovementSystem.MoveCard → FieldFront
  6. 사운드·보이스 RPC (사기사는 전체방송, 일반은 본인)
  7. StatSystem.UpdatePlayerStats + 동기화
  8. OnReveal 트리거 (Echo면 isEcho=1 변수 주입)
  9. CheckGameRules

[카드 내려놓기]
- `Play(Player, handCard, parentCard, slotIndex)`: 손패 카드를 필드 트리에 자식으로 삽입. 배치 후 `IsRevealImmediately`가 `true`라면, 자동 `Reveal`
- `CanPlayCard(Player, hand, parent)`: 카드 내려놓기 & 사용하기 검증
  - 본인 손패/턴/Play 페이즈 &&
  - 부모 카드의 Junction 한계 &&
  - 플레이어 MaxJunction

`Play`의 내부 단계는 대략적으로 다음과 같습니다.
1. CanPlayCard 검증
2. FieldState.GetNodeByInstanceId로 부모 노드 확보(없으면 생성. 없다는건? 루트카드라는거~)
3. 새 FieldNode 만들어 InsertChild(slotIndex, ...)
4. CardMovementSystem.MoveCard → Zone.Field, FieldBack
5. 배치 사운드 RPC
6. StatSystem.UpdatePlayerStats
7. IsRevealImmediately면 즉시 Reveal 호출

[카드 사용하기]
- `Use(Player, CardInstance)`: 뒷면으로 존재하는 카드 혹은 앞면으로 존재하는 카드 중 `OnClick` 트리거가 존재하는 카드를 클릭했을 때 `OnClick` 트리거 발화.
  - 본인 차례
  - Play 페이즈
- `CanUseCard(Player, CardInstance)`: 사용 가능 검증을 한 곳에 모아둔 헬퍼

[파괴·추방]
- `Destroy(Player, targetCard)`: 신도 카드 파괴. Echo 분기(다른 플레이어가 IsEcho 카드를 파괴 시도하면 파괴 대신 공개). OnPreDestroy → 이동 → OnDestroyed 트리거. 복제본을 교역소에 추가 (Crisis 제외)
  - 앞면 카드는 파괴 불가 (게임 룰).
  - `IsEcho` 카드를 다른 플레이어가 파괴하려 하면 → Reveal(RevealReason.Echo)로 전환.
  - `IsCrisis` 카드는 복제본 미생성. (Exile과 동일하게 취급)
- `Exile(Player, targetCard)`: 파괴와 거의 동일하되 복제본 생성 안 함. `ActionType.Exile`로 기록. `OnDestroyed` 트리거는 공유!

[유틸]
- `IsPlayerAlive(Player)`: `PlayerState.LifeStatus` == Alive 확인. 모든 public 액션의 첫 줄에서 호출. **탈락자 액션 차단**

즉, `Draw`, `Trade`, `Starve`, `Reveal`, `Play`, `Use`, `Destroy`/`Exile`은 다음의 **공통 패턴**을 가지고 설계했습니다.
  1. IsPlayerAlive 체크 (탈락자 차단)
  2. 본인 차례/페이즈 검증 (Can*Card 헬퍼)
  3. 비용·조건 검증 (특히 Reveal)
  4. 실제 상태 변경 (CardMovementSystem 등)
  5. 사운드 RPC
  6. StatSystem 갱신 + 동기화
  7. 관련 트리거 발화 (OnReveal, OnHand, OnDestroyed...)
  8. CheckGameRules (승패 판정)

##### [`FieldSystem.cs`](./Scripts/Systems/FieldSystem.cs)
> 카드를 필드 트리에 배치하는 필드 조작 로직

##### [`DeckSystem.cs`](./Scripts/Systems/DeckSystem.cs)
> 게임 로직이 덱을 다루는 진입점
##### [`DeckRepository.cs`](./Scripts/Data/Repositories/DeckRepository.cs)
> 파일 시스템·JSON 직접 IO (데이터 접근 계층)

`DeckSystem`과 `DeckRepository`은 두 클래스가 `Repository` 패턴으로 짝지어 동작합니다.
  - `DeckRepository`: 덱이 어디 저장되어 있고, 어떻게 읽고·쓰고·검증되는지 만 안다. 게임 룰은 모름.
  - `DeckSystem`: 게임에 어떤 덱을 어떻게 투입할지 만 안다. 파일 위치·포맷은 모름.

`DeckSystem`이 `DeckRepository`를 생성자로 주입받습니다. (Repository 패턴)

##### `DeckRepository`

게임의 모든 덱은 두 종류로 나뉘고, 각각의 JSON에 배열 형태로 저장되어있습니다.
  - `샘플 덱`(`SampleDeckDBTargetFilePath`): 기본 제공되며, **IsSmaple = true. 삭제 불가**입니다.
  - `플레이어 덱`(`PlayerDeckTargetFilePath`): 사용자가 만들고 저장한 덱

`LoadAllDecksAsync`를 통해 이 두 소스를 모두 읽고, 덱을 불러옵니다. 샘플 덱 이름은 플레이어가 사용할 수 없게 하려는 기획 의도가 있었지만, 저장 진입점(`SaveCurrentDeckAsync`)에서 중복 검사가 플레이어 덱 목록에만 한정되어 있어 의도가 강제되지 않는 상태였습니다. 로드 시 충돌이 일어나면 플레이어 덱이 우선되는 `fallback`이 있었지만, 이는 '발생하면 안 되는 상황'을 처리하는 보험일 뿐 의도를 직접 반영한 코드가 아니었습니다. 샘플 이름 집합을 유지하고 저장 입구에서 차단하는 1차 방어를 추가해 의도를 코드로 정착시켰습니다.

```csharp
            if (_sampleDeckNames.Contains(_currentDeckData.deckName))
            {
                Debug.LogWarning($"[DeckRepository] '{_currentDeckData.deckName}'은 샘플 덱 이름이라 사용할 수 없습니다.");
                return false;
            }
```

파일 입출력의 경우, 동시성 문제가 발생할 수 있습니다. 읽는 중에 쓰기가 동시에 일어나면 파일이 깨져버릴 수 있기에, `SemaphoreSlim`을 적용했습니다. `LoadPlayerDeckAsync`와 `SavePlayerDeckToFileAsync`가 이 락을 거칩니다. 오직 단 1개의 동시 접근만 허용하였습니다. 그리고 `await _fileLock.WaitAsync()` → 작업 → `try/finally`로 항상 `Release()`하여 안정성을 높였습니다.

```csharp
private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
```

각 메서드에 대한 간단한 설명은 다음과 같습니다.

[로딩]
- `LoadAllDecksAsync()`: 샘플 + 플레이어 덱 전부, 이름→DeckData 딕셔너리로 저장
- `LoadPlayerDeckAsync()`: 플레이어 덱 파일만, 동시성 락 통과

[편집] - 현재 편집 중인 덱 `_currentDeckData` 한 개를 잡고 작업합니다.
- `CreateNewDeck(name, rootCardId)`: 새 덱 시작
- `LoadDeckForEditingAsync(name)`: 기존 덱을 편집 모드로 불러옴 (원본 이름 `_originalEditingDeckName`도 저장)
- `AddCardToCurrentDeck(cardId)`: 카드 추가, Cultist 값 기준 자동 정렬
- `RemoveCardFromCurrentDeck(cardId)`: 카드 제거
- `ClearCurrentDeck()`: 편집 상태 초기화

[저장·삭제]
- `SaveCurrentDeckAsync()`: 30장 정확히 채웠을 때만 저장. 원본 이름이 있으면 덮어쓰기로 처리
- `DeleteDeckAsync(name)`: 샘플 덱은 삭제 불가

[검증·제약]
- `SanitizeDecks()`: 카드 카탈로그에 없는 카드/잘못된 루트 제거
- `IsContainOver3(id)`: 같은 카드 3장 초과 금지
- `IsCollectible 체크`: 수집 불가 카드(Card.IsCollectible == false)는 덱에 못 넣음
- `IsRoot 분리`: 루트 카드는 cardIds에 안 들어가고 별도 rootCardId 필드로

[이름 중복 방지 — Regex]
```csharp
        private string GenerateUniqueDeckName(List<DeckData> existingDecks, string deckName)
        {
            if (existingDecks.All(d => d.deckName != deckName)) return deckName;

            int maxIndex = 0;
            var pattern = $@"^{Regex.Escape(deckName)}_(\d+)$";

            foreach (var deck in existingDecks)
            {
                if (deck.deckName == deckName) continue;
                var match = Regex.Match(deck.deckName, pattern);
                if (match.Success)
                {
                    maxIndex = Math.Max(maxIndex, int.Parse(match.Groups[1].Value));
                }
            }

            return $"{deckName}_{maxIndex + 1}";
        }

```

프로그래머가 사랑하는 정규식입니다. 원하던 효과는 덱 이름이 이미 있으면 `_숫자`를 붙여서 유일한 이름으로 만들어서 돌려주는 것이고, 그 숫자는 같은 베이스 이름을 가진 기존 변형들 중 가장 큰 인덱스 + 1로, 윈도우의 파일 시스템 생성처럼 진행하고자 했습니다.

코드를 하나씩 살펴보겠습니다.

  1. 조기 반환
    ```if (existingDecks.All(d => d.deckName != deckName)) return deckName;```
    All(...)은 "리스트의 모든 원소가 조건을 만족하는가?" 를 묻습니다. 여기선 "모든 기존 덱의 이름이 이 이름과 다른가?" 즉, "이 이름이 어디에도 없는가?". 답이 true 면 충돌이 없으니 입력 이름 그대로 반환합니다.
  2. 핵심 정규식
    ```var pattern = $@"^{Regex.Escape(deckName)}_(\d+)$";```
    문자열 보간 + verbatim 문자열($@"...") 안에서 정규식 패턴을 만듭니다.
    
    | 토큰 | 뜻 |
    | :--- | :--- |
    | `^` | 문자열 시작 |
    | `{Regex.Escape(deckName)}` | 입력된 덱 이름(메타 문자 이스케이프 처리됨) |
    | `_` | 리터럴 언더스코어 |
    | `(\d+)` | 숫자 1개 이상을 캡처 그룹 1번으로 잡음 |
    | `$` | 문자열 끝 |

  3. 기존 덱들 훑으며 최대 인덱스 찾기
    ```csharp
        foreach (var deck in existingDecks)
        {
            if (deck.deckName == deckName) continue;     // 베이스 이름 자체는 건너뜀
            var match = Regex.Match(deck.deckName, pattern);
            if (match.Success)
            {
                maxIndex = Math.Max(maxIndex, int.Parse(match.Groups[1].Value));
            }
        }
    ```
  4. 최종 반환
    ```return $"{deckName}_{maxIndex + 1}";```

##### `DeckSystem`

`DeckSystem`은 비교적 간단합니다. Repository 주입 받아 보관하고, 모든 덱 데이터를 이름→DeckData 딕셔너리 형태로 캐싱합니다. 그리고 초기화 1회 보장 플래그를 통해 덱을 로딩시킵니다.

```csharp
private readonly DeckRepository _deckRepository;
private Dictionary<string, DeckData> _deckDataCache;
private bool _isInitialized = false;
```
- `Initialize()`: Repository에서 모든 덱을 불러와 `_deckDataCache`에 적재. `_isInitialized`로 이중 초기화 방지.
- `CreateDeckState(Player player, string deckName)`: 캐시에서 덱 이름으로 `DeckData`를 찾고, `IdGenerator.ReturnInstanceIdDeck`로 인스턴스 ID가 부여된 DeckState 를 만들어 반환. 이를 통해 **카드마다 교유 `InstanceId`를 부여하고, 이것으로 서버가 카드를 식별합니다.**
- `Reload()`: `_isInitialized = false`로 리셋 후 다시 `Initialize`. 덱 데이터를 외부에서 수정한 뒤 캐시 갱신용.

즉, 이 두가지 클래스는 다음과 같이 작동하게 됩니다.
```
NetworkGameController.InitializeServerLogic()
  └ var deckRepo = new DeckRepository(CardCatalog.Instance);    // 1. 주입 대상 준비
  └ _deckSystem = new DeckSystem(deckRepo);                     // 2. 주입
  └ await _deckSystem.Initialize();                             // 3. 캐시 채움
       └ _deckRepository.LoadAllDecksAsync()
            ├─ 샘플 덱 JSON 파싱
            └─ 플레이어 덱 JSON 파싱 → 병합
```

*DeckSystem은 인게임 진입 시점의 진입점, DeckRepository는 덱 편집 UI의 진입점*으로 구분하였습니다.

여기에서 한가지 과거의 잔재가 있습니다.
```csharp
// NetworkGameController.StartGameLogic 내부
DeckData dData = InGameSessionManager.Instance.GetPlayerDeck(pComp.netId);
var deckState = Utils.IdGenerator.ReturnInstanceIdDeck(dData, (Player)i);
```
실제 인게임에서 플레이어의 덱이 `ServerGameState`로 들어가는 경로를 추적해보면 이 코드를 볼 수 있습니다.

클라이언트가 `Cmd_SubmitDeckData로` 자기 덱을 서버에 보내고, 서버는 `InGameSessionManager`에 보관된 그 데이터를 직접 `IdGenerator.ReturnInstanceIdDeck`로 변환합니다. 이 경로엔 `DeckSystem.CreateDeckState`가 호출되지 않습니다.

즉, `DeckSystem.CreateDeckState`는 실제 멀티플레이 흐름에서 우회됩니다. `DeckSystem`이 완전히 쓸모없는 건 아니지만, 현 구조에서 `CreateDeckState`는 사실상 사용처를 잃은 상태입니다.

이게 사실, 원래는 덱을 생성 -> 서버에 덱을 보내고 -> 서버에서 이 덱이 올바른 덱인지 검토 -> 게임 플레이가 순서였습니다. 그런데 앞서 `DeckRepository`를 설계하면서 *굳이?*가 되었습니다. 덱 생성이야 자체에서 판단 가능하고, 서버에서는 이미 '검증된 덱'을 받아와서 ID만 보고 `InstanceId` 일괄 발급해서 관리하면 되는데, 서버에 부담을 굳이 주어야 하나 싶어서 그냥 과감하게 사용하지 않았습니다. 결국 DeckSystem.CreateDeckState는 사용처가 줄었고, 지금은 전혀 사용하지 않고 있습니다. 추후 이 부분을 삭제하고 정리할 예정입니다!!
