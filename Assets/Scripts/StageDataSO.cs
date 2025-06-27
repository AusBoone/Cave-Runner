using UnityEngine;

/// <summary>
/// ScriptableObject wrapper around <see cref="StageManager.StageData"/> so stage
/// configurations can be stored as assets and reused between scenes.
/// It exposes spawn multipliers and probability weights for easy tweaking
/// in the Unity inspector.
/// </summary>
[CreateAssetMenu(menuName = "CaveRunner/Stage Data", fileName = "StageData")]
public class StageDataSO : ScriptableObject
{
    public StageManager.StageData stage;
}
