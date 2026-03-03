# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.1] - 2026-03-03

### Added
- Poki screenshot capture tool — capture Game View screenshots resized to Poki store format (800x480 + 100x56 JPG) directly from Play Mode
- Customizable keyboard shortcut for screenshot capture (default: F12), rebindable from the Tools tab
- Per-slot preview and delete buttons for screenshot management
- Smart screenshot index that fills empty slots first before overwriting
- `Yes2SDK/Capture Screenshot` menu item as fallback trigger

### Changed
- Extracted shared jslib helpers (`$__y2` utilities) to reduce duplication across 11 jslib bridge files (~310 lines removed)
- Replaced Bridge.cs callback boilerplate with delegate routing (~220 lines removed)
- Consolidated 7 stub modules into shared `Yes2SDKStubModule` base class (~250 lines removed)

### Fixed
- `Yes2SDK.Version` property now correctly returns `1.1.1` (was stuck at `1.0.0`)
- Prevented duplicate Poki SDK `gameLoadingStart`/`gameLoadingFinished` lifecycle calls

## [1.1.0] - 2026-03-02

### Added
- Sprite Atlas automation tool — scan for loose sprites and create WebGL-optimized atlases to reduce draw calls
- KTX2 texture compression tool — convert textures to Basis Universal for GPU-optimal runtime transcoding
- KTX2 runtime loader component (`Yes2SDKKtx2Image`) with optional `com.unity.cloud.ktx` integration
- Texture swap tool — replace texture references in scenes/prefabs with KTX2 runtime loaders to reduce build size
- `YES2SDK_KTX` scripting define when `com.unity.cloud.ktx` is installed
- IL2CPP link preservation for KTX assembly

### Changed
- Editor window refactored from single scrollable layout to three tabs: **Build**, **Optimization**, **Tools**
- Foldout sections replaced with always-visible content per tab
- Selected tab persists across window reopens via EditorPrefs

## [1.0.0] - 2026-02-07

### Added
- Unified C# API wrapping WebGL platform SDKs (Poki, CrazyGames) behind a single interface
- Full module support: Ads, Analytics, Session, Player, Data, Auth, Game, Banners, Friends, Score
- WebGL templates for Poki, CrazyGames, and Debug (Editor/local testing)
- Editor window (Yes2SDK > Settings) for template installation, platform selection, and build configuration
- CrazyGames SDK async detection with polling and CDN fallback
- Mock implementations for Editor testing
- jslib bridges for all active modules
- `Yes2SDKPlatformInit.jslib` postset bootstrap for CrazyGames HTML replacement
