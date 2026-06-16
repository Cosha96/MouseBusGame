using System;
using System.Collections.Generic;
using UnityEngine;

// Create one asset per level: right-click Project → Create → Mousebus → Level Score Config
// Or run Mousebus → Create Level Score Config (Tutorial) for a ready-made starting point.
//
// Each stat has a weight that controls how much it contributes to the final score.
// Weights don't need to sum to 1 — they're normalized at compute time.
// This means you can add a new stat mid-development and just give it a weight without
// rebalancing every existing entry.
[CreateAssetMenu(menuName = "Mousebus/Level Score Config")]
public class LevelScoreConfig : ScriptableObject
{
    [Serializable]
    public class StatConfig
    {
        [Tooltip("Must match the statId passed to ScoreTracker.Report()")]
        public string statId      = "passengers";
        public string displayName = "Passengers";

        [Range(0f, 1f)]
        [Tooltip("Relative importance. All weights are normalized — they don't need to sum to 1.")]
        public float weight = 1f;
    }

    public List<StatConfig> stats = new();

    public const float GreenThreshold  = 0.80f;   // 80 %+ = Green
    public const float YellowThreshold = 0.60f;   // 60 %+ = Yellow, below = Red
}
