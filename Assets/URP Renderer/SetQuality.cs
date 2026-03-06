using TMPro;
using UnityEngine;

namespace URP_Renderer
{
    /// <summary>
    /// Manages quality level switching with validation, logging, and persistence.
    /// Wire a UI Dropdown's OnValueChanged to <see cref="SetVisualQuality(int)"/>.
    /// The selected level is saved to PlayerPrefs and auto-applied on startup.
    /// </summary>
    public class SetQuality : MonoBehaviour
    {
        private const string PrefKey = "QualityLevel";

        [Tooltip("If true, automatically apply the saved quality level on Awake.")]
        [SerializeField] private bool applyOnAwake = true;

        [Tooltip("Optional: assign a UI Dropdown to auto-sync on startup.")]
        [SerializeField] private TMP_Dropdown qualityDropdown;

        private void Awake()
        {
            if (applyOnAwake)
            {
                int saved = PlayerPrefs.GetInt(PrefKey, QualitySettings.GetQualityLevel());
                ApplyQuality(saved);
            }
        }

        private void Start()
        {
            // Sync dropdown to current quality level if assigned
            if (qualityDropdown != null)
            {
                // Populate the dropdown with quality level names
                qualityDropdown.ClearOptions();
                var names = QualitySettings.names;
                var options = new System.Collections.Generic.List<string>(names);
                qualityDropdown.AddOptions(options);
                qualityDropdown.SetValueWithoutNotify(QualitySettings.GetQualityLevel());

                // Wire the dropdown change event
                qualityDropdown.onValueChanged.AddListener(SetVisualQuality);
            }
        }

        /// <summary>
        /// Sets the quality level by index. Safe to call from UI Dropdown OnValueChanged.
        /// </summary>
        public void SetVisualQuality(int index)
        {
            ApplyQuality(index);
        }

        private void ApplyQuality(int index)
        {
            string[] names = QualitySettings.names;

            if (index < 0 || index >= names.Length)
            {
                Debug.LogWarning(
                    $"[SetQuality] Invalid quality index {index}. " +
                    $"Valid range: 0–{names.Length - 1}. Falling back to current level.");
                return;
            }

            int previousLevel = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(index, applyExpensiveChanges: true);
            int actualLevel = QualitySettings.GetQualityLevel();

            // Verify it actually changed (can fail if the level is excluded for this platform)
            if (actualLevel != index)
            {
                Debug.LogWarning(
                    $"[SetQuality] Requested level '{names[index]}' (index {index}) but Unity applied " +
                    $"'{names[actualLevel]}' (index {actualLevel}). This level may be excluded from " +
                    $"this platform in Project Settings > Quality > Excluded Platforms.");
            }
            else
            {
                Debug.Log($"[SetQuality] Quality changed: '{names[previousLevel]}' → '{names[actualLevel]}'");
            }

            // Persist the choice
            PlayerPrefs.SetInt(PrefKey, actualLevel);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Returns the currently active quality level index.
        /// </summary>
        public int GetCurrentQuality() => QualitySettings.GetQualityLevel();
    }
}
