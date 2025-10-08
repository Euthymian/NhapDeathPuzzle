using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private AudioSource musicAudioSource;

    private void Start()
    {
        musicAudioSource = GetComponent<AudioSource>();
    }

    public void StopMusic()
    {
        musicAudioSource.Stop();
    }

    public void PlayMusic()
    {
        musicAudioSource.Play();
    }
}
