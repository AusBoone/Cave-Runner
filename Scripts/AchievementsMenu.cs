/*
 * AchievementsMenu.cs
 * -----------------------------------------------------------------------------
 * Populates a simple scrollable UI list with the player's Steam achievements.
 * Steamworks.NET must be initialised via SteamManager prior to use. When the
 * game runs without Steam (for example in WebGL) the menu will simply remain
 * empty. Typical usage:
 *   - Attach this component to a panel with a VerticalLayoutGroup.
 *   - Assign 'entryPrefab' which contains a TMP_Text component.
 *   - The Start method calls PopulateList automatically, but tests may invoke
 *     it directly.
 * Achievements are queried once at startup and displayed using localized
 * strings from LocalizationManager so language changes reflect immediately.
 * -----------------------------------------------------------------------------
 */
using UnityEngine;
using TMPro; // TextMeshPro is used for achievement entry labels
#if UNITY_STANDALONE
using Steamworks;
#endif

/// <summary>
/// Displays a scrollable list of Steam achievements and their unlocked state.
/// Attach this component to a panel containing a vertical layout group. Provide
/// a prefab with a <see cref="TMP_Text"/> component for each entry.
/// </summary>
public class AchievementsMenu : MonoBehaviour
{
    [Tooltip("Prefab used to display each achievement entry.")]
    /// <summary>
    /// Prefab used to visualise a single achievement. Must contain a
    /// <see cref="TMP_Text"/> component which is populated with the achievement
    /// name and description.
    /// </summary>
    public GameObject entryPrefab;
    [Tooltip("Parent transform where instantiated entries are placed.")]
    /// <summary>
    /// Container under which achievement prefabs are instantiated. Should
    /// typically be a UI layout group so entries stack vertically.
    /// </summary>
    public Transform listParent;

    /// <summary>
    /// Called on start to build the achievement list. Separated so tests can
    /// invoke <see cref="PopulateList"/> directly without triggering Unity's
    /// Start lifecycle.
    /// </summary>
    void Start()
    {
        PopulateList();
    }

    /// <summary>
    /// Queries Steam for all achievements and instantiates a UI entry for each
    /// one. Entries show the name, description and an optional "unlocked" tag.
    /// The method safely does nothing when Steamworks is not initialised so the
    /// game can run on platforms without Steam support.
    /// </summary>
    private void PopulateList()
    {
        if (entryPrefab == null || listParent == null)
            return;

#if UNITY_STANDALONE
        // Steamworks.NET is only available on standalone platforms. Skip
        // population in WebGL and mobile builds where the API is absent.
        if (SteamManager.Instance == null)
            return;

        int count = SteamUserStats.GetNumAchievements();
        for (int i = 0; i < count; i++)
        {
            string id = SteamUserStats.GetAchievementName(i);
            string name = SteamManager.GetAchievementName(id);
            string desc = SteamManager.GetAchievementDescription(id);
            bool achieved;
            SteamUserStats.GetAchievement(id, out achieved);

            GameObject entry = Instantiate(entryPrefab, listParent);
            // Use TextMeshPro for crisp, flexible rendering of achievement info.
            TMP_Text text = entry.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                string unlocked = achieved ? LocalizationManager.Get("achievement_unlocked") : string.Empty;
                text.text = string.Format("{0} - {1}{2}", name, desc, unlocked);
            }
        }
#endif
    }
}
