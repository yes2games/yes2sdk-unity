# Optimizer manual checks

Run once in a scratch Unity 2021.3 project before tagging a release. This package has no
openable project of its own, so these cannot be automated here.

- [ ] The package imports with no compile errors.
- [ ] `Yes2SDK > Optimizer` appears directly under `Yes2SDK > Build Window`.
- [ ] Analyze on an empty project completes with no exception in the console.
- [ ] Every check row's `Why` button opens its own section of the optimization docs page.
- [ ] Muting a check hides its findings and survives a domain reload.
- [ ] A fix shows a confirmation dialog listing the affected assets before it writes anything.
- [ ] One Ctrl+Z after a fix reverses the whole run, including newly created assets.
- [ ] Report-only checks show no fix button.
- [ ] The window compiles and runs both with and without the optional KTX2 package present.

Before tagging, run the anchor gate in release mode from the repo root. A non-zero exit means the
optimization docs page is not published yet, so do not tag:

    node scripts~/check-optimizer-anchors.mjs --release
