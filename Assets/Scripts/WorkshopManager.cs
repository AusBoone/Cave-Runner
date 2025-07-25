#region WorkshopManager Overview
/*
 * Provides convenience wrappers around the Steamworks UGC API so the game can
 * download and upload community content. Usage typically looks like:
 *
 *   // After SteamManager has initialized Steamworks
 *   WorkshopManager.Instance.DownloadSubscribedItems(paths =>
 *   {
 *       foreach (var dir in paths)
 *       {
 *           // Load assets from each directory
 *       }
 *   });
 *
 * Call <see cref="UploadItem"/> to publish new content. These features require
 * the Steamworks.NET plugin and only function on standalone platforms.
 * 2024 update: introduced helper methods that load downloaded content using
 * Unity Addressables so large workshop packs stream in smoothly.
 */
#endregion
using UnityEngine;
#if UNITY_STANDALONE
using Steamworks;
#endif
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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
    private List<CallResult<DownloadItemResult_t>> downloadResults;
    private HashSet<PublishedFileId_t> pendingDownloads;
    private List<string> downloadPaths;
    private System.Action<List<string>> downloadsCallback;
#endif

    /// <summary>
    /// Sets up the singleton instance and checks Steam initialization.
    /// </summary>
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

    /// <summary>
    /// Cleans up the singleton instance on destruction.
    /// </summary>
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
        if (count == 0)
        {
            callback?.Invoke(new List<string>());
            return;
        }

        PublishedFileId_t[] ids = new PublishedFileId_t[count];
        SteamUGC.GetSubscribedItems(ids, count);
        pendingDownloads = new HashSet<PublishedFileId_t>(ids);
        downloadPaths = new List<string>();
        downloadsCallback = callback;
        downloadResults = new List<CallResult<DownloadItemResult_t>>();

        foreach (var id in ids)
        {
            var handle = SteamUGC.DownloadItem(id, true);
            var result = CallResult<DownloadItemResult_t>.Create((res, failure) =>
            {
                if (!failure && res.m_eResult == EResult.k_EResultOK && pendingDownloads.Contains(res.m_nPublishedFileId))
                {
                    var folder = new System.Text.StringBuilder(1024);
                    bool call = SteamUGC.GetItemInstallInfo(res.m_nPublishedFileId, out ulong size, folder, (uint)folder.Capacity, out uint time);
                    if (call)
                    {
                        downloadPaths.Add(folder.ToString());
                    }
                }

                if (pendingDownloads != null)
                {
                    pendingDownloads.Remove(res.m_nPublishedFileId);
                    if (pendingDownloads.Count == 0)
                    {
                        var cb = downloadsCallback;
                        downloadsCallback = null;
                        cb?.Invoke(downloadPaths);
                        downloadResults.Clear();
                        pendingDownloads = null;
                        downloadPaths = null;
                    }
                }
            });
            result.Set(handle);
            downloadResults.Add(result);
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

    /// <summary>
    /// Loads an addressable asset and invokes the callback when finished. A
    /// loading indicator is shown via <see cref="UIManager"/> while the request
    /// is in progress.
    /// </summary>
    public void LoadAddressableAsset<T>(string address, System.Action<T> callback)
    {
        StartCoroutine(LoadAddressableRoutine(address, callback));
    }

    private IEnumerator LoadAddressableRoutine<T>(string address, System.Action<T> callback)
    {
        if (string.IsNullOrEmpty(address))
        {
            callback?.Invoke(default);
            yield break;
        }

        UIManager.Instance?.ShowLoadingIndicator();
        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
        yield return handle;
        T result = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : default;
        callback?.Invoke(result);
        Addressables.Release(handle);
        UIManager.Instance?.HideLoadingIndicator();
    }
#endif
}
