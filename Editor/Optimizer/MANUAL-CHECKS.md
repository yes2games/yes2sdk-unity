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
- [ ] The sidebar lists every check, each with its finding count, and selecting one shows only that
      check's findings. A muted check reads `muted` and a check that has not run reads `-`.
- [ ] The sidebar scrolls when there are more checks than fit, and the Analyze button stays reachable
      below it.
- [ ] A per-row `Fix` on an undoable check applies straight away with no dialog, and Ctrl+Z reverses
      exactly that one row.
- [ ] A per-row `Fix` on a check that is not undoable still shows the confirmation dialog first.
- [ ] The packages check lists only packages this project does not have, and its `Install` button opens
      a Package Manager request. After the install finishes and scripts recompile, Analyze again and the
      installed package is gone from the list.
- [ ] Clicking an action that only opens a web page does not trigger a re-scan.
- [ ] Severity icons render as Unity's own info, warning, and error icons rather than as blank squares.
- [ ] Fixing a Read/Write texture, a Read/Write mesh, a mipmapped sprite, or an audio clip clears the
      Inspector value it names, and Analyze again drops the row.
- [ ] A clip with more than one audio problem shows one row per problem, and one fix run clears all of
      them.
- [ ] The Resources check lists an asset placed under `Assets/Resources`, and does not list one placed
      under `Assets/Editor/Resources`.
- [ ] The missing-script check finds a component whose script was deleted, both on a prefab and on an
      object in the open scene, and names the object's path within it.

Several fixes are deliberately outside Undo, and each says so in its own source. Do not fail the
checklist on any of them:

- Applying the recommended build settings writes player settings, which live outside `Assets` and are
  not on the Undo stack. Each finding names the value it found, so the previous values can be typed
  back in by hand.
- Converting textures runs an external process that writes into `StreamingAssets` rather than creating
  assets, so there is nothing for Undo to hold. The output files can be deleted by hand.
- The import-setting fixes (Read/Write on textures and meshes, sprite mipmaps, audio) write the asset's
  `.meta` file, which is not on the Undo stack. Every one of them names the asset it changed, so the
  value can be ticked back on in the Inspector.

Before tagging, run the anchor gate in release mode from a checkout of the SDK repository, not from a
consuming project. A non-zero exit means the optimization docs page is not published yet, so do not tag:

    node scripts~/check-optimizer-anchors.mjs --release
