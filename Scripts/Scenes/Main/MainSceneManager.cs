using System;
using System.Collections.Generic;
using Core.Data.Enums;
using UnityEngine;
using Core.Interfaces;
using Core.Events;
using Core.Managers;

namespace Scenes.Main
{
    public class MainSceneManager : MonoBehaviour
    {
        [Header("Scene Setup")]
        [SerializeField] private string currentScene = "00_Main";
        [SerializeField] private List<BGMSoundType> sceneBGM;

        [Header("Dependencies")]
        [Tooltip("이 씬의 UI를 담당하는 컨트롤러")]
        [SerializeField] private MainSceneUIController uiController;

        private void Awake()
        {
            EnterSceneLoad();
        }

        public void EnterSceneLoad()
        {
            foreach (var bgm in sceneBGM)
                SoundManager.Instance.AddBgmToQueue(bgm);

            SoundManager.Instance.PlayQueue(true);

            Debug.Log($"[{currentScene}] EnterSceneLoad: 씬 입장 완료");
        }
    }
}