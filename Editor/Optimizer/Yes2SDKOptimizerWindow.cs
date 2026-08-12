using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Project optimization report. Runs every registered check, groups the findings by check,
    /// and offers the fix where the check provides one.
    /// </summary>
    public class Yes2SDKOptimizerWindow : EditorWindow
    {
        private readonly Dictionary<string, IReadOnlyList<Yes2SDKOptimizationFinding>> _results =
            new Dictionary<string, IReadOnlyList<Yes2SDKOptimizationFinding>>();

        private readonly HashSet<string> _expanded = new HashSet<string>();
        private Vector2 _scroll;
        private bool _hasRun;

        [MenuItem("Yes2SDK/Optimizer", false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<Yes2SDKOptimizerWindow>("Yes2SDK Optimizer");
            window.minSize = new Vector2(460, 400);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();

            if (!_hasRun)
            {
                EditorGUILayout.HelpBox("Run Analyze to scan this project.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var check in Yes2SDKOptimizationRegistry.All)
            {
                DrawCheck(check);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Analyze", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                Analyze();
            }

            GUILayout.FlexibleSpace();

            if (_hasRun)
            {
                var total = _results.Values.Sum(f => f.Count);
                var saving = _results.Values
                    .SelectMany(f => f)
                    .Where(f => f.EstimatedSaving.HasValue)
                    .Sum(f => f.EstimatedSaving.Value);

                var label = saving > 0
                    ? string.Format("{0} findings, about {1} recoverable", total, EditorUtility.FormatBytes(saving))
                    : string.Format("{0} findings", total);

                GUILayout.Label(label, EditorStyles.miniLabel);
            }

            if (GUILayout.Button("Docs", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                Application.OpenURL(Yes2SDKOptimizationRegistry.DocsUrl);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void Analyze()
        {
            Yes2SDKOptimizationRegistry.Invalidate();
            _results.Clear();

            foreach (var check in Yes2SDKOptimizationRegistry.All)
            {
                if (Yes2SDKOptimizationRegistry.IsMuted(check.Id))
                {
                    continue;
                }

                try
                {
                    _results[check.Id] = check.Analyze();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarningFormat("Optimizer check '{0}' failed: {1}", check.Id, e.Message);
                    _results[check.Id] = new List<Yes2SDKOptimizationFinding>();
                }
            }

            _hasRun = true;
        }

        private void DrawCheck(IYes2SDKOptimizationCheck check)
        {
            var muted = Yes2SDKOptimizationRegistry.IsMuted(check.Id);
            IReadOnlyList<Yes2SDKOptimizationFinding> findings;
            if (!_results.TryGetValue(check.Id, out findings))
            {
                findings = new List<Yes2SDKOptimizationFinding>();
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var open = _expanded.Contains(check.Id);
            var header = string.Format("{0}  ({1})", check.Title, muted ? "muted" : findings.Count.ToString());
            var nowOpen = EditorGUILayout.Foldout(open, header, true);
            if (nowOpen && !open)
            {
                _expanded.Add(check.Id);
            }
            else if (!nowOpen && open)
            {
                _expanded.Remove(check.Id);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Why", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                Application.OpenURL(Yes2SDKOptimizationRegistry.DocsUrlFor(check));
            }

            if (GUILayout.Button(muted ? "Unmute" : "Mute", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                Yes2SDKOptimizationRegistry.SetMuted(check.Id, !muted);
            }

            EditorGUILayout.EndHorizontal();

            if (nowOpen && !muted)
            {
                DrawFindings(check, findings);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFindings(IYes2SDKOptimizationCheck check, IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
            if (findings.Count == 0)
            {
                EditorGUILayout.LabelField("Nothing to report.", EditorStyles.miniLabel);
                return;
            }

            foreach (var finding in findings)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(SeverityIcon(finding.Severity), GUILayout.Width(18));
                EditorGUILayout.LabelField(finding.Message, EditorStyles.wordWrappedMiniLabel);

                if (!string.IsNullOrEmpty(finding.AssetPath) &&
                    GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(55)))
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(finding.AssetPath);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!check.CanFix)
            {
                EditorGUILayout.LabelField("Report only. Fix this by hand, see the docs.", EditorStyles.miniLabel);
                return;
            }

            var fixable = findings.Where(f => f.Fixable).ToList();
            if (fixable.Count == 0)
            {
                return;
            }

            if (GUILayout.Button(string.Format("Fix {0} finding(s)", fixable.Count)))
            {
                ConfirmAndFix(check, fixable);
            }
        }

        private void ConfirmAndFix(IYes2SDKOptimizationCheck check, IReadOnlyList<Yes2SDKOptimizationFinding> fixable)
        {
            var preview = string.Join("\n", fixable
                .Take(15)
                .Select(f => "- " + (string.IsNullOrEmpty(f.AssetPath) ? f.Message : f.AssetPath))
                .ToArray());

            if (fixable.Count > 15)
            {
                preview += string.Format("\n...and {0} more", fixable.Count - 15);
            }

            var proceed = EditorUtility.DisplayDialog(
                check.Title,
                string.Format("This will change:\n\n{0}\n\nOne Undo reverses the whole run.", preview),
                "Apply",
                "Cancel");

            if (!proceed)
            {
                return;
            }

            check.Fix(fixable);
            Analyze();
        }

        private static string SeverityIcon(Yes2SDKFindingSeverity severity)
        {
            switch (severity)
            {
                case Yes2SDKFindingSeverity.Critical:
                    return "!!";
                case Yes2SDKFindingSeverity.Warning:
                    return "!";
                default:
                    return "i";
            }
        }
    }
}
