using System;
using UnityEngine;
namespace RogueShooter.Audio
{
    [CreateAssetMenu(menuName="JianHai/Audio Bank")]
    public sealed class JianHaiAudioBank : ScriptableObject
    {
        [Serializable] public sealed class Cue { public string id,bus,spatial; public AudioClip[] clips; public int priority,busLimit; public float gainDb,stopFade; public bool loop; }
        [Serializable] public sealed class Stem { public string id; public AudioClip clip; }
        [Serializable] public sealed class Layer { public string id; public float weight; }
        [Serializable] public sealed class Preset { public string id; public Layer[] layers; }
        public string version; public Cue[] cues; public Stem[] stems; public Preset[] presets;
    }
}
