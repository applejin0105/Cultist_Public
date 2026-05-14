using System.Collections.Generic;
using Components.Common.Buttons.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Scenes.Tutorial
{
    public class TutorialController : MonoBehaviour
    {
        [SerializeField] private Image tutorialImage;
        [SerializeField] private List<Sprite> tutorialSprites;
        [SerializeField] private CompoundButton compoundButton;

        private int _index = 0;
        private int _maxSpriteCount;

        private void Start()
        {
            _maxSpriteCount = tutorialSprites.Count;
        }

        private void OnEnable()
        {
            compoundButton.onLeftClickEvent.AddListener(OnClick);
        }

        private void OnDisable()
        {
            compoundButton.onLeftClickEvent.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            if (_index >= _maxSpriteCount - 1) SceneManager.LoadScene("01_Main");
            else
            {
                tutorialImage.sprite = tutorialSprites[++_index];
            }
        }
    }
}