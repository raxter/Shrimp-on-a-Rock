using System.Collections.Generic;
using UnityEngine;

public class PopSoundController : MonoBehaviour
{
    public AudioSource audioSource;
    public List<AudioClip> pops;
    public float minPitch = 0.9f;
    public float maxPitch = 1.1f;

    public void Play()
    {
        if (audioSource == null || pops == null || pops.Count == 0) return;
        AudioClip clip = pops[Random.Range(0, pops.Count)];
        if (clip == null) return;
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(clip);
    }
}
