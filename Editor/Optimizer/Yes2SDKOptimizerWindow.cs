using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Project optimization report. One tab per registered check: the strip across the top names every
    /// check with its finding count, and the body shows the selected check's findings and the actions it
    /// offers.
    /// </summary>
    public class Yes2SDKOptimizerWindow : EditorWindow
    {
        private const float TabHeight = 26f;

        // Room for a row of tabs plus the horizontal scrollbar underneath it. Too short and the strip
        // grows a vertical scrollbar of its own, which steals width from the tabs.
        private const float TabStripHeight = 46f;

        private const float AnalyzeWidth = 90f;

        private readonly Dictionary<string, IReadOnlyList<Yes2SDKOptimizationFinding>> _results =
            new Dictionary<string, IReadOnlyList<Yes2SDKOptimizationFinding>>();

        private Vector2 _tabScroll;
        private Vector2 _bodyScroll;
        private string _selectedId;
        private bool _hasRun;

        // Set by a click, run after the layout pass closes. Fixes write assets and open modal dialogs,
        // and doing either with the layout stack live can start an import or an unbalanced repaint.
        private Action _pending;

        // Whether the pending action changed something a scan would see. A fix or a mute did; opening a
        // download page did not, and a package install resolves asynchronously so an immediate re-scan
        // would report the old answer anyway.
        private bool _pendingRescan;

        // Built once on first use, not per repaint. EditorStyles is not readable before the first
        // OnGUI, so these cannot be field initializers.
        private GUIStyle _titleStyle;
        private GUIStyle _summaryStyle;
        private GUIStyle _tabStyle;

        /// <summary>Opens the Optimizer window, or focuses it when it is already open.</summary>
        [MenuItem("Yes2SDK/Optimizer", false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<Yes2SDKOptimizerWindow>("Yes2SDK Optimizer");
            window.minSize = new Vector2(720, 420);
            window.Show();
        }

        private void OnGUI()
        {
            BuildStyles();
            DrawTitleBar();
            DrawTabStrip();
            DrawBody();
            RunPending();
        }

        private void BuildStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _titleStyle.normal.textColor = new Color(0.85f, 0.87f, 0.92f);

            _summaryStyle = new GUIStyle(EditorStyles.miniLabel);
            _summaryStyle.normal.textColor = new Color(0.72f, 0.74f, 0.80f);

            _tabStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = TabHeight,
                padding = new RectOffset(10, 10, 4, 4),
            };
        }

        private void DrawTitleBar()
        {
            var bar = EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(30));
            EditorGUI.DrawRect(bar, new Color(0.18f, 0.20f, 0.24f));

            GUILayout.Label(SeverityIcon(WorstSeverity()), GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Yes2SDK Optimizer", _titleStyle, GUILayout.Height(20));

            GUILayout.FlexibleSpace();

            if (_hasRun)
            {
                GUILayout.Label(HeaderSummary(), _summaryStyle, GUILayout.Height(20));
            }

            if (GUILayout.Button(new GUIContent(" Docs", EditorGUIUtility.IconContent("_Help").image),
                    EditorStyles.miniButton, GUILayout.Width(70), GUILayout.Height(20)))
            {
                Application.OpenURL(Yes2SDKOptimizationRegistry.DocsUrl);
            }

            EditorGUILayout.EndHorizontal();
        }

        private string HeaderSummary()
        {
            var total = _results.Values.Sum(f => f.Count);
            var saving = _results.Values
                .SelectMany(f => f)
                .Where(f => f.EstimatedSaving.HasValue)
                .Sum(f => f.EstimatedSaving.Value);

            return saving > 0
                ? string.Format("{0} findings, about {1} recoverable", total, EditorUtility.FormatBytes(saving))
                : string.Format("{0} findings", total);
        }

        private void DrawTabStrip()
        {
            EditorGUILayout.BeginHorizontal();

            _tabScroll = EditorGUILayout.BeginScrollView(_tabScroll, false, false,
                GUILayout.Height(TabStripHeight));

            // No flexible space inside the scroll view. It would expand the content to the viewport
            // width, so the strip could never be wider than the window and would never scroll.
            EditorGUILayout.BeginHorizontal();

            foreach (var check in Yes2SDKOptimizationRegistry.All)
            {
                DrawTab(check);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(_hasRun ? "Re-analyze" : "Analyze",
                    GUILayout.Width(AnalyzeWidth), GUILayout.Height(TabHeight)))
            {
                Defer(() => { }, true);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTab(IYes2SDKOptimizationCheck check)
        {
            var muted = Yes2SDKOptimizationRegistry.IsMuted(check.Id);
            IReadOnlyList<Yes2SDKOptimizationFinding> findings;
            var ran = _results.TryGetValue(check.Id, out findings);

            var badge = muted ? "muted" : ran ? findings.Count.ToString() : "-";
            var label = string.Format("{0}   {1}", check.Title, badge);

            // One shared style, so the colour is assigned on every path rather than inherited from
            // whichever tab was drawn last.
            if (muted)
            {
                _tabStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
            }
            else if (ran && findings.Count > 0 && Worst(findings) != Yes2SDKFindingSeverity.Info)
            {
                _tabStyle.normal.textColor = new Color(0.98f, 0.78f, 0.32f);
            }
            else
            {
                _tabStyle.normal.textColor = EditorStyles.miniButton.normal.textColor;
            }

            // Content width, not stretched. A stretched tab in a horizontal group would divide the strip
            // evenly and truncate every longer title.
            var selected = check.Id == _selectedId;
            if (GUILayout.Toggle(selected, label, _tabStyle, GUILayout.ExpandWidth(false)) && !selected)
            {
                _selectedId = check.Id;
                _bodyScroll = Vector2.zero;
            }
        }

        private void DrawBody()
        {
            EditorGUILayout.BeginVertical();

            var check = Selected();
            if (check == null)
            {
                EditorGUILayout.HelpBox("No optimization checks are available in this project.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawBodyHeader(check);

            if (Yes2SDKOptimizationRegistry.IsMuted(check.Id))
            {
                EditorGUILayout.HelpBox("This check is muted. Unmute it and run Analyze to see its findings.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            IReadOnlyList<Yes2SDKOptimizationFinding> findings;
            if (!_results.TryGetValue(check.Id, out findings))
            {
                EditorGUILayout.HelpBox("Not analyzed yet. Run Analyze to scan this project.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (findings.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing to report.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll);
            foreach (var finding in findings)
            {
                DrawFinding(check, finding);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBodyHeader(IYes2SDKOptimizationCheck check)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(check.Title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(check.Category.ToString(), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent(" Why", EditorGUIUtility.IconContent("_Help").image),
                    GUILayout.Width(70), GUILayout.Height(22)))
            {
                Application.OpenURL(Yes2SDKOptimizationRegistry.DocsUrlFor(check));
            }

            var muted = Yes2SDKOptimizationRegistry.IsMuted(check.Id);
            if (GUILayout.Button(muted ? "Unmute" : "Mute", GUILayout.Width(80), GUILayout.Height(22)))
            {
                var id = check.Id;
                var next = !muted;
                Defer(() => Yes2SDKOptimizationRegistry.SetMuted(id, next), true);
            }

            GUILayout.FlexibleSpace();

            var fixable = FixableOf(check);
            if (fixable.Count > 0)
            {
                if (GUILayout.Button(string.Format("Fix {0} finding(s)", fixable.Count), GUILayout.Height(22)))
                {
                    Defer(() => ConfirmAndFix(check, fixable), true);
                }
            }
            else if (!check.CanFix)
            {
                GUILayout.Label("Report only. Fix this by hand, see the docs.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawFinding(IYes2SDKOptimizationCheck check, Yes2SDKOptimizationFinding finding)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label(SeverityIcon(finding.Severity), GUILayout.Width(20), GUILayout.Height(18));
            EditorGUILayout.LabelField(finding.Message, EditorStyles.wordWrappedMiniLabel);

            if (!string.IsNullOrEmpty(finding.AssetPath) &&
                GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(55)))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(finding.AssetPath);
            }

            if (!string.IsNullOrEmpty(finding.ActionLabel) && finding.Action != null &&
                GUILayout.Button(finding.ActionLabel, EditorStyles.miniButton, GUILayout.Width(70)))
            {
                var action = finding.Action;
                Defer(action, false);
            }

            if (check.CanFix && finding.Fixable &&
                GUILayout.Button("Fix", EditorStyles.miniButton, GUILayout.Width(45)))
            {
                var one = new List<Yes2SDKOptimizationFinding> { finding };

                // A one-row fix is self-disclosing: the row the user clicked names exactly what changes,
                // and Undo takes it back. A fix Undo cannot reverse still goes through the dialog.
                Defer(
                    check.FixIsUndoable
                        ? (Action)(() => check.Fix(one))
                        : () => ConfirmAndFix(check, one),
                    true);
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Queues a click for after the layout pass. <paramref name="rescan"/> is true when the action
        /// changes something a scan would see, so the results are refreshed once it has run.
        /// </summary>
        private void Defer(Action action, bool rescan)
        {
            _pending = action;
            _pendingRescan = rescan;
        }

        private void RunPending()
        {
            if (_pending == null)
            {
                return;
            }

            var pending = _pending;
            var rescan = _pendingRescan;
            _pending = null;

            EditorApplication.delayCall += () =>
            {
                pending();

                if (rescan)
                {
                    Analyze();
                }

                // The window can be closed between the click and this callback, which destroys the
                // native object while the closure still holds the managed one.
                if (this != null)
                {
                    Repaint();
                }
            };
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
                catch (Exception e)
                {
                    Debug.LogWarningFormat("Optimizer check '{0}' failed: {1}", check.Id, e.Message);
                    _results[check.Id] = new List<Yes2SDKOptimizationFinding>();
                }
            }

            _hasRun = true;

            if (Selected() == null)
            {
                var first = Yes2SDKOptimizationRegistry.All.FirstOrDefault();
                _selectedId = first == null ? null : first.Id;
            }
        }

        private IYes2SDKOptimizationCheck Selected()
        {
            var all = Yes2SDKOptimizationRegistry.All;
            return all.FirstOrDefault(c => c.Id == _selectedId) ?? all.FirstOrDefault();
        }

        private IReadOnlyList<Yes2SDKOptimizationFinding> FixableOf(IYes2SDKOptimizationCheck check)
        {
            IReadOnlyList<Yes2SDKOptimizationFinding> findings;
            if (!check.CanFix || !_results.TryGetValue(check.Id, out findings))
            {
                return new List<Yes2SDKOptimizationFinding>();
            }

            return findings.Where(f => f.Fixable).ToList();
        }

        private static void ConfirmAndFix(IYes2SDKOptimizationCheck check, IReadOnlyList<Yes2SDKOptimizationFinding> fixable)
        {
            var preview = string.Join("\n", fixable
                .Take(15)
                .Select(f => "- " + (string.IsNullOrEmpty(f.AssetPath) ? f.Message : f.AssetPath))
                .ToArray());

            if (fixable.Count > 15)
            {
                preview += string.Format("\n...and {0} more", fixable.Count - 15);
            }

            var reversal = check.FixIsUndoable
                ? "One Undo reverses the whole run."
                : "This cannot be reversed with Undo. Check the docs for how to restore the previous state.";

            var proceed = EditorUtility.DisplayDialog(
                check.Title,
                string.Format("This will change:\n\n{0}\n\n{1}", preview, reversal),
                "Apply",
                "Cancel");

            if (proceed)
            {
                check.Fix(fixable);
            }
        }

        private Yes2SDKFindingSeverity WorstSeverity()
        {
            var all = _results.Values.SelectMany(f => f).ToList();
            return all.Count == 0 ? Yes2SDKFindingSeverity.Info : Worst(all);
        }

        private static Yes2SDKFindingSeverity Worst(IEnumerable<Yes2SDKOptimizationFinding> findings)
        {
            var worst = Yes2SDKFindingSeverity.Info;
            foreach (var finding in findings)
            {
                if (finding.Severity > worst)
                {
                    worst = finding.Severity;
                }
            }

            return worst;
        }

        private static GUIContent SeverityIcon(Yes2SDKFindingSeverity severity)
        {
            switch (severity)
            {
                case Yes2SDKFindingSeverity.Critical:
                    return EditorGUIUtility.IconContent("console.erroricon.sml");
                case Yes2SDKFindingSeverity.Warning:
                    return EditorGUIUtility.IconContent("console.warnicon.sml");
                default:
                    return EditorGUIUtility.IconContent("console.infoicon.sml");
            }
        }
    }
}
