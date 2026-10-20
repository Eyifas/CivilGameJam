using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Fixes the known Unity URP 2D build error:
/// "More than one global light on layer Default for light blend style index 0"
/// 
/// In Unity URP 2D, Light2DPrefilteringAnalysis opens all build scenes additively during
/// the build preprocessor step (callbackOrder = -100). If a scene with a Global Light 2D is
/// already open in the editor (e.g. MainScene), opening each subsequent scene additively causes
/// Light2DManager to detect duplicate global lights and call Debug.LogError, failing the build.
/// 
/// This script runs before URP's preprocessor (callbackOrder = -200), temporarily disabling
/// the currently loaded scene's global lights so they do not conflict, and restores them
/// immediately in OnPostprocessBuild.
/// </summary>
public class URP2DGlobalLightBuildFix : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => -200;

    private static readonly List<Light2D> s_TemporarilyDisabledLights = new List<Light2D>();

    public void OnPreprocessBuild(BuildReport report)
    {
        s_TemporarilyDisabledLights.Clear();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var rootObj in scene.GetRootGameObjects())
            {
                var lights = rootObj.GetComponentsInChildren<Light2D>(true);
                foreach (var light in lights)
                {
                    if (light != null && light.enabled && light.lightType == Light2D.LightType.Global)
                    {
                        light.enabled = false;
                        s_TemporarilyDisabledLights.Add(light);
                    }
                }
            }
        }

        if (s_TemporarilyDisabledLights.Count > 0)
        {
            Debug.Log($"<color=cyan>[URP2DGlobalLightBuildFix] Temporarily disabled {s_TemporarilyDisabledLights.Count} open global light(s) to prevent additive scene analysis collision during build.</color>");
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        RestoreLights();
    }

    [InitializeOnLoadMethod]
    private static void InitSafetyHooks()
    {
        // Safety fallback: Ensure lights are always restored if build pipeline is interrupted
        EditorApplication.delayCall += RestoreLights;
    }

    private static void RestoreLights()
    {
        if (s_TemporarilyDisabledLights.Count > 0)
        {
            int restoredCount = 0;
            foreach (var light in s_TemporarilyDisabledLights)
            {
                if (light != null)
                {
                    light.enabled = true;
                    restoredCount++;
                }
            }
            s_TemporarilyDisabledLights.Clear();
            if (restoredCount > 0)
            {
                Debug.Log($"<color=cyan>[URP2DGlobalLightBuildFix] Restored {restoredCount} global light(s) after build.</color>");
            }
        }
    }
}
