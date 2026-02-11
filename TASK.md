# TASK.md — NewPCSetupWPF

## Phase 1: Theme System
- [x] `Theme.DarkElegant.xaml` — renamed **Midnight Blue** — refined dark blue, lavender accents
- [x] `Theme.GamerRgb.xaml` — renamed **Neon Cyberpunk** — AMOLED black, electric blue #3BA9FF/magenta
- [x] `Theme.Corporate.xaml` — renamed **Slate Professional** — warm obsidian, muted teal

## Phase 2: UI Overhaul (V1 → V7)
- [x] Sidebar nav + card layout
- [x] DynamicResource everywhere
- [x] Modern control styles (CheckBox, ProgressBar, TabControl, ScrollBar)
- [x] Profile pill buttons (Default, Dev, Home Office, Gaming, Custom)
- [x] Custom ComboBox template with DataTemplate binding
- [x] Execution log in sidebar
- [x] Category horizontal pill tabs in Store
- [x] Fixed TabControl template with visible headers + accent underline
- [x] Tab 1 "📦 My Setup" — only selected + installed apps
- [x] DefaultCatalog = exact 12 apps (user-specified)
- [x] Alphabetical sorting + pending first
- [x] FilterAppItem only shows IsSelected || IsInstalled
- [x] Removed confusing SearchQuery TextBox
- [x] Profiles auto-add missing apps from store catalog
- [x] Store tab checkboxes (StoreTrendItem class with IsSelected)
- [x] **V7: Themes renamed** — Midnight Blue, Neon Cyberpunk, Slate Professional
- [x] **V7: Removed Install All button** — prevented potential mass-install issues
- [x] **V7: Removed Select All/None from header** — moved to per-tab footers
- [x] **V7: Store by Category footer** — Select All, Select None, Add to My Setup
- [x] **V7: Full Search tab (🔍)** — live `winget search` with checkbox selection
- [x] **V7: CatalogStatus column** — shows "📋 In My Setup" or "✅ Installed" per row
- [x] **V7: WingetSearchService.cs** — parses `winget search` output async

## Phase 3: Store Catalog
- [x] 30+ free apps per category (static curated catalog)
- [x] 7 categories: Productivity, Communication, Media & Creativity, Development, Gaming, Security & Privacy, System Utilities
- [x] StoreTrendItem class with IsSelected, CatalogStatus + INotifyPropertyChanged

## Phase 4: Profiles
- [x] Default (all 12 defaults), Dev (defaults + dev tools), Home Office, Gaming, Custom

## Phase 5: Build & Publish (V7)
- [x] Build: 0 warnings, 0 errors
- [x] Published: `bin\Release\net8.0-windows\win-x64\publish`

## Discovered During Work
- [x] Fix "My Setup" to show installed apps (do not hide them)
- [x] Fix "Full Search" returning no results (debug winget parsing)
- [x] Create "Gemini" Theme (novel, beautiful UI)
- [x] Refine "Gemini" Theme (fix hit-testing & transparency)
- [x] **App Icon**: Selected and applied user-chosen minimalist glowing PC icon
- [x] **UI Fixes**: High-contrast CheckBoxes (all themes), Fix "No apps checked" status update, Search on Enter key, Auto-scroll logs
- [x] **Logic**: Global "Add to My Setup" (Store + Search tabs combined)
- [x] **Simulation Mode**: "Dry run" install (fake success) to test flow without installing
- [x] **Theme Redesign**: Polish Corporate, Gamer, Midnight Blue
- [x] **New Theme**: "Hacker" (Matrix/Terminal style)
- [x] **Debug Fix**: Resolved persistent startup crash (Added SearchBoxStyle & InverseBoolConverter)
- [x] **Icon Fix**: Remove black border (make transparent) and increase logo size (PowerShell Script)
- [x] **Simulation Logic**: Remove random failure chance (confuses user) and add "[Simulated]" tag (Always Successful)
- [x] **UI Polish**: Improve visibility of "Selected" state in Theme/App lists (High Contrast ComboBox Items)



- [x] **Icon Final**: "Maximize" icon content (crop transparent padding) so it fills the taskbar slot
- [x] **CheckBoxes**: Custom ControlTemplate colored **CheckMark** (not background)
- [x] **Global Add**: Ensure "Add to My Setup" grabs selected apps from BOTH Store & Search tabs
- [x] **Selection Status**: Fix "No apps checked" in Search tab AND Store tab (Added Missing Events)
- [x] **Winget Check**: Confirmed app checks for Winget (does not auto-install)
- [x] **Localization Fix**: Handle Spanish/non-English `winget` output by verifying via `winget list`
- [x] **Spotify Fix**: Handle ID mismatches (Store vs Winget) by verifying app *Name* if ID check fails
- [x] **Interactive Mode**: Add "Silent Install" toggle to allow users to opt-out of default installer options
- [x] **Defaults**: Ensure "Silent Install" is **Off** by default (User Preference)
- [x] **Startup Detection**: Ensure `RefreshInstalledStates` also checks by Name (for Spotify/Store apps detection on launch)
- [x] **Store Catalog Status**: Update `UpdateStoreCatalogStatus` to use `winget` output so Store items show "Installed" even if not in "My Setup"
- [x] **Cleanup**: Remove "Simulate Install" features and "Recommended" text from Silent Install
- [x] **Icon Fix**: Maximize icon content (remove transparent padding) to appear larger in Taskbar
- [x] **Riot Fix**: Update default catalog Name to "Riot Vanguard" to match `winget list` output
- [x] **Rebranding**: Rename app to **"Initio"** in UI and Documentation

