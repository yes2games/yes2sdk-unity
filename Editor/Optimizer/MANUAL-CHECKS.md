# Optimizer manual checks

Run once in a scratch Unity 2021.3 project before tagging a release. This package has no
openable project of its own, so these cannot be automated here.

- [ ] The package imports with no compile errors.
- [ ] `Yes2SDK > Optimizer` appears directly under `Yes2SDK > Build Window`.
- [ ] Analyze on an empty project completes with no exception in the console.
- [ ] Every check row's `Why` button opens its own section of the optimization docs page.
- [ ] Muting a check hides its findings and survives a domain reload.
- [ ] A fix shows a confirmation dialog listing the affected assets before it writes anything.
- [ ] One Ctrl+Z after a fix that creates assets reverses the whole run, and leaves any asset that
      already existed at the same path untouched.
- [ ] Report-only checks show no fix button.
- [ ] The window compiles and runs both with and without the optional KTX2 package present.
- [ ] The tab strip names every check, each with its finding count, and selecting one shows only that
      check's findings. A muted check reads `muted` and a check that has not run reads `-`.
- [ ] Narrow the window until the tabs no longer fit. The strip scrolls sideways to reach the rest, no
      tab title is truncated, and the Analyze button stays in place beside the strip.
- [ ] The strip never grows a vertical scrollbar of its own, at any window width.
- [ ] The findings list scrolls vertically on its own, and the selected check's header row stays put
      above it while it scrolls.
- [ ] A per-row `Fix` on an undoable check applies straight away with no dialog, and Ctrl+Z reverses
      exactly that one row.
- [ ] A per-row `Fix` on a check that is not undoable still shows the confirmation dialog first.
- [ ] The packages check lists only packages this project does not have, and its `Install` button opens
      a Package Manager request. After the install finishes and scripts recompile, Analyze again and the
      installed package is gone from the list.
- [ ] Clicking an action that only opens a web page does not trigger a re-scan.
- [ ] Severity icons render as Unity's own info, warning, and error icons rather than as blank squares.

Two fixes are deliberately outside Undo, and both say so in their own source. Do not fail the
checklist on either:

- Applying the recommended build settings writes player settings, which live outside `Assets` and are
  not on the Undo stack. Each finding names the value it found, so the previous values can be typed
  back in by hand.
- Converting textures runs an external process that writes into `StreamingAssets` rather than creating
  assets, so there is nothing for Undo to hold. The output files can be deleted by hand.

Before tagging, run the anchor gate in release mode from a checkout of the SDK repository, not from a
consuming project. A non-zero exit means the optimization docs page is not published yet, so do not tag:

    node scripts~/check-optimizer-anchors.mjs --release
