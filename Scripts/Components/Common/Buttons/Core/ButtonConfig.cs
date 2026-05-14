#nullable enable
using System;
using System.Collections.Generic;
using Core.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Components.Common.Buttons.Core
{
    [Serializable]
    public struct UIElementStateData
    {
        [Header("Color Settings")]
        public bool useColor;
        public Color color;

        [Header("Graphic Settings (Text/Image)")]
        public bool useSprite;
        public Sprite? sprite;
        public bool useText;
        public string? text;

        [Header("Transform Settings")]
        public bool useScale;
        public Vector3 scale;

        [Header("Rotation Settings")]
        public bool useRotation;
        public Vector3 rotation;
        [Tooltip("체크 시 회전을 애니메이션 시간과 무관하게 무한 반복. (회전 속도는 Duration 기준)")]
        public bool isRotationLoop;
        [Tooltip("0이면 Global Duration을 따르고, 값을 넣으면 개별 회전 속도.")]
        public float rotationDuration;

        [Header("Material Glow Settings")]
        public bool useGlow;
        public float glowPower;
        public Color glowColor;

        [Header("Fake Bloom Settings")]
        public bool useBloom;
        public float bloomAlpha;
        public float bloomScaleX;
        public float bloomScaleY;
        public float bloomScaleZ;
        public Color bloomColor;
    }

    [Serializable]
    public struct GraphicConfig
    {
        [Header("Target Elements")]
        [Tooltip("색상, 텍스트, 이미지를 변경할 UI 요소를 연결하세요.")]
        public Graphic? target;

        [Tooltip("Graphic 컴포넌트가 없는 빈 객체(부모 등)의 스케일만 조절하고 싶을 때 사용하세요.")]
        public RectTransform? targetRect;

        [Tooltip("Material Glow 효과를 적용할 타겟 매테리얼")]
        public Material? targetMaterial;

        [Tooltip("Fake Bloom 효과를 적용할 UI Image")]
        public Image? bloomImage;

        [Header("Always State")]
        [Tooltip("체크 시 하위 상태(Hover, Pressed)를 무시하고 항상 이 설정을 적용합니다.")]
        public bool useAlways;

        [ShowIf("useAlways == true")]
        public UIElementStateData alwaysState;

        [Header("Dynamic States")]
        [ShowIf("useAlways == false")]
        public UIElementStateData hoverState;
        [ShowIf("useAlways == false")]
        public UIElementStateData leftPressedState;
        [ShowIf("useAlways == false")]
        public UIElementStateData rightPressedState;
        [ShowIf("useAlways == false")]
        public UIElementStateData disabledState;

        [HideInInspector]
        public UIElementStateData baseState;

        [Header("Individual Duration")]
        public bool useSeparateDuration;
        [ShowIf("useSeparateDuration == true")]
        public float customDuration;
    }

    [Serializable]
    public struct ButtonConfig
    {
        [Header("Global Settings")]
        public float animDuration;

        [Header("Graphic Elements")]
        public List<GraphicConfig> graphicSettings;

        public ButtonConfig Clone()
        {
            var clone = new ButtonConfig();
            clone.animDuration = animDuration;

            if (graphicSettings != null)
                clone.graphicSettings = new List<GraphicConfig>(graphicSettings);

            return clone;
        }
    }
}