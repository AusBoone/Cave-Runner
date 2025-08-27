using UnityEngine;

/// <summary>
/// ScriptableObject wrapper around <see cref="StageManager.StageData"/> so stage
/// configurations can be stored as assets and reused between scenes. All
/// prefabs and sprites are referenced via Addressables so they can be streamed
/// in on demand.
/// </summary>
[CreateAssetMenu(menuName = "CaveRunner/Stage Data", fileName = "StageData")]
public class StageDataSO : ScriptableObject
{
    public StageManager.StageData stage;
}
