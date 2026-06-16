using System.Collections.Generic;
using UnityEngine;

public enum ScoreGrade { Green, Yellow, Red }

// Static accumulator — survives the scene transition from a level to LevelComplete.
// LevelManager calls Reset() + Report() + FinalizeScore() before handing off.
// LevelCompleteUI reads LastResult to populate the score screen.
//
// Adding a new scoring dimension (e.g. "car_bumps") requires only:
//   1. Call ScoreTracker.Report("car_bumps", earned, max) in the relevant system.
//   2. Add a StatConfig entry in the level's LevelScoreConfig asset.
public static class ScoreTracker
{
    public struct StatResult
    {
        public string displayName;
        public float  earned;
        public float  max;
        public float  ratio;            // earned / max  (0–1)
        public float  normalizedWeight; // this stat's share of total weight
        public float  contribution;     // ratio × normalizedWeight
    }

    public struct ScoreResult
    {
        public float            percentage;  // 0–1 weighted total
        public ScoreGrade       grade;
        public List<StatResult> breakdown;
        public bool             isValid;
    }

    static readonly Dictionary<string, (float earned, float max)> _data = new();
    public static ScoreResult LastResult { get; private set; }

    // Clear stale data at the start of every level (and on editor Play entry)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() => _data.Clear();

    // Call once per scoring dimension when the level ends
    public static void Report(string statId, float earned, float max)
        => _data[statId] = (earned, max);

    // Call after all Report() calls — computes and stores LastResult
    public static void FinalizeScore(LevelScoreConfig config)
    {
        if (config == null || config.stats == null || config.stats.Count == 0)
        {
            LastResult = new ScoreResult { isValid = false };
            return;
        }

        // Normalize weights so they sum to 1 regardless of their raw values
        float totalWeight = 0f;
        foreach (var s in config.stats)
            totalWeight += Mathf.Max(0f, s.weight);
        if (totalWeight <= 0f) totalWeight = 1f;

        float weightedSum = 0f;
        var breakdown = new List<StatResult>();

        foreach (var cfg in config.stats)
        {
            float normWeight = Mathf.Max(0f, cfg.weight) / totalWeight;
            float earned = 0f, max = 0f, ratio = 0f;

            if (_data.TryGetValue(cfg.statId, out var d))
            {
                earned = d.earned;
                max    = d.max;
                ratio  = max > 0f ? Mathf.Clamp01(earned / max) : 0f;
            }

            float contribution = ratio * normWeight;
            weightedSum += contribution;

            breakdown.Add(new StatResult
            {
                displayName      = cfg.displayName,
                earned           = earned,
                max              = max,
                ratio            = ratio,
                normalizedWeight = normWeight,
                contribution     = contribution,
            });
        }

        ScoreGrade grade = weightedSum >= LevelScoreConfig.GreenThreshold  ? ScoreGrade.Green
                         : weightedSum >= LevelScoreConfig.YellowThreshold ? ScoreGrade.Yellow
                         : ScoreGrade.Red;

        LastResult = new ScoreResult
        {
            percentage = weightedSum,
            grade      = grade,
            breakdown  = breakdown,
            isValid    = true,
        };

        Debug.Log($"[ScoreTracker] {grade} — {weightedSum * 100f:F1}%");
    }
}
