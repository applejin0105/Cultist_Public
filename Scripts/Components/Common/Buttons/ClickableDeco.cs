using System;
using System.Collections;
using System.Collections.Generic;
using Components.Effects.UI.Core;
using Core.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Components.Common.Buttons
{
    public enum OrderType
    {
        SpriteToEffect,
        EffectToSprite,
        Simultaneous
    }

    [Serializable]
    public struct ClickableDecoConfig
    {
        public bool useSprite;
        [ShowIf("UseSprite == true")]
        public bool useRandomSprite;

        public bool useEffect;
        [ShowIf("UseEffect == true")]
        public bool useRandomEffect;

        [ShowIf("UseSprite == true && UseEffect == true")]
        public OrderType order;
    }

    public class ClickableDeco : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private ClickableDecoConfig decoConfig;

        [SerializeField] private List<UIEffect> effects;

        [SerializeField] private SerializableDictionary<Sprite, float> sprite;

        public void OnPointerClick(PointerEventData eventData)
        {
            PlayEffectType(decoConfig.useSprite, decoConfig.useEffect);
        }


        private IEnumerator ChangeSprite(bool isRandom = false)
        {
            if (isRandom)
            {
                // 기존에 있는 index가 아닌 것 중에, 랜덤으로 1개 뽑기
                // currentIndex만 피해서 랜덤 돌리면 됨.
            }

            yield return new WaitForEndOfFrame();
        }

        private IEnumerator PlayEffect(bool isRandom = false)
        {
            if (isRandom)
            {
            }

            yield return new WaitForEndOfFrame();
        }

        private IEnumerator PlaySimultaneous(bool isSpriteRandom, bool isEffectRandom = false)
        {
            switch (isSpriteRandom)
            {
                case true when isEffectRandom:
                    break;
                case true:
                    break;
                default:
                {
                    if (isEffectRandom)
                    {
                    }

                    break;
                }
            }

            yield return new WaitForEndOfFrame();
        }

        private void PlayEffectType(bool useSprite, bool useEffect)
        {
            switch (useSprite)
            {
                case true when useEffect:
                    StartCoroutine(PlaySimultaneous(decoConfig.useRandomSprite, decoConfig.useRandomEffect));
                    break;
                case true:
                    StartCoroutine(ChangeSprite(decoConfig.useRandomSprite));
                    break;
                default:
                {
                    if (useEffect)
                        StartCoroutine(PlayEffect(decoConfig.useRandomEffect));
                    break;
                }
            }
        }
    }
}