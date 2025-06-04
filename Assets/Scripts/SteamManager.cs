using UnityEngine;
#if UNITY_STANDALONE
using Steamworks;
#endif

public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

#if UNITY_STANDALONE
    private bool initialized;
#endif

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
#if UNITY_STANDALONE
            try
            {
                initialized = SteamAPI.Init();
            }
            catch (System.DllNotFoundException e)
            {
                Debug.LogError("Steamworks DLL not found: " + e);
            }
#endif
        }
        else
        {
            Destroy(gameObject);
        }
    }

#if UNITY_STANDALONE
    void Update()
    {
        if (initialized)
        {
            SteamAPI.RunCallbacks();
        }
    }
#endif

    void OnDestroy()
    {
#if UNITY_STANDALONE
        if (Instance == this && initialized)
        {
            SteamAPI.Shutdown();
        }
#endif
    }

    public void UnlockAchievement(string id)
    {
#if UNITY_STANDALONE
        if (!initialized) return;
        bool achieved;
        if (SteamUserStats.GetAchievement(id, out achieved) && !achieved)
        {
            SteamUserStats.SetAchievement(id);
            SteamUserStats.StoreStats();
        }
#endif
    }

    public void SaveHighScore(int score)
    {
#if UNITY_STANDALONE
        if (!initialized) return;
        byte[] data = System.BitConverter.GetBytes(score);
        SteamRemoteStorage.FileWrite("highscore.dat", data, data.Length);
#endif
    }

    public int LoadHighScore()
    {
#if UNITY_STANDALONE
        if (!initialized) return 0;
        if (SteamRemoteStorage.FileExists("highscore.dat"))
        {
            int size = SteamRemoteStorage.GetFileSize("highscore.dat");
            byte[] data = new byte[size];
            SteamRemoteStorage.FileRead("highscore.dat", data, size);
            return System.BitConverter.ToInt32(data, 0);
        }
#endif
        return 0;
    }
}
