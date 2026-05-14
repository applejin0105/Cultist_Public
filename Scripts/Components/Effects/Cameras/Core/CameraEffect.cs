using System.Collections;
using UnityEngine;

namespace Components.Effects.Cameras.Core
{
    public abstract class CameraEffect : MonoBehaviour
    {
        protected enum AnimType
        {
            Normal,
            DoTween
        }

        [Header("Base Settings")]
        [SerializeField] protected float defaultDuration = 1.0f;

        [Header("Method")]
        [SerializeField] protected AnimType animType;

        [Header("Target Camera")]
        [Tooltip("None 설정 시, 메인 카메라 자동 탐색")]
        [SerializeField] protected Camera targetCamera;
        protected Transform TargetTransform; // 캐싱용

        protected float? OverrideDuration;
        protected Coroutine EffectCoroutine;

        protected virtual void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    Debug.LogError($"[{gameObject.name}] {GetType().Name}에 타겟 Camera가 존재하지 않음.", gameObject);
                    return;
                }
            }

            TargetTransform = targetCamera.transform;
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

        public void SetDuration(float newDuration)
        {
            OverrideDuration = newDuration;
        }

        protected abstract IEnumerator ExecuteEffect();

        protected float ActualDuration => OverrideDuration ?? defaultDuration;
        public float Duration => ActualDuration;
    }
}