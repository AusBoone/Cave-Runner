using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests verifying that gameplay events unlock the expected Steam
/// achievements without requiring the real Steamworks API.
/// </summary>
public class AchievementTriggerTests
{
    private class DummySteamManager : SteamManager
    {
        public System.Collections.Generic.List<string> unlocked = new System.Collections.Generic.List<string>();
        public override void UnlockAchievement(string id)
        {
            unlocked.Add(id);
        }
    }

    [Test]
    public void ComboAchievement_UnlocksAtThreshold()
    {
        var steamObj = new GameObject("steam");
        var steam = steamObj.AddComponent<DummySteamManager>();

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();

        // Determine the cap so the test remains valid if designers adjust it.
        int max = (int)typeof(GameManager).GetField("maxComboMultiplier", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(gm);

        // Quickly collect coins to raise the combo multiplier to the cap.
        for (int i = 0; i < max; i++)
        {
            gm.AddCoins(1);
        }

        Assert.Contains("ACH_COMBO_10", steam.unlocked,
            "Reaching the maximum combo multiplier should unlock the achievement");

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(steamObj);
    }

    [Test]
    public void BossDefeat_TriggersAchievement()
    {
        var steamObj = new GameObject("steam");
        var steam = steamObj.AddComponent<DummySteamManager>();

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();

        gm.NotifyBossDefeated();

        Assert.Contains("ACH_FIRST_BOSS", steam.unlocked);

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(steamObj);
    }

    [Test]
    public void HardcoreWin_UnlocksAchievement()
    {
        var steamObj = new GameObject("steam");
        var steam = steamObj.AddComponent<DummySteamManager>();

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();
        gm.HardcoreMode = true;

        // Set distance field directly to simulate a long run
        typeof(GameManager).GetField("distance", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, 6000f);
        typeof(GameManager).GetField("coins", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, 0);

        gm.GameOver();

        Assert.Contains("ACH_HARDCORE_WIN", steam.unlocked);

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(steamObj);
    }
}
