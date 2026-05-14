using System.Collections;
using Core.Data.Enums;
using UnityEngine;

namespace Components.Effects.UI.Core
{
    // 나는 솔직히 존나 게으른 개발자다.
    // 그래서 이펙트도 매니저 하나가 관리해줬으면 좋겠다.
    // 이펙트 스크립트가 중구난방한건 하나하나 관리하기 귀찮으니깐.
    // 그래서 난, 인간을 그만두겠다 죠죠!!!!!!

    // 그러면 버튼의 경우 현재 색이 변하는 기능도 들어가있다.
    // 내 짧은 식견으로 보자면 이것도 Effect에 보내야 하지 싶다.
    // 그래서 다시금 분류해보기로 했다.

    /* State vs Event
     * State는 객체가 특정 시점에 가지고 있는 데이터나 모습이다.
     * 지속성을 가지고 있으며, 외부에서 개입 혹은 스스로 바꾸기 전까지 그 상태(State)를 유지한다.
     * Event는 특정 시점에 순간적으로 발생한 일이다.
     * 휘발성으로, 발생한 그 순간에 누군가가 듣지 않으면(Event Listen) 그대로 사라진다.
     *
     * OOP의 관점에서의 역할 분담
     * SRP를 따져보면, State와 Event를 구분할 수 있다.
     *
     * 일단 State는 캡슐화의 대상이다.
     * 주체: 객체 자기 자신이 책임진다.
     * 현재까지 제작한 버튼의 구조는 버튼의 마우스가 올라갔을 때 커지고, 색이 변한다.
     * 즉, 버튼 객체 스스로의 상태 변화다. 거기에 외부 시스템에서는 버튼이 지금 커져 있는지 작아져 있는지 상관없다.
     * 버튼이 알아서 자신의 State를 관리해야한다.
     *
     * Event는 Communication의 수단이다.
     * 주체: 다른 객체(System)에게 무언가를 알릴 때 사용한다. (옵저버 버튼)
     * 가령 비밀번호가 틀렸다는 것을 알리거나, 어떤 씬으로 이동하라고 알리는 등의, 흔한 이벤트 형식을 구현해야한다.
     *
     * 그럼 앞으로 생성할 많은 스크립트에서 이런 State와 Event를 구분하기 위해서, 나는 어떻게 생각해야 할까 고민해봤다.
     *
     * 1. 이 변화가 이 객체 혼자만의 일인가 아니면 다른 시스템에서도 알아야 하는 일인가?
     * 만일 혼자만의 일이라면 State (버튼 색 변경, UI 패널 페이드인/아웃 등) -> 객체 내부의 스크립트에서 처리
     * 다른 시스템도 알아야 한다면 -> Event 처리
     *
     * 2. 게임이 멈췄을 때 (Time.timeScale = 0), 이것의 값을 저장하고 있어야 하는가?
     * 저장해야한다 -> State (플레이어 HP, 인벤토리 아이템, 버튼의 활성화 여부 등)
     * 저장할 필요 없다 -> Event (클릭했다는 사실 자체, 데미지를 입은 순간 등)
     *
     * 그럼 처음 내가 생각한 EffectManger는? 쓸모없다. 개별 상태를 관리하는거지 이걸 하나하나 처리하고 알리면 무슨 소용인가.
     * State 변경은 객체 개별이 관리하는 것이지 다른곳에서는 필요 없다.
     * Event는 결국 '소리지르기'다. '여기 나 있소' 소리지르면 그걸 듣고 관리하는 이벤트 수신기만 따로 있으면 된다.
     * 그럼 나는 여기에 EventManger를 추가하고 싶다. 중앙 집중식으로 디버깅도 가능하고, 이벤트 관리를 수월하게 하고싶다.
     * 설계 방향은 정해졌다. 각 신마다 EventManger를 두고, enum으로 이벤트를 구분하고 수신한다.
     * 이걸 디버깅 가능하게 구성한다.
     */


    // 템플릿 메서드 패턴 (Template Method Pattern):
    //
    // 자식 클래스(UIShakeEffect)는 이제 startDelay가 몇 초인지, 끝났을 때 누구한테 알려줘야 하는지(onComplete) 전혀 신경 쓸 필요가 없습니다.
    //
    //     자식은 오로지 ExecuteEffect() 안에서 "어떻게 흔들 것인가"만 집중하면 됩니다. 나머지는 부모가 알아서 통제합니다. (완벽한 역할 분담)
    //
    // UnityEvent의 활용:
    //
    // C#의 기본 Action을 쓰지 않고 UnityEvent onComplete를 쓴 이유는, 유니티 인스펙터 창에서 드래그 앤 드롭으로 "이거 끝나면 저거 실행해"를 연결할 수 있게 만들기 위해서입니다. 기획자나 디자이너가 코딩 없이 시퀀스를 짤 수 있는 엄청난 무기가 됩니다.
    //
    //     yield return StartCoroutine(...)의 마법:
    //
    // 자식의 연출 코루틴이 0.5초짜리라면, 부모의 PlayRoutine도 정확히 0.5초 동안 그 자리에 멈춰서 기다려줍니다. 덕분에 연출이 "진짜로 다 끝난 직후"에만 onComplete 바통을 넘겨줄 수 있습니다.

    // 기존 코드
    // public abstract class UIEffect : MonoBehaviour
    // {
    //     [FormerlySerializedAs("targetTransform")]
    //     [Header("Common Settings")]
    //     [Tooltip("이펙트가 적용될 타겟.")]
    //     [SerializeField] protected RectTransform targetRect;
    //
    //     [Tooltip("실행 명령 후, 연출 시작까지 대기 시간")]
    //     [SerializeField] protected float startDelay = 0f;
    //
    //     [Header("Events")] public UnityEvent onComplete;
    //
    //     private WaitForSeconds _delayWfs;
    //
    //     // 자식 클래스에서 사용 가능하게 protected 클래스
    //     // override 허용하기 위해 virtual
    //     protected virtual void Awake()
    //     {
    //         // 인스펙터에서 타겟 미설정시 자기 자신을 기본 타겟으로 설정
    //         if (targetRect == null)
    //             targetRect = GetComponent<RectTransform>();
    //
    //         if (startDelay > 0f)
    //             _delayWfs = new WaitForSeconds(startDelay);
    //     }
    //
    //     // 추상 메서드. 구현부 없음. 모든 자식 클래스가 무조건 가지고 있어야 하므로.
    //     // 자기 자신만의 방식으로 구현해야 하므로.
    //     public void Play()
    //     {
    //         StopAllCoroutines();
    //         StartCoroutine(PlayRoutine());
    //     }
    //
    //     private IEnumerator PlayRoutine()
    //     {
    //         if (startDelay > 0f)
    //         {
    //             // 함수 실행 때 마다, 메모리(Heap)에 WaitForSeconds라는 객체를 새로 찍어냄.
    //             // 대기 시간이 끝나면 GC가 처리때까지 메모리 파먹음.
    //             // 그럼 캐싱을 활용해보자앗
    //             // 어차피 startDelay 값은 인스펙터에서 정해지면 실행중에 바뀔 일이 거의 없음.
    //             // 그럼 Awake에서 딱 한번만 만들어서 저장, 계속 재활용하면 됨
    //             // Zero-Allocation 코딩!!!!
    //             // yield return new WaitForSeconds(startDelay);
    //             yield return _delayWfs;
    //         }
    //
    //         yield return StartCoroutine(ExecuteEffect());
    //
    //         onComplete?.Invoke();
    //     }
    //
    //     protected abstract IEnumerator ExecuteEffect();
    //
    //     // 가상 메서드. 구현부가 존재.
    //     // 자식들이 멈추는 기능이 필요할 때만 선택적으로 덮어쓸 수 있도록 기본 틀만 제공.
    //     // 당연히 필요없을 수 있으므로 virtual로 선언함 우효
    //     public virtual void Stop()
    //     {
    //     }
    // }

    // 이 코드의 구조에서 다음과 같이 바꾸었다.
    // 이유는 간단한다
    // 외부에서 프로퍼티로 받아와서 효과를 사용해야 하는 경우, 인스펙터에서도 쉽게 사용하려면 어떻게 해야할까.
    // 간단하게 그냥 적당한 함수 하나 구현하면 되겠지만, 휴면 에러로 넣지 못하는 경우를 방지하고 싶었다.
    // 그러면 UIEffect에 abstract로 할 수 있겠지만, 그럴경우 파라미터가 문제다 (반드시 일치해야하므로)
    // 그래서 한번, 제네릭 부모 클래스를 적용해서 기존 코드를 갈아엎어보았다.

    /* UIEffect는 모든 이펙트의 공통 분모(타겟의 RectTransform, 전체 시간, 애니메이션 타입)을 관리.
     * Awake에서 자기 자신을 자동으로 찾아주게 설계
     *
     * UIConfigurableEffect<T>는 외부 매니저가 이펙트를 덮어씌울 수 있도록 제네릭 설정(Config) 통로를 설계
     * SetProperty를 통해 외부 데이터를 자유로운 형식으로 주입
     * 각 자식들은
     * 1. Config를 소비하고, null로 비워 GC 처리
     * 2. 도중에 Stop이 불렸을 때 최종 값으로 변경, 목적지 캐싱
     * 3. 첫 프레임 스냅을 통해 시작점을 강제 조정하여 팝핑 버그 막기
     * 4. 새 트윈을 킬때는, 기존 트윈을 반드시 제거. GC
     */

    public abstract class UIEffect : MonoBehaviour
    {
        [Header("Base Settings")]
        [SerializeField] protected float defaultDuration = 1.0f;

        [Header("Method")]
        [SerializeField] protected AnimType animType;

        [Header("Target RectTransform")]
        [Tooltip("None 설정 시, 자기 자신의 Target Rect로 설정")]
        [SerializeField] protected RectTransform targetRectTransform;

        protected Coroutine EffectCoroutine;

        // 코드로 덮어씌울 일회성 시간
        protected float? OverrideDuration;

        protected float ActualDuration => OverrideDuration ?? defaultDuration;

        public float Duration => ActualDuration;

        protected virtual void Awake()
        {
            if (targetRectTransform == null)
            {
                targetRectTransform = GetComponent<RectTransform>();
                if (targetRectTransform == null)
                    Debug.LogError($"[{gameObject.name}] {GetType().Name}에 타겟 RectTransform이 존재하지 않음.", gameObject);
            }
        }

        public virtual void Play()
        {
            if (!gameObject.activeInHierarchy) return;

            if (EffectCoroutine != null) Stop(false);

            EffectCoroutine = StartCoroutine(ExecuteEffect());
        }

        public virtual IEnumerator PlayWaitable()
        {
            if (!gameObject.activeInHierarchy) yield break;

            if (EffectCoroutine != null) Stop(false);

            EffectCoroutine = StartCoroutine(ExecuteEffect());
            yield return EffectCoroutine;
        }

        public virtual void Stop(bool snapToEnd = true)
        {
            if (EffectCoroutine != null)
            {
                StopCoroutine(EffectCoroutine);
                EffectCoroutine = null;
            }
        }

        public virtual void PlaySound(UISoundType soundType, float volume = 0.6f)
        {
        }

        public virtual void StopSound()
        {
        }

        public void SetDuration(float newDuration)
        {
            OverrideDuration = newDuration;
        }

        protected abstract IEnumerator ExecuteEffect();

        protected enum AnimType
        {
            Normal,
            DoTween
        }
    }
}