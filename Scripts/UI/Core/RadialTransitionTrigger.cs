using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Core
{
    [RequireComponent(typeof(RectTransform))]
    public class RadialTransitionTrigger : MonoBehaviour, IPointerClickHandler
    {
        [Header("Manager Reference")]
        [SerializeField] private RadialTransitionManager manager;

        [Header("Button Index")]
        [SerializeField] private int elementIndex;

        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (manager != null) manager.OnElementClicked(elementIndex, _rectTransform);
        }
    }
}