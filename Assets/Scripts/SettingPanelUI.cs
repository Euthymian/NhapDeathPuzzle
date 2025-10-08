using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;

public class SettingPanelUI : MonoBehaviour
{
    [SerializeField] private Button closeButton;
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle soundToggle;

    private void Start()
    {
        closeButton.onClick.AddListener(() =>
        {
            gameObject.SetActive(false);
        });

        musicToggle.onValueChanged.AddListener((isOn) =>
        {
            ToggleMusic(isOn);
        });

        if(musicToggle.isOn) SoundManager.Instance.PlayMusic();
        else SoundManager.Instance.StopMusic();
    }

    private void ToggleMusic(bool isOn)
    {
        if(isOn) SoundManager.Instance.PlayMusic();
        else SoundManager.Instance.StopMusic();
    }
}
