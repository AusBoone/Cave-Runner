using UnityEngine;
#if UNITY_STANDALONE
using Steamworks;
#endif
using System.Collections.Generic;

/// <summary>
/// Handles Steam Workshop interactions for downloading and uploading
/// level or skin packs. Uses Steamworks.NET's UGC API under the hood.
/// </summary>
public class WorkshopManager : MonoBehaviour
{
    public static WorkshopManager Instance { get; private set; }

#if UNITY_STANDALONE
    private bool initialized;
    private CallResult<CreateItemResult_t> createResult;
    private CallResult<SubmitItemUpdateResult_t> submitResult;
#endif

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
#if UNITY_STANDALONE
            initialized = SteamManager.Instance != null;
#endif
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

#if UNITY_STANDALONE
    /// <summary>
    /// Downloads all subscribed workshop items and returns their local paths.
    /// </summary>
    public void DownloadSubscribedItems(System.Action<List<string>> callback)
    {
        if (!initialized)
        {
            callback?.Invoke(new List<string>());
            return;
        }

        uint count = SteamUGC.GetNumSubscribedItems();
        PublishedFileId_t[] ids = new PublishedFileId_t[count];
        SteamUGC.GetSubscribedItems(ids, count);

        List<string> paths = new List<string>();
        int remaining = ids.Length;
        foreach (var id in ids)
        {
            SteamUGC.DownloadItem(id, true);
            var folder = new System.Text.StringBuilder(1024);
            bool call = SteamUGC.GetItemInstallInfo(id, out ulong size, folder, (uint)folder.Capacity, out uint time);
            if (call)
            {
                paths.Add(folder.ToString());
                remaining--;
                if (remaining == 0)
                {
                    callback?.Invoke(paths);
                }
            }
            else
            {
                remaining--;
                if (remaining == 0)
                {
                    callback?.Invoke(paths);
                }
            }
        }
    }

    /// <summary>
    /// Creates or updates a workshop item from the specified folder.
    /// </summary>
    public void UploadItem(string folderPath, string previewImage, string title, string description, System.Action<bool> callback)
    {
        if (!initialized)
        {
            callback?.Invoke(false);
            return;
        }

        createResult = CallResult<CreateItemResult_t>.Create((result, failure) =>
        {
            if (failure || result.m_eResult != EResult.k_EResultOK)
            {
                callback?.Invoke(false);
                return;
            }

            var updateHandle = SteamUGC.StartItemUpdate(SteamUtils.GetAppID(), result.m_nPublishedFileId);
            SteamUGC.SetItemTitle(updateHandle, title);
            SteamUGC.SetItemDescription(updateHandle, description);
            SteamUGC.SetItemContent(updateHandle, folderPath);
            if (!string.IsNullOrEmpty(previewImage))
            {
                SteamUGC.SetItemPreview(updateHandle, previewImage);
            }

            submitResult = CallResult<SubmitItemUpdateResult_t>.Create((submitRes, submitFailure) =>
            {
                callback?.Invoke(!submitFailure && submitRes.m_eResult == EResult.k_EResultOK);
            });

            var submitHandle = SteamUGC.SubmitItemUpdate(updateHandle, string.Empty);
            submitResult.Set(submitHandle);
        });

        var createHandle = SteamUGC.CreateItem(SteamUtils.GetAppID(), EWorkshopFileType.k_EWorkshopFileTypeCommunity);
        createResult.Set(createHandle);
    }
#endif
}
