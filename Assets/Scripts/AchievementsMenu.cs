using UnityEngine;
using UnityEngine.UI;
#if UNITY_STANDALONE
using Steamworks;
#endif

/// <summary>
/// Displays a scrollable list of Steam achievements and their unlocked state.
/// Attach this component to a panel containing a vertical layout group. Provide
/// a prefab with a <see cref="Text"/> component for each entry.
/// </summary>
public class AchievementsMenu : MonoBehaviour
{
    [Tooltip("Prefab used to display each achievement entry.")]
    public GameObject entryPrefab;
    [Tooltip("Parent transform where instantiated entries are placed.")]
    public Transform listParent;

    void Start()
    {
        PopulateList();
    }

    // Queries Steam for all achievements and instantiates a UI entry for each.
    private void PopulateList()
    {
        if (entryPrefab == null || listParent == null)
            return;

#if UNITY_STANDALONE
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
            Text text = entry.GetComponentInChildren<Text>();
            if (text != null)
            {
                string unlocked = achieved ? LocalizationManager.Get("achievement_unlocked") : string.Empty;
                text.text = string.Format("{0} - {1}{2}", name, desc, unlocked);
            }
        }
#endif
    }
}
