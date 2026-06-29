using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;

namespace TankTrouble.Editor
{
    public sealed class BuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = CollectErrors();
            if (errors.Count > 0)
                throw new BuildFailedException(string.Join(System.Environment.NewLine, errors));
        }

        [MenuItem("Tank Trouble/Validate Project")]
        public static void ValidateProjectMenu()
        {
            var errors = CollectErrors();
            if (errors.Count == 0)
            {
                Debug.Log("Tank Trouble project validation passed.");
                return;
            }

            foreach (var error in errors)
                Debug.LogError(error);
        }

        public static List<string> CollectErrors()
        {
            var errors = new List<string>();
            ValidateConstants(errors);
            ValidateLayers(errors);
            ValidateBuildScenes(errors);
            return errors;
        }

        private static void ValidateConstants(List<string> errors)
        {
            if (CoordinateUtil.PixelsPerUnit <= 0f)
                errors.Add("PixelsPerUnit must be positive.");
            if (GameConfig.GridCols != 15 || GameConfig.GridRows != 11)
                errors.Add("Grid size must stay 15 x 11 unless the design document is updated.");
            if (GameConfig.MaxBounces != 7)
                errors.Add("Bullet max bounce count must stay at 7.");
            if (GameConfig.TankRotationSpeedDeg != 150f)
                errors.Add("AI and player tank turn speed must stay 150 deg/s.");
        }

        private static void ValidateLayers(List<string> errors)
        {
            RequireLayer("Tank", errors);
            RequireLayer("Bullet", errors);
            RequireLayer("Wall", errors);
        }

        private static void RequireLayer(string layerName, List<string> errors)
        {
            if (LayerMask.NameToLayer(layerName) < 0)
                errors.Add($"Required layer missing: {layerName}");
        }

        private static void ValidateBuildScenes(List<string> errors)
        {
            var hasEnabledScene = false;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    hasEnabledScene = true;
                    break;
                }
            }

            if (!hasEnabledScene)
                errors.Add("No enabled scene is configured in Build Settings.");
        }
    }
}
