<!--
  AchievementsMenu.md
  -------------------
  Describes how to configure the achievements listing UI.
  Includes prefab requirements and integration snippets for Steam and localization.
-->
# Achievements Menu

This guide explains how to present Steam achievements inside the in‑game menu.
It covers the required prefab structure, component setup, and scripts needed to
fetch and localize achievement data.

## Purpose

The achievements menu displays every achievement available in the build and
shows whether each has been unlocked. It can be opened from any other menu and
updates whenever achievement state changes.

## Required Prefab

- **`entryPrefab`** – A prefab representing a single achievement row. It should
  contain text fields for the name and description plus an optional icon or
  checkmark to indicate completion.

## Setup Steps

1. **Create the menu container**
   - Add a `ScrollView` (or vertical layout group) under your UI canvas.
   - Create a content panel to hold instantiated entries.
2. **Assign the prefab**
   - Drag the `entryPrefab` into the AchievementsMenu component's inspector
     field.
3. **Initialize the menu**
   - On `Start`, call a method that queries `SteamManager` for all known
     achievements and instantiates an entry for each.

## Usage Examples

### Hooking into `SteamManager`

```csharp
// AchievementsMenu.cs – populate the menu with Steam data at startup
private void Start()
{
    // Fetch achievement metadata (IDs, unlock state, localization keys)
    var achievements = SteamManager.Instance.GetAchievements();

    foreach (var ach in achievements)
    {
        // Instantiate a row for each achievement
        var entry = Instantiate(entryPrefab, contentRoot);

        // Apply localized text and completion status
        entry.Initialize(
            LocalizationManager.Localize(ach.nameKey),
            LocalizationManager.Localize(ach.descKey),
            ach.isUnlocked);
    }
}
```

### Hooking into `LocalizationManager`

```csharp
// Entry prefab component – updates text fields when language changes
public void Initialize(string name, string description, bool unlocked)
{
    nameText.text = name;               // Localized achievement name
    descriptionText.text = description; // Localized achievement description
    checkmark.SetActive(unlocked);      // Show a checkmark if unlocked
}

private void OnEnable()
{
    // Reapply localization if the language changes while the menu is open
    LocalizationManager.LanguageChanged += RefreshTexts;
}

private void OnDisable()
{
    LocalizationManager.LanguageChanged -= RefreshTexts;
}
```

These snippets assume `SteamManager` exposes a `GetAchievements` method
returning objects with `nameKey`, `descKey`, and `isUnlocked` fields, and that
`LocalizationManager` raises `LanguageChanged` when the active language toggles.

## Related Documentation

- `SteamManager` usage and achievement definitions are covered in
  [`docs/ArchitectureOverview.md`](ArchitectureOverview.md#steam-achievements).
- For broad architectural context see the full
  [Architecture Overview](ArchitectureOverview.md) document.
