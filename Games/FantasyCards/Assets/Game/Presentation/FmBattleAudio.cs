using System.Collections.Generic;
using UnityEngine;

namespace FantasyCards.Presentation
{
    /// <summary>Original synthesized remaster SFX; never presented as recovered FM recordings.</summary>
    public static class FmBattleAudio
    {
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        public static void Play(AudioSource source, string cue)
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) return;
            if (!Clips.TryGetValue(cue, out var clip) || clip == null)
            { clip = Make(cue); Clips[cue] = clip; }
            source.PlayOneShot(clip, .85f);
        }
        static AudioClip Make(string cue)
        {
            const int rate = 44100;
            float duration = cue == "spell" || cue == "heal" ? .7f : cue == "burn" || cue == "defeat" ? .55f : .30f;
            var samples = new float[Mathf.CeilToInt(rate * duration)]; uint noiseState = 73191;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, p = t / duration;
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = (noiseState >> 8) / 8388607.5f - 1;
                float attack = Mathf.Clamp01(t * 180), decay = Mathf.Pow(1 - p, 2.1f);
                float value;
                switch (cue)
                {
                    case "coin": value = Mathf.Sin(t * 1450 * Mathf.PI * 2) * .42f + Mathf.Sin(t * 2319 * Mathf.PI * 2) * .25f + noise * .06f; break;
                    case "shield": value = Mathf.Sin(t * 920 * Mathf.PI * 2) * .3f + Mathf.Sin(t * 1480 * Mathf.PI * 2) * .3f + noise * .15f; break;
                    case "attack": value = noise * Mathf.Sin(p * Mathf.PI) * .6f + Mathf.Sin(t * (180 - 90 * p) * Mathf.PI * 2) * .18f; break;
                    case "hit": value = noise * .43f + Mathf.Sin(t * (130 - 65 * p) * Mathf.PI * 2) * .4f; break;
                    case "spell": value = noise * .12f + (Mathf.Sin(t * 392 * Mathf.PI * 2) + Mathf.Sin(t * 587.33f * Mathf.PI * 2) + Mathf.Sin(t * 784 * Mathf.PI * 2)) * .17f; break;
                    case "heal": value = (Mathf.Sin(t * 523.25f * Mathf.PI * 2) + Mathf.Sin(t * 659.25f * Mathf.PI * 2) + Mathf.Sin(t * 783.99f * Mathf.PI * 2)) * .17f; break;
                    case "burn": value = noise * .4f * (.5f + .5f * Mathf.Sin(t * 97)) + Mathf.Sin(t * 98 * Mathf.PI * 2) * .15f; break;
                    case "defeat": value = noise * .22f + Mathf.Sin(t * (140 - 100 * p) * Mathf.PI * 2) * .35f; break;
                    case "phase": value = Mathf.Sin(t * (p < .4f ? 392 : 523.25f) * Mathf.PI * 2) * .35f; break;
                    default: value = noise * .25f + Mathf.Sin(t * (400 - 250 * p) * Mathf.PI * 2) * .18f; break;
                }
                samples[i] = Mathf.Clamp(value * attack * decay * .62f, -.9f, .9f);
            }
            var clip = AudioClip.Create("FM Remaster SFX " + cue, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
    }
}
