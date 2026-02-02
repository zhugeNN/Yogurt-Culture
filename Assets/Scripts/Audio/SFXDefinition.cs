// SFXDefinition.cs
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "NewSFXSet", menuName = "Audio/SFX Definition")]
public class SFXDefinition : ScriptableObject
{
    [Serializable]
    public class SFXClip
    {
        public string key; // 音效标识，如"PlayerJump"
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 2f)] public float pitch = 1f;
        public bool loop = false;
    }

    public SFXClip[] clips;
}