using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Discovers every optimization check in the editor assembly and holds the per-project
    /// mute preferences. Adding a check is adding one class; nothing registers here by hand.
    /// </summary>
    public static class Yes2SDKOptimizationRegistry
    {
        private const string DocsPageUrl = "https://developer.yes2games.com/docs/unity-optimization";
        private const string MutePrefixPref = "Yes2SDK.Optimizer.Mute.";

        private static List<IYes2SDKOptimizationCheck> _all;

        /// <summary>Every discovered check, ordered by category then title so the window is stable.</summary>
        public static IReadOnlyList<IYes2SDKOptimizationCheck> All
        {
            get
            {
                if (_all == null)
                {
                    var discovered = new List<IYes2SDKOptimizationCheck>();

                    foreach (var type in TypeCache.GetTypesDerivedFrom<IYes2SDKOptimizationCheck>())
                    {
                        if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
                        {
                            continue;
                        }

                        try
                        {
                            discovered.Add((IYes2SDKOptimizationCheck)Activator.CreateInstance(type));
                        }
                        catch (Exception e)
                        {
                            // Constructing one check must not throw out of this property: that would
                            // leave the cache null and make every working check unreachable too.
                            Debug.LogWarning("Yes2SDK Optimizer skipped a check that threw while being constructed: "
                                + type.FullName + ". " + e.Message);
                        }
                    }

                    _all = discovered
                        .OrderBy(c => c.Category)
                        .ThenBy(c => c.Title)
                        .ToList();
                }

                return _all;
            }
        }

        /// <summary>Drops the cached check list so a domain reload picks up newly added checks.</summary>
        public static void Invalidate() => _all = null;

        /// <summary>
        /// True when the user has muted this check. The preference is stored per Editor
        /// installation, the same store the build settings use, so it is shared by every
        /// project opened with this Editor rather than travelling with one project.
        /// </summary>
        public static bool IsMuted(string checkId) => EditorPrefs.GetBool(MutePrefixPref + checkId, false);

        /// <summary>Mutes or unmutes a check for this Editor installation.</summary>
        public static void SetMuted(string checkId, bool muted) => EditorPrefs.SetBool(MutePrefixPref + checkId, muted);

        /// <summary>Deep link to the section of the optimization docs that explains this check.</summary>
        public static string DocsUrlFor(IYes2SDKOptimizationCheck check) => DocsPageUrl + "#" + check.DocsAnchor;

        /// <summary>The optimization docs page itself, with no anchor.</summary>
        public static string DocsUrl => DocsPageUrl;
    }
}
