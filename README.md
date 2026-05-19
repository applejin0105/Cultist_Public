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

<img width="1240" height="4045" alt="계층 스택" src="https://github.com/user-attachments/assets/846795ef-1916-4b80-99cb-d1ab06a20f84" />

코드의 정적 구조에 대해 먼저 설명하겠습니다. 앞서 설명한 Mirror와 Facepunch의 연장선입니다. 위로 갈수록 게임에 가깝고(고수준, Mirror), 아래로 갈수록 하드웨어에 가깝습니다.(저수준, Transport). 게임 코드는 Mirror만, Mirror는 `Transport` 추상 타입까지만, `FizzyFacepunch`는 `Facepunch.Steamworks`까지만 알고 그 아래는 알지 못합니다. 이러한 분리를 통해 `Transport` 자리에 무엇을 꽂느냐에 따라 통신 방식이 결정됩니다.

이 프로젝트에서는 이러한 `Transport`의 방식을 이용하여 FizzyFacepunch(Steam P2P)와 kcp2k(IP/로컬) 모두를 지원하고 있습니다.

2. 연결 수립 흐름 (게임 시작 전)
> 물리적으로 떨어진 PC들이 Steam에서 인증·로비를 거쳐, SteamID를 주소 삼아 P2P로 하나의 네트워크 세션에 묶습니다.

<img width="5892" height="5360" alt="2  Game Networking-2026-05-1 연결 수립 흐름9-112013" src="https://github.com/user-attachments/assets/44367a11-8483-49e9-8623-87cd37131887" />

각 PC는 독립적으로 부팅하여 `SteamClient.Init`으로 Steam에 인증하고 각자의 SteamID를 얻습니다. 이후 모든 연결의 *주소*는 **IP가 아닌 SteamID**로 설정됩니다. 호스트는 `SteamMatchmaking.CreateLobbyAsync(3)`로 로비를 만들고, `StartHost()`로 Mirror를 호스트모드(서버+플레이어 겸임)로 띄웁니다. `GameNetworkManager`의 `OnStartServer()`가 `NetworkGameController`를 스폰하고, 스폰된 NetworkGameController 자신의 OnStartServer()가 InitializeServerLogic()을 호출해 서버에만 존재할 게임의 실질적 두뇌(`GameState`·시스템·이펙트 등록)를 생성합니다. 클라이언트는 친구창에서 참가 시 `OnLobbyEntered`에서 `networkAddress`에 호스트 SteamID를 넣고 `StartClient()`를 호출하며, Transport가 이를 SteamID로 해석해 Steam P2P가 NAT을 뚫거나 Relay로 우회해 연결을 성사시킵니다. 연결되면 `OnServerAddPlayer()`가 플레이어 프리팹(`GamePlayer` + `LobbyPlayerState`)을 스폰하고, 이 객체가 양쪽 PC에 복제되며 연결된 PC들이 하나의 세션으로 구성됩니다.

현재 이러한 방식은, 호스트가 서버+플레이어 이므로 호스트 측에서 게임 데이터를 조작하거나 수정해버리면 이를 방지할 방법이 구축되어 있지 않으며, 호스트가 게임에서 나갈 경우 게임 세션이 강제 종료되는 한계가 존재합니다.

3. 로비 → 인게임 세션 셋업
> 모든 플레이가(호스트 제외) 준비가 끝나면 서버 주도로 인게임 씬으로 전환하고, 덱 제출·좌석 배정을 거쳐 게임 로직이 초기화됩니다.

<img width="4662" height="3770" alt="3  로비에서 인게임 세션 셋업" src="https://github.com/user-attachments/assets/ae4ba9b8-8a17-4075-818e-ff85f3d4fa3b" />

로비에서 각 플레이어는 `CmdSetReady(true)`로 준비 상태를 알리고 그 값은 `SyncVar`로 공유됩니다. 호스트를 제외한 전원이 준비 되었다면, 그리고 호스트가 Start 버튼을 눌렀다면 서버가 `ServerChangeScene("05_InGame")`으로 모든 클라이언트를 동시에 인게임 씬으로 전환합니다. **이때 씬 전환은 서버가 주도합니다!!!** 인게임 진입 후, 각 클라이언트들은 `CmdSubmitDeckData()`로 덱을 제출하고, 서버는 이때 `RegisterPlayer()`로 좌석 번호를 배정하며(`SyncVar`) 덱을 저장합니다. 서버는 `WaitForPlayersToStartGame` 코루틴으로 모든 덱이 도착할 때까지 기다린 뒤 `StartGameLogic()`을 실행해 `ServerGameState`를 초기화하고, 카드 상태(`SyncFullGameState`)와 UI 초기화(`RpcInitializeGameUI`)를 전원에 전파한 다음 루트 카드를 공개하고 첫 턴을 시작합니다.

4. 인게임 통신: 상태 동기화 vs 원격 호출
> 인게임 통신은 "변수를 감시하는 상태 동기화"와 "함수를 원격 실행하는 원격 호출(RPC)" 두 메커니즘으로 나뉩니다.

게임 진행 중의 모든 통신은 성격이 다른 두 메커니즘 중 하나에 속하여 진행됩니다. 편의상 메커니즘 A와 메커니즘 B로 부르겠습니다.

<img width="6472" height="4110" alt="4-1  통신 메커니즘" src="https://github.com/user-attachments/assets/60b5b966-8ab3-4217-b23f-bea5b86ec1f9" />

메커니즘 A; 상태 동기화: `SyncVar`·`SyncList`는 변수·리스트를 **감시**하는 장치입니다. 서버가 `CurrentRound`나 `SyncCards`의 값이 바뀌면 Mirror가 자동으로 dirty 처리해 전파합니다. 함수 호출이 아닌, 값의 변화 자체가 통신이라 **수동적이고, 언제나 서버->전체 단방향**으로 이루어집니다. 클라이언트에서는 hook과 Callback이 발화해 화면을 갱신합니다.

<img width="1693" height="2940" alt="4-2  게임 진행 순환" src="https://github.com/user-attachments/assets/f6eb5d9d-c741-4ca6-92ff-5d04fcbc9c13" />

메커니즘 B; 원격 호출(RPC): 원격 컴퓨터의 함수를 실제로 실행하는 능동적 통신이며 방향에 따라 셋으로 나뉩니다.
  B-1 | `Command`: 클라이언트 -> 서버, 행동 요청
  B-2 | `ClientRpc`: 서버 -> 전체, 일회성 통보
  B-3 | `TargetRpc`: 서버 -> 특정 1명, 개별 지정

플레이어의 입력은 B-1로 서버에 도달하고, 서버가 검증·처리해 `ServerGameState`를 바꾸면 그 결과는 메커니즘 A로 전원에 자동 반영됩니다. 만일 일회성이라면 B-2를, 특정 플레이어의 선택이라면 B-3으로 끼워넣습니다. 따라서 **모든 결정은 서버에서만 내려지고 클라이언트는 요청(B-1)과 표시(A)만 담당합니다.**

5. 비동기 입력 브릿지
> TaskCompletionSource로 RPC 왕복을 await 한 줄로 바꿔, 서버 이펙트가 플레이어 입력을 기다렸다가 정확히 그 지점에서 재개합니다.

<img width="7232" height="3760" alt="5  비동기 입력 브릿지" src="https://github.com/user-attachments/assets/f3c1d00f-7547-4266-9267-f136ba2daec4" />

서버의 카드 효과 실행기(`EffectRunner`)는 `async/await`로 한 줄씩 진행됩니다. 이는, 카드 효과 중 '파괴/희생할 카드를 선택'과 같이 플레이어가 *타겟*을 골라야 계속 진행되는 지점이 있기 때문입니다. 당연히 해당 플레이어는 다른 PC에 존재하고, 응답 시점을 알 수 없으므로 서버 로직은 **해당 플레이어의 입력이 올 때 까지 멈췄다가 응답이 오면 재개해야 합니다.**

이를 작동하게 만들어주는 것이 `TaskCompletionSource`로, 완료 시점을 직접 제어하는 Task입니다. 작동 방식은 다음과 같습니다.
  1. 서버 효과가 `await SelectTargetsAsync()`를 호출하면 `TaskCompletionSource`가 생성되어 플레이어별 상태에 저장됩니다.
  2. 해당 플레이어에게만 `TargetRpc` (B-3)가 발사됩니다.
  3. 서버는 이 시점에서 `await`으로 일시정지하고, B-3를 받은 플레이어는 선택 UI가 띄워지고, 이를 플레이어가 고르면 `Cmd_SubmitTargets()` (B-1)로 응답합니다.
  4. 서버는 이를 받아 `ReceiveTargetResponse()`에서 `tcs.TrySetResult()`를 호출하고, 이 순간 서버는 일시정지를 해제합니다.

카드 효과 코드를 짤 때, 개발자는 `await SelectTargetsAsync(...)` 한 줄만 쓰면 되고, 그 뒤의 네트워크 처리는 신경 쓸 필요가 없게 설계했습니다. 특히 이 부분에서 공을 많이 들였는데, 이유는 '확장성'과 '개발 편의성'에 있습니다. 기획자가 새로이 요구하는 새로운 효과들을 구현할 때, 네트워크에 엮여있으면 기능 하나를 구현하는데 시간이 많이 걸릴 것은 불보듯 뻔했습니다. 그래서, 이 부분에서는 적어도 카드 효과를 작성할때는 네트워크를 전혀 의식하지 않을 수 있게 구현했습니다.

6. 게임종료
> 승패 판정 결과를 SyncVar 두 개로 전원에 전파하고, 서버 주도로 로비에 복귀합니다.

<img width="3855" height="2750" alt="6  게임 종료" src="https://github.com/user-attachments/assets/16c81508-270b-48c5-be8a-93a21a94d10b" />

서버의 `GameRuleSystem`은 게임의 상태가 바뀔 때마다 승리조건을 판정합니다. 조건이 충족되면 `TriggerGameEnd(winnerSeat)`가 호출되고, 여기서 종료 전파를 `SyncVar` 두 개(`WinnerSeat`·`IsGameEnded`)로 처리합니다. 서버가 이 값을 설정하면 Mirror가 모든 플레이어에게 자동으로 전파하고, 각 클라이언트의 `OnGameEndedHook()`이 발화해 게임 종료 UI와 승/패 사운드를 재생합니다. 마지막으로 서버는 5초 후 `ServerChangeScene("03_Lobby")`로 전원을 로비로 되돌립니다.
