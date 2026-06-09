using UnityEditor;
using UnityEngine;

// Two features in one file:
//   1. "Mousebus → Apply Environment Scale" — locks in SCALE_FACTOR and adds mesh
//      colliders to all FBXes in Art/Environments so the bus can drive on them.
//   2. "Mousebus → Place Environment in Scene" — drops the selected FBX into the
//      open scene under ENV_Root.

public class EnvironmentImportSettings : AssetPostprocessor
{
    private const string EnvFolder = "Assets/_Mousebus/Art/Environments";

    // ── Scale factor confirmed via Inspector testing ──────────────────────
    // 2.0 = correct for this Maya export (confirmed 2026-06-09)
    // If a future export looks wrong, re-test in Inspector and update this.
    // ─────────────────────────────────────────────────────────────────────
    private const float SCALE_FACTOR = 2.0f;

    [MenuItem("Mousebus/Apply Environment Scale")]
    private static void ApplyScale()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { EnvFolder });
        if (guids.Length == 0)
        {
            Debug.LogWarning("[EnvironmentImporter] No models found in " + EnvFolder);
            return;
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            importer.globalScale     = SCALE_FACTOR;
            importer.useFileUnits    = false;
            importer.importAnimation = false;

            // Generate a MeshCollider on every mesh in the FBX so the bus can
            // drive on the road surface. For a static environment this has no
            // meaningful performance cost. If you add UCX_ collision meshes in
            // Maya later, set this to false and let those take over.
            importer.addCollider = true;

            importer.SaveAndReimport();

            Debug.Log($"[EnvironmentImporter] Scale {SCALE_FACTOR} + colliders applied to: {path}");
        }

        EditorUtility.DisplayDialog("Done",
            $"Scale {SCALE_FACTOR} and mesh colliders applied to all models in Art/Environments.\n\n" +
            "If ENV_Root is already in the scene, delete it and re-run Place Environment in Scene " +
            "to pick up the colliders.", "OK");
    }

    // ── Menu item: drop selected FBX into the open scene ─────────────────

    [MenuItem("Mousebus/Place Environment in Scene")]
    private static void PlaceInScene()
    {
        GameObject selectedAsset = Selection.activeObject as GameObject;
        if (selectedAsset == null)
        {
            EditorUtility.DisplayDialog("Nothing Selected",
                "Select an FBX in the Project window under Art/Environments, then run this.", "OK");
            return;
        }

        // Find or create the ENV_Root parent — keeps the Hierarchy clean
        GameObject envRoot = GameObject.Find("ENV_Root");
        if (envRoot == null)
        {
            envRoot = new GameObject("ENV_Root");
            Undo.RegisterCreatedObjectUndo(envRoot, "Create ENV_Root");
            Debug.Log("[EnvironmentImporter] Created ENV_Root in scene.");
        }

        // InstantiatePrefab keeps the link to the source asset (not a plain copy)
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(selectedAsset);
        Undo.RegisterCreatedObjectUndo(instance, "Place Environment");
        instance.transform.SetParent(envRoot.transform, worldPositionStays: false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale    = Vector3.one;
        instance.name = selectedAsset.name;

        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);

        Debug.Log($"[EnvironmentImporter] Placed {selectedAsset.name} under ENV_Root.");
    }

    // Grey out the menu item unless an FBX in Art/Environments is selected
    [MenuItem("Mousebus/Place Environment in Scene", true)]
    private static bool PlaceInSceneValidate()
    {
        if (Selection.activeObject == null) return false;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return path.StartsWith(EnvFolder);
    }
}
