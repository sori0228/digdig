using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// This project has zero [BurstCompile] code, but Burst still runs its mandatory AOT
// compilation pass on every player build. On this machine, that pass's temp file gets
// deleted/locked by ESTsoft/ALYac antivirus before Burst can read it back, failing every
// build with a FileNotFoundException out of BurstAotCompiler.OnPostBuildPlayerScriptDLLsImpl.
// There's no public API to disable Burst AOT, so this reflects into the internal
// Unity.Burst.Editor.BurstPlatformAotSettings type. Safe for any project not actually
// using Burst-compiled jobs. See feedback_unity_mcp_workflow memory gotcha #4/#10.
public static class DisableBurstAot
{
    static System.Type FindType(string shortName)
    {
        var asm = System.AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Unity.Burst.Editor");
        return asm?.GetTypes().FirstOrDefault(t => t.Name == shortName);
    }

    [MenuItem("DigGame Tools/Debug Dump GetOrCreateSettings Signature")]
    public static void DumpSignature()
    {
        var t = FindType("BurstPlatformAotSettings");
        if (t == null) { Debug.LogError("[BurstFix] Type not found."); return; }

        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                     .Where(m => m.Name == "GetOrCreateSettings" || m.Name == "Save"))
        {
            var ps = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName + " " + p.Name));
            Debug.Log($"[BurstFix] {(m.IsStatic ? "static " : "")}{m.ReturnType.FullName} {m.Name}({ps})");
        }

        var f = t.GetField("EnableBurstCompilation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (f != null) Debug.Log($"[BurstFix] Field EnableBurstCompilation type={f.FieldType.FullName} isStatic={f.IsStatic}");
    }

    [MenuItem("DigGame Tools/Disable Burst AOT (Windows)")]
    public static void Disable()
    {
        var settingsType = FindType("BurstPlatformAotSettings");
        if (settingsType == null)
        {
            Debug.LogError("[BurstFix] Could not find Unity.Burst.Editor.BurstPlatformAotSettings type.");
            return;
        }

        var getOrCreate = settingsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetOrCreateSettings");
        var saveMethod = settingsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Save");
        var enabledField = settingsType.GetField("EnableBurstCompilation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        if (getOrCreate == null || saveMethod == null || enabledField == null)
        {
            Debug.LogError($"[BurstFix] Reflection lookup failed - getOrCreate={getOrCreate != null} save={saveMethod != null} enabledField={enabledField != null}.");
            return;
        }

        object settings = getOrCreate.Invoke(null, new object[] { BuildTarget.StandaloneWindows64 });
        enabledField.SetValue(settings, false);

        // Save might be static(settings, target) or instance(target) - handle either.
        if (saveMethod.IsStatic) saveMethod.Invoke(null, new object[] { settings, BuildTarget.StandaloneWindows64 });
        else saveMethod.Invoke(settings, new object[] { BuildTarget.StandaloneWindows64 });

        Debug.Log("[BurstFix] Disabled Burst AOT compilation for StandaloneWindows64/StandaloneWindows.");
    }
}
