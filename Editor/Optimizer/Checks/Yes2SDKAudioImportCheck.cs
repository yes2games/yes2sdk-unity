using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Reports audio clips whose import settings cost more download or more memory than the clip needs:
    /// a short effect kept in stereo, a long clip decompressed into memory, or a sample rate above what
    /// a browser will resample to anyway.
    /// </summary>
    public sealed class Yes2SDKAudioImportCheck : IYes2SDKOptimizationCheck
    {
        /// <summary>
        /// Clips at or below this length are treated as effects, and clips above it as music or voice.
        /// The split decides both whether stereo is worth its second channel and whether the clip should
        /// stream rather than sit decompressed in memory.
        /// </summary>
        private const float EffectLengthSeconds = 5f;

        /// <summary>A browser mixer resamples above this, so importing above it ships bytes nothing plays.</summary>
        private const uint MaxUsefulSampleRate = 44100;

        public string Id => "audio-import-settings";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Audio;

        public string Title => "Audio import settings";

        public string DocsAnchor => "audio";

        public bool CanFix => true;

        /// <summary>Import settings are not on the Undo stack.</summary>
        public bool FixIsUndoable => false;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            var findings = new List<Yes2SDKOptimizationFinding>();

            foreach (var pair in Yes2SDKImporterScan.AudioImporters())
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(pair.Key);
                if (clip == null) continue;

                foreach (var message in Problems(pair.Value, clip))
                {
                    findings.Add(new Yes2SDKOptimizationFinding
                    {
                        Severity = Yes2SDKFindingSeverity.Warning,
                        AssetPath = pair.Key,
                        Message = message,
                        Fixable = true,
                    });
                }
            }

            return findings;
        }

        // Import settings live in the asset's .meta file rather than in the scene, so Ctrl+Z will not
        // reverse this. To restore one clip by hand, select it and set Force To Mono, Load Type, and
        // Sample Rate Setting back in the Inspector. A clip that is genuinely stereo, such as a stereo
        // ambience effect, is the case to restore.
        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
            Yes2SDKImporterScan.Apply(findings, path =>
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (importer == null || clip == null) return false;

                var changed = false;
                var settings = importer.defaultSampleSettings;

                if (WantsMono(importer, clip))
                {
                    importer.forceToMono = true;
                    changed = true;
                }

                if (WantsStreaming(settings, clip))
                {
                    settings.loadType = AudioClipLoadType.Streaming;
                    changed = true;
                }

                if (WantsSampleRateCut(settings, clip))
                {
                    settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                    settings.sampleRateOverride = MaxUsefulSampleRate;
                    changed = true;
                }

                if (changed)
                {
                    importer.defaultSampleSettings = settings;
                }

                return changed;
            });
        }

        private static IEnumerable<string> Problems(AudioImporter importer, AudioClip clip)
        {
            var settings = importer.defaultSampleSettings;

            if (WantsMono(importer, clip))
            {
                yield return "A short effect imported in stereo. Force To Mono halves it, and an effect this "
                             + "short rarely reads as stereo to the player.";
            }

            if (WantsStreaming(settings, clip))
            {
                yield return "A long clip set to Decompress On Load, which holds the whole clip in memory "
                             + "uncompressed. Streaming plays it from the compressed data instead.";
            }

            if (WantsSampleRateCut(settings, clip))
            {
                yield return "Imported above 44100 Hz, which the browser resamples down anyway, so the extra "
                             + "samples are download and memory nothing plays.";
            }
        }

        // Only clips short enough to read as effects are candidates: forcing music or voice to mono is a
        // quality decision this check has no basis to make.
        private static bool WantsMono(AudioImporter importer, AudioClip clip)
        {
            return !importer.forceToMono
                   && clip.channels > 1
                   && clip.length <= EffectLengthSeconds;
        }

        private static bool WantsStreaming(AudioImporterSampleSettings settings, AudioClip clip)
        {
            return settings.loadType == AudioClipLoadType.DecompressOnLoad
                   && clip.length > EffectLengthSeconds;
        }

        private static bool WantsSampleRateCut(AudioImporterSampleSettings settings, AudioClip clip)
        {
            return settings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate
                   && clip.frequency > MaxUsefulSampleRate;
        }
    }
}
