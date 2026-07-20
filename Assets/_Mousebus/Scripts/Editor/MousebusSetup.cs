using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One-time project setup tool.
// Menu bar: Mousebus → Run Project Setup
// Safe to re-run — existing files and scenes are skipped.
public static class MousebusSetup
{
    // All scenes in play order. MainMenu must be index 0 (auto-loads on launch).
    private static readonly string[] SceneNames =
    {
        "MainMenu",
        "Level_Tutorial",
        "Level_01_June",
        "Level_02_July",
        "Level_03_August",
        "Level_04_September",
        "Level_05_October",
        "Level_06_November",
        "Level_07_December",
        "Level_08_January",
        "Level_09_February",
        "Level_10_March",
        "Level_11_April",
        "Level_12_May",
        "LevelComplete",    // shown after every non-final level
        "Level_Outro"
    };

    [MenuItem("Mousebus/Run Project Setup")]
    public static void RunSetup()
    {
        // Prompt to save anything currently open before we start swapping scenes
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        CreateScenes();
        string prefabPath = CreateGameManagerPrefab();
        PlacePrefabInMainMenu(prefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Mousebus] Setup complete! MainMenu is open and ready.");
    }

    // ── Scene Creation ────────────────────────────────────────────────────

    private static void CreateScenes()
    {
        string folder = "Assets/_Mousebus/Scenes";
        var buildScenes = new List<EditorBuildSettingsScene>();

        foreach (string name in SceneNames)
        {
            string path = $"{folder}/{name}.unity";

            if (!File.Exists(path))
            {
                // NewSceneMode.Additive = create alongside whatever is open, not replace it
                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.DefaultGameObjects,
                    NewSceneMode.Additive
                );
                EditorSceneManager.SaveScene(scene, path);
                EditorSceneManager.CloseScene(scene, true); // true = remove from hierarchy too
                Debug.Log($"[Mousebus] Created scene: {name}");
            }
            else
            {
                Debug.Log($"[Mousebus] Scene exists, skipping: {name}");
            }

            buildScenes.Add(new EditorBuildSettingsScene(path, true));
        }

        // Replace the entire Build Settings scene list — order here = build index order
        EditorBuildSettings.scenes = buildScenes.ToArray();
        Debug.Log("[Mousebus] Build Settings updated.");
    }

    // ── GameManager Prefab ────────────────────────────────────────────────

    private static string CreateGameManagerPrefab()
    {
        string prefabPath = "Assets/_Mousebus/Prefabs/GameManager.prefab";

        if (File.Exists(prefabPath))
        {
            Debug.Log("[Mousebus] GameManager prefab already exists, skipping.");
            return prefabPath;
        }

        // ── Root object ──
        GameObject root = new GameObject("GameManager");
        root.AddComponent<GameManager>();
        SceneLoader sceneLoader = root.AddComponent<SceneLoader>();

        // ── Loading screen canvas ──
        // Screen Space Overlay renders on top of everything else in the scene.
        // Sort Order 100 ensures it beats any other overlay canvases we add later.
        GameObject canvasGO = new GameObject("LoadingScreen");
        canvasGO.transform.SetParent(root.transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<GraphicRaycaster>(); // required for Canvas to receive UI events
        CanvasGroup canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false; // loading screen is never interactive

        // ── Full-screen black panel ──
        GameObject panelGO = new GameObject("BlackPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelGO.AddComponent<Image>().color = Color.black;

        // Anchor all four corners to the parent so it stretches to fill any resolution
        RectTransform rect = panelGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // ── Wire CanvasGroup → SceneLoader ──
        // We use SerializedObject here because direct C# field assignment bypasses
        // Unity's serialization system and won't persist into the saved prefab.
        SerializedObject so = new SerializedObject(sceneLoader);
        so.FindProperty("loadingScreen").objectReferenceValue = canvasGroup;
        so.ApplyModifiedProperties();

        // ── Save prefab and clean up the temporary scene objects ──
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[Mousebus] GameManager prefab saved: {prefabPath}");
        return prefabPath;
    }

    // ── Place Prefab in MainMenu ───────────────────────────────────────────

    private static void PlacePrefabInMainMenu(string prefabPath)
    {
        string mainMenuPath = "Assets/_Mousebus/Scenes/MainMenu.unity";
        Scene mainMenu = EditorSceneManager.OpenScene(mainMenuPath, OpenSceneMode.Single);

        // Guard against placing a second GameManager if setup is re-run
        if (Object.FindFirstObjectByType<GameManager>() != null)
        {
            Debug.Log("[Mousebus] GameManager already in MainMenu, skipping placement.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        PrefabUtility.InstantiatePrefab(prefab, mainMenu);
        EditorSceneManager.SaveScene(mainMenu);

        Debug.Log("[Mousebus] GameManager prefab placed in MainMenu.");
    }

    // ── Level 00 Tutorial (sandbox scene) ────────────────────────────────

    // Adds Level_00_Tutorial to Build Settings right after Level_Tutorial,
    // but disabled — so it never gets a build index and never disrupts the
    // main menu flow. Open it directly in the editor to test real-level geo.
    // Run once after the scene file has been copied and Unity has imported it.
    [MenuItem("Mousebus/Add Level 00 Tutorial to Build Settings")]
    public static void AddLevel00Tutorial()
    {
        const string scenePath = "Assets/_Mousebus/Scenes/Level_00_Tutorial.unity";
        const string sceneName = "Level_00_Tutorial";

        if (!File.Exists(scenePath))
        {
            EditorUtility.DisplayDialog("Scene Not Found",
                scenePath + " does not exist.\n\nMake sure Unity has imported it " +
                "(check the Project window — it should appear in _Mousebus/Scenes).", "OK");
            return;
        }

        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.path == scenePath)
            {
                EditorUtility.DisplayDialog("Already Added",
                    sceneName + " is already in Build Settings.", "OK");
                return;
            }
        }

        var scenes   = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int afterTut = scenes.FindIndex(s => s.path.Contains("Level_Tutorial"));
        int insertAt = afterTut >= 0 ? afterTut + 1 : 1;

        // disabled = visible in Build Settings window but excluded from builds
        // and not assigned a build index, so Level_01_June etc. stay at their
        // expected indices and GameManager progression is unaffected.
        scenes.Insert(insertAt, new EditorBuildSettingsScene(scenePath, false));
        EditorBuildSettings.scenes = scenes.ToArray();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Mousebus] " + sceneName + " added to Build Settings (disabled).");
        EditorUtility.DisplayDialog("Done",
            sceneName + " added to Build Settings (disabled).\n\n" +
            "To test: double-click it in the Project window to open it, then hit Play.\n" +
            "The main menu flow and level progression are unchanged.", "OK");
    }

    // ── CutscenePlayer Prefab ─────────────────────────────────────────────

    // Adds the SlideImageOver cross-fade layer to an existing CutscenePlayer prefab.
    // Run this once if your prefab was created before the cross-fade feature was added.
    [MenuItem("Mousebus/Update Cutscene Prefab (Cross-fade)")]
    public static void UpdateCutscenePrefab()
    {
        string prefabPath = "Assets/_Mousebus/Prefabs/CutscenePlayer.prefab";

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            Debug.LogWarning("[Mousebus] CutscenePlayer prefab not found — run Create Cutscene Prefab first.");
            return;
        }

        // Work on a live instance so component references resolve correctly
        GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;

        Transform canvas = instance.transform.Find("CutsceneCanvas");
        if (canvas == null)
        {
            Debug.LogWarning("[Mousebus] CutsceneCanvas not found inside prefab.");
            Object.DestroyImmediate(instance);
            return;
        }

        // Find or create the overlay object
        Transform existingOver = canvas.Find("SlideImageOver");
        Image overImage;

        if (existingOver != null)
        {
            // Object exists — grab or add the Image component and re-wire below
            overImage = existingOver.GetComponent<Image>();
            if (overImage == null)
                overImage = existingOver.gameObject.AddComponent<Image>();
            Debug.Log("[Mousebus] SlideImageOver found — re-wiring reference.");
        }
        else
        {
            // Create it fresh
            GameObject overGO = new GameObject("SlideImageOver");
            overGO.transform.SetParent(canvas, false);

            // Place it directly above SlideImage but below SubtitleText
            Transform subtitle = canvas.Find("SubtitleText");
            if (subtitle != null)
                overGO.transform.SetSiblingIndex(subtitle.GetSiblingIndex());

            overImage                 = overGO.AddComponent<Image>();
            overImage.preserveAspect  = true;
            StretchToFill(overGO.GetComponent<RectTransform>());
        }

        // Always ensure it starts invisible
        overImage.color = new Color(1f, 1f, 1f, 0f);

        // Wire the new reference into CutscenePlayer
        CutscenePlayer player = instance.GetComponent<CutscenePlayer>();
        SerializedObject so   = new SerializedObject(player);
        so.FindProperty("slideImageOver").objectReferenceValue = overImage;
        so.ApplyModifiedProperties();

        // Save back to the prefab asset (preserves all existing scene references)
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Mousebus] CutscenePlayer prefab updated — cross-fade layer added.");
    }

    [MenuItem("Mousebus/Create Cutscene Prefab")]
    public static void CreateCutscenePrefab()
    {
        string prefabPath = "Assets/_Mousebus/Prefabs/CutscenePlayer.prefab";

        if (File.Exists(prefabPath))
        {
            Debug.Log("[Mousebus] CutscenePlayer prefab already exists, skipping.");
            return;
        }

        // ── Root object ──
        GameObject root = new GameObject("CutscenePlayer");
        CutscenePlayer player = root.AddComponent<CutscenePlayer>();

        // ── Canvas ──
        // Sort Order 50: above game world, below the loading screen (100)
        GameObject canvasGO = new GameObject("CutsceneCanvas");
        canvasGO.transform.SetParent(root.transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGO.AddComponent<GraphicRaycaster>();

        CanvasGroup canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0f; // hidden until Play() is called

        // ── Black background (sits behind the PNG) ──
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        bgGO.AddComponent<Image>().color = Color.black;
        StretchToFill(bgGO.GetComponent<RectTransform>());

        // ── Slide image — base layer (always shows the current/new image) ──
        // preserveAspect = true keeps portrait/landscape PNGs from stretching
        GameObject slideGO = new GameObject("SlideImage");
        slideGO.transform.SetParent(canvasGO.transform, false);
        Image slideImage = slideGO.AddComponent<Image>();
        slideImage.color = Color.white;
        slideImage.preserveAspect = true;
        StretchToFill(slideGO.GetComponent<RectTransform>());

        // ── Slide image overlay — cross-fade layer (shows the outgoing/old image) ──
        // Sits on top of SlideImage. During a transition the overlay shows the previous
        // slide and fades out to reveal the new one beneath — creating a true dissolve.
        GameObject slideOverGO = new GameObject("SlideImageOver");
        slideOverGO.transform.SetParent(canvasGO.transform, false);
        Image slideImageOver = slideOverGO.AddComponent<Image>();
        slideImageOver.color         = new Color(1f, 1f, 1f, 0f); // transparent until a transition starts
        slideImageOver.preserveAspect = true;
        StretchToFill(slideOverGO.GetComponent<RectTransform>());

        // ── Subtitle text (lower portion of the screen) ──
        GameObject textGO = new GameObject("SubtitleText");
        textGO.transform.SetParent(canvasGO.transform, false);

        TextMeshProUGUI subtitleText = textGO.AddComponent<TextMeshProUGUI>();
        subtitleText.text = "";
        subtitleText.fontSize = 32;
        subtitleText.color = Color.white;
        subtitleText.alignment = TextAlignmentOptions.Bottom;
        subtitleText.enableWordWrapping = true;

        // Position: horizontally centred, sitting in the bottom 20% of the screen
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.1f, 0.02f);
        textRect.anchorMax = new Vector2(0.9f, 0.22f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // ── Wire references into CutscenePlayer ──
        SerializedObject so = new SerializedObject(player);
        so.FindProperty("slideImage").objectReferenceValue     = slideImage;
        so.FindProperty("slideImageOver").objectReferenceValue = slideImageOver;
        so.FindProperty("subtitleText").objectReferenceValue   = subtitleText;
        so.FindProperty("canvasGroup").objectReferenceValue    = canvasGroup;
        so.ApplyModifiedProperties();

        // ── Save and clean up ──
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Mousebus] CutscenePlayer prefab saved: {prefabPath}");
    }

    // ── Bus Test Scene ────────────────────────────────────────────────────

    // Run this in any level scene to get a driveable bus cube instantly.
    // Replace the cube later with your imported bus model — BusController
    // and CameraFollow stay the same regardless of what the bus looks like.
    [MenuItem("Mousebus/Create Bus (Test Cube)")]
    public static void CreateTestBus()
    {
        if (Object.FindFirstObjectByType<BusController>() != null)
        {
            Debug.Log("[Mousebus] Bus already exists in this scene.");
            return;
        }

        // ── Ground plane ──
        if (GameObject.Find("Ground") == null)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f); // 100x100 units
        }

        // ── Bus cube ──
        // Base size (1.25 x 1 x 3 m) is the production standard — BusController.busScaleMultiplier
        // scales it at runtime. Set multiplier to 2 in Level_Tutorial for the old dummy geometry.
        GameObject bus = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bus.name = "Bus";
        bus.transform.localScale = new Vector3(1.25f, 1f, 3f);
        bus.transform.position   = new Vector3(0f, 0.5f, 0f); // sit flush on the ground

        Rigidbody rb         = bus.AddComponent<Rigidbody>();
        rb.mass              = 1500f;  // heavy bus — matters for collision response
        rb.linearDamping              = 0.5f;   // light resistance when coasting
        rb.angularDamping       = 5f;     // resists unwanted spinning from collisions
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // stops tunnelling at speed
        rb.interpolation = RigidbodyInterpolation.Interpolate;         // prevents jitter between physics and render frames
        // BusController sets the rotation constraints in its own Awake()

        bus.AddComponent<BusController>();

        // ── Camera ──
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            CameraFollow follow = mainCam.GetComponent<CameraFollow>()
                               ?? mainCam.gameObject.AddComponent<CameraFollow>();

            SerializedObject so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = bus.transform;
            so.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("[Mousebus] No Main Camera in scene — add CameraFollow manually and set Target to Bus.");
        }

        // Select the bus so the user can see it highlighted in the scene
        Selection.activeGameObject = bus;
        Debug.Log("[Mousebus] Bus created. Open a level scene, hit Play, and drive with WASD.");
    }

    // ── Floating Passenger Label ──────────────────────────────────────────

    // Adds a world-space passenger count label above the bus.
    // Run this once after creating the bus — it adds "12/30" text that hovers
    // above the bus and always faces the camera.
    [MenuItem("Mousebus/Add Floating Passenger Label")]
    public static void AddFloatingPassengerLabel()
    {
        BusController bus = Object.FindFirstObjectByType<BusController>();
        if (bus == null)
        {
            Debug.LogWarning("[Mousebus] No BusController found in scene. Create the bus first.");
            return;
        }

        // Don't add a second one if it already exists
        if (bus.GetComponentInChildren<FloatingPassengerCount>() != null)
        {
            Debug.Log("[Mousebus] Floating label already exists on this bus.");
            return;
        }

        // Create a child object sitting above the bus centre
        GameObject labelGO = new GameObject("PassengerLabel");
        labelGO.transform.SetParent(bus.transform, false);
        labelGO.transform.localPosition = new Vector3(0f, 2.5f, 0f); // above the bus roof

        // TextMeshPro (3D — not UI) renders in world space
        TMPro.TextMeshPro tmp = labelGO.AddComponent<TMPro.TextMeshPro>();
        tmp.text          = "0/0";
        tmp.fontSize      = 4f;         // world-space units — tune this to taste
        tmp.color         = Color.white;
        tmp.alignment     = TMPro.TextAlignmentOptions.Center;
        tmp.fontStyle     = TMPro.FontStyles.Bold;

        labelGO.AddComponent<FloatingPassengerCount>();

        Selection.activeGameObject = labelGO;
        EditorUtility.SetDirty(bus.gameObject);
        Debug.Log("[Mousebus] Floating passenger label added above the bus.");
    }

    // ── Bus Stops ─────────────────────────────────────────────────────────

    // Stamps a single BusStop trigger into the current scene.
    // Run this once per stop — each call creates the next numbered stop.
    // Move each stop along your route after creation.
    [MenuItem("Mousebus/Create Bus Stop")]
    public static void CreateBusStop()
    {
        // Count existing stops to auto-number the new one
        int existing = Object.FindObjectsByType<BusStop>(FindObjectsSortMode.None).Length;
        int number   = existing + 1;
        string name  = $"STOP_{number:00}";

        GameObject stop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stop.name = name;

        // Place each new stop a bit further along the Z axis as a starting point
        stop.transform.position   = new Vector3(0f, 1f, number * 10f);
        stop.transform.localScale = new Vector3(6f, 4f, 3f); // wide enough to catch the bus

        // Remove the mesh renderer — the stop is invisible in-game, visible via gizmo
        Object.DestroyImmediate(stop.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(stop.GetComponent<MeshFilter>());

        BoxCollider col = stop.GetComponent<BoxCollider>();
        col.isTrigger = true;

        BusStop busStop = stop.AddComponent<BusStop>();
        busStop.stopName         = name;
        busStop.waitingPassengers = 5;

        EditorUtility.SetDirty(busStop);
        Selection.activeGameObject = stop;

        Debug.Log($"[Mousebus] Created {name} at Z={number * 10f}. " +
                  "Move it to your stop position and set Waiting Passengers in the Inspector.");
    }

    // ── Level Triggers ────────────────────────────────────────────────────

    // Creates the two proxy trigger cubes for the basic level loop.
    // Move them in the scene to match your route once you have geometry.
    [MenuItem("Mousebus/Setup Level Triggers")]
    public static void SetupLevelTriggers()
    {
        // Halfway trigger — bus drives forward and hits this to trigger the midpoint cutscene.
        // Placed ahead by default; move it to the far end of your route.
        CreateLevelTrigger("TRG_Halfway", "halfway", new Vector3(0f, 1f, 40f));

        // End trigger — bus drives BACK and hits this to trigger the outro + complete the level.
        // Placed near the start by default; keep it close to where the level begins.
        CreateLevelTrigger("TRG_End", "end", new Vector3(0f, 1f, -5f));

        Debug.Log("[Mousebus] Level triggers created. Move TRG_Halfway to your route's far end, " +
                  "and TRG_End to the return destination near the start.");
    }

    private static void CreateLevelTrigger(string name, string id, Vector3 position)
    {
        if (GameObject.Find(name) != null)
        {
            Debug.Log($"[Mousebus] {name} already exists, skipping.");
            return;
        }

        GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trigger.name = name;
        trigger.transform.position = position;
        trigger.transform.localScale = new Vector3(6f, 4f, 1f); // wide gate the bus drives through

        BoxCollider col = trigger.GetComponent<BoxCollider>();
        col.isTrigger = true;

        LevelTrigger lt = trigger.AddComponent<LevelTrigger>();
        lt.triggerId = id;

        EditorUtility.SetDirty(lt);
        Debug.Log($"[Mousebus] Created {name} (id: '{id}') at {position}");
    }

    // ── Level Scene UI ────────────────────────────────────────────────────

    // Run this in an open level scene to get a HUD and a Pause Menu instantly.
    // Each is a Screen Space Overlay Canvas with a CanvasGroup for fade-in/out.
    [MenuItem("Mousebus/Setup Level Scene UI")]
    public static void SetupLevelSceneUI()
    {
        CreateHUD();
        CreatePauseMenu();
        CreateBusStopArrivalUI();

        AssetDatabase.SaveAssets();
        Debug.Log("[Mousebus] Level Scene UI created. Assign a BusController to the HUD if not auto-found.");
    }

    // ── Main Menu Settings ────────────────────────────────────────────────

    // Run with the MainMenu scene open.
    // Adds a Settings button to the main panel and stamps a SettingsPanel into
    // the canvas, then wires both into MainMenuUI.
    [MenuItem("Mousebus/Add Main Menu Settings")]
    public static void AddMainMenuSettings()
    {
        var menuUI = Object.FindFirstObjectByType<MainMenuUI>();
        if (menuUI == null)
        {
            EditorUtility.DisplayDialog("Scene Not Found",
                "Open the MainMenu scene first, then run this.", "OK");
            return;
        }

        SerializedObject so = new SerializedObject(menuUI);

        if (so.FindProperty("settingsPanel").objectReferenceValue != null)
        {
            EditorUtility.DisplayDialog("Already Done",
                "Settings panel already wired on this MainMenuUI.", "OK");
            return;
        }

        // Find the main panel so we can add the Settings button to it
        Canvas canvas    = menuUI.GetComponentInParent<Canvas>();
        Transform root   = canvas != null ? canvas.transform : menuUI.transform;
        Transform mainPanelT = root.Find("MainPanel");

        if (mainPanelT == null)
        {
            EditorUtility.DisplayDialog("Main Panel Not Found",
                "Could not find a child named 'MainPanel' inside the menu canvas.", "OK");
            return;
        }

        // Settings button — sits between Level Select and Quit
        // Shift existing Quit button down first
        Transform quitT = mainPanelT.Find("QuitButton");
        if (quitT != null)
        {
            var qRect = quitT.GetComponent<RectTransform>();
            qRect.offsetMin = new Vector2(10f, -310f);
            qRect.offsetMax = new Vector2(250f, -260f);
        }

        Button settingsBtn = CreateButton(mainPanelT, "SettingsButton",
            new Vector2(0f, 1f), new Vector2(10f, -250f), new Vector2(250f, -200f), "Settings");

        // SettingsPanel — reuse same creation as pause menu, parented to canvas root
        GameObject settingsRoot = new GameObject("SettingsPanel");
        settingsRoot.transform.SetParent(root, false);
        settingsRoot.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.09f, 0.97f);
        var settingsRect = settingsRoot.GetComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(0.5f, 0.5f);
        settingsRect.anchorMax = new Vector2(0.5f, 0.5f);
        settingsRect.pivot     = new Vector2(0.5f, 0.5f);
        settingsRect.sizeDelta = new Vector2(400f, 320f);

        TMP_Text settingsHeader = CreateLabel(settingsRoot.transform, "HeaderText",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16f, -50f), new Vector2(-16f, -10f), "SETTINGS", 22);
        settingsHeader.color = new Color(0.5f, 0.75f, 1f);

        GameObject sdiv = new GameObject("Divider");
        sdiv.transform.SetParent(settingsRoot.transform, false);
        sdiv.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.1f);
        var sdivRect = sdiv.GetComponent<RectTransform>();
        sdivRect.anchorMin = new Vector2(0f, 1f); sdivRect.anchorMax = new Vector2(1f, 1f);
        sdivRect.pivot = new Vector2(0.5f, 1f);
        sdivRect.anchoredPosition = new Vector2(0f, -54f);
        sdivRect.sizeDelta = new Vector2(-32f, 1f);

        CreateLabel(settingsRoot.transform, "MasterVolumeLabel",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(16f, -100f), new Vector2(0f, -68f), "Master Volume", 16);
        Slider masterSlider = CreateSlider(settingsRoot.transform, "MasterVolumeSlider",
            new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -100f), new Vector2(-16f, -68f));

        CreateLabel(settingsRoot.transform, "MusicVolumeLabel",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(16f, -155f), new Vector2(0f, -123f), "Music Volume", 16);
        Slider musicSlider = CreateSlider(settingsRoot.transform, "MusicVolumeSlider",
            new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -155f), new Vector2(-16f, -123f));

        CreateLabel(settingsRoot.transform, "SfxVolumeLabel",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(16f, -210f), new Vector2(0f, -178f), "SFX Volume", 16);
        Slider sfxSlider = CreateSlider(settingsRoot.transform, "SfxVolumeSlider",
            new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -210f), new Vector2(-16f, -178f));

        CreateLabel(settingsRoot.transform, "FullscreenLabel",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(16f, -265f), new Vector2(0f, -233f), "Fullscreen", 16);
        Toggle fsToggle = CreateToggle(settingsRoot.transform, "FullscreenToggle",
            new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -265f), new Vector2(-16f, -233f));

        UnityEngine.UI.Button backBtn = CreateButton(settingsRoot.transform, "BackButton",
            new Vector2(0.5f, 0f), new Vector2(-110f, 10f), new Vector2(110f, 50f), "← Back");

        SettingsPanel sp = settingsRoot.AddComponent<SettingsPanel>();
        SerializedObject spSO = new SerializedObject(sp);
        spSO.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
        spSO.FindProperty("musicVolumeSlider").objectReferenceValue  = musicSlider;
        spSO.FindProperty("sfxVolumeSlider").objectReferenceValue    = sfxSlider;
        spSO.FindProperty("fullscreenToggle").objectReferenceValue   = fsToggle;
        spSO.FindProperty("backButton").objectReferenceValue         = backBtn;
        spSO.ApplyModifiedProperties();

        // Wire both into MainMenuUI
        so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
        so.FindProperty("settingsPanel").objectReferenceValue  = sp;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(menuUI);
        EditorSceneManager.MarkSceneDirty(menuUI.gameObject.scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Done",
            "Settings button and panel added to Main Menu.\n\nSave the scene (Ctrl+S).", "OK");
    }

    // ── Audio ─────────────────────────────────────────────────────────────

    // Run with MainMenu or any level scene open.
    // Finds Music_<SceneName>.wav (or Music_MainMenu.wav) and wires it automatically.
    [MenuItem("Mousebus/Wire Audio Clips")]
    public static void WireAudioClips()
    {
        const string audioFolder = "Assets/_Mousebus/Art/Audio";
        string sceneName = EditorSceneManager.GetActiveScene().name;
        bool wired = false;

        // ── Main Menu ──
        var menuUI = Object.FindFirstObjectByType<MainMenuUI>();
        if (menuUI != null)
        {
            string path = $"{audioFolder}/Music_MainMenu.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                var so = new SerializedObject(menuUI);
                so.FindProperty("menuMusic").objectReferenceValue = clip;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(menuUI);
                Debug.Log($"[Mousebus] Wired Music_MainMenu.wav → MainMenuUI");
                wired = true;
            }
            else Debug.LogWarning($"[Mousebus] {path} not found.");
        }

        // ── Level Music ──
        var lm = Object.FindFirstObjectByType<LevelManager>();
        if (lm != null)
        {
            // Try scene-specific clip first (e.g. Music_Level_Tutorial.wav),
            // fall back to Music_Level_Tutorial.wav if none found.
            string[] candidates =
            {
                $"{audioFolder}/Music_{sceneName}.wav",
                $"{audioFolder}/Music_Level_Tutorial.wav"
            };

            AudioClip clip = null;
            string matched = null;
            foreach (string p in candidates)
            {
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (clip != null) { matched = p; break; }
            }

            if (clip != null)
            {
                var so = new SerializedObject(lm);
                so.FindProperty("levelMusic").objectReferenceValue = clip;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(lm);
                Debug.Log($"[Mousebus] Wired {System.IO.Path.GetFileName(matched)} → LevelManager");
                wired = true;
            }
            else Debug.LogWarning("[Mousebus] No level music clip found in Art/Audio.");

            // ── Alighting Bell ──
            var bellClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                $"{audioFolder}/SFX/SFX_Bell_chime.mp3");
            if (bellClip != null)
            {
                var so = new SerializedObject(lm);
                so.FindProperty("alightingBellClip").objectReferenceValue = bellClip;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(lm);
                Debug.Log("[Mousebus] Wired SFX_Bell_chime.mp3 → LevelManager.alightingBellClip");
                wired = true;
            }
            else Debug.LogWarning("[Mousebus] SFX/SFX_Bell_chime.mp3 not found.");
        }

        if (!wired)
        {
            EditorUtility.DisplayDialog("Nothing to wire",
                "Open MainMenu or a level scene (with a LevelManager in the Hierarchy) first.", "OK");
            return;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Done", "Audio clips wired. Save the scene (Ctrl+S).", "OK");
    }

    [MenuItem("Mousebus/Add Bus Stop Arrival UI")]
    public static void AddBusStopArrivalUI()
    {
        CreateBusStopArrivalUI();
        AssetDatabase.SaveAssets();
    }

    private static void CreateBusStopArrivalUI()
    {
        if (Object.FindFirstObjectByType<BusStopArrivalUI>() != null)
        {
            Debug.Log("[Mousebus] BusStopArrivalUI already exists in this scene.");
            return;
        }

        // Canvas — sort order 20, above HUD (10), below pause menu (40)
        GameObject canvasGO = new GameObject("BusStopArrivalUI");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        CanvasGroup cg = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        // Popup panel — centred horizontally, lower third of screen
        GameObject panelGO = new GameObject("PopupPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.72f);
        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.5f, 0f);
        panelRect.anchorMax        = new Vector2(0.5f, 0f);
        panelRect.pivot            = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 90f);
        panelRect.sizeDelta        = new Vector2(380f, 72f);

        // Single label — "ARRIVED\nSTOP NAME" via rich text
        TMP_Text stopText = CreateLabel(panelGO.transform, "StopNameText",
            Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f),
            "", 22);
        stopText.alignment         = TMPro.TextAlignmentOptions.Center;
        stopText.color             = new Color(1f, 0.45f, 0.75f); // HUD pink
        stopText.enableWordWrapping = false;

        BusStopArrivalUI ui = canvasGO.AddComponent<BusStopArrivalUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("canvasGroup").objectReferenceValue  = cg;
        so.FindProperty("stopNameText").objectReferenceValue = stopText;
        so.ApplyModifiedProperties();

        Debug.Log("[Mousebus] BusStopArrivalUI created.");
    }

    // ── Rebuild HUD ───────────────────────────────────────────────────────────

    // Deletes the existing HUD and rebuilds it with the updated layout:
    // Clock top-left | Passenger count top-centre | Next Stop top-right | Speedometer bottom-right
    [MenuItem("Mousebus/Rebuild HUD")]
    public static void RebuildHUD()
    {
        var existing = Object.FindFirstObjectByType<HUD>();
        if (existing != null)
        {
            Canvas c = existing.GetComponentInParent<Canvas>();
            Undo.DestroyObjectImmediate(c != null ? c.gameObject : existing.gameObject);
        }
        CreateHUD();
        EditorUtility.DisplayDialog("Done",
            "HUD rebuilt.\n\nLayout:\n• Speedometer — bottom-right\n• Clock — above speedometer (bottom-right)\n• Next Stop — top-right\n• Passenger count — top-centre", "OK");
    }

    private static void CreateHUD()
    {
        if (Object.FindFirstObjectByType<HUD>() != null)
        {
            Debug.Log("[Mousebus] HUD already exists in this scene.");
            return;
        }

        // Canvas
        GameObject canvasGO = new GameObject("HUD");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        CanvasGroup cg = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha = 1f; cg.interactable = false; cg.blocksRaycasts = false;

        Color hudPink = new Color(1f, 0.45f, 0.75f);

        // Speedometer — bottom-right
        // Offsets are relative to anchor (1,0) = canvas bottom-right corner.
        // +y = up from bottom, -x = left from right edge.
        TMP_Text speedText = CreateLabel(canvasGO.transform, "SpeedText",
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-190f, 12f), new Vector2(-12f, 77f),
            "0\n<size=60%>km/h</size>", 36);
        speedText.alignment = TMPro.TextAlignmentOptions.MidlineRight;
        speedText.color = hudPink;

        // Clock — bottom-right, 8px gap above speedometer
        TMP_Text clockText = CreateLabel(canvasGO.transform, "ClockText",
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-190f, 85f), new Vector2(-12f, 119f),
            "00:00", 22);
        clockText.alignment = TMPro.TextAlignmentOptions.MidlineRight;
        clockText.color = hudPink;

        // Next Stop — top-right
        // Offsets relative to anchor (1,1) = canvas top-right corner.
        // -y = down from top. offsetMin.y must be more negative than offsetMax.y.
        TMP_Text nextStopText = CreateLabel(canvasGO.transform, "NextStopText",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-220f, -80f), new Vector2(-12f, -10f),
            "", 20);
        nextStopText.alignment = TMPro.TextAlignmentOptions.TopRight;
        nextStopText.color = hudPink;

        // Passenger count — top-centre
        TMP_Text busText = CreateLabel(canvasGO.transform, "BusCountText",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-90f, -46f), new Vector2(90f, -10f),
            "0/40", 26);
        busText.alignment = TMPro.TextAlignmentOptions.Center;
        busText.color = hudPink;

        // Wire HUD component
        HUD hud = canvasGO.AddComponent<HUD>();
        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("speedText").objectReferenceValue    = speedText;
        so.FindProperty("nextStopText").objectReferenceValue = nextStopText;
        so.FindProperty("busCountText").objectReferenceValue = busText;
        so.FindProperty("clockText").objectReferenceValue    = clockText;
        so.FindProperty("canvasGroup").objectReferenceValue  = cg;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = canvasGO;
        Debug.Log("[Mousebus] HUD created.");
    }

    // ── Rebuild Pause Menu ────────────────────────────────────────────────

    // Deletes any existing PauseMenu and recreates it with the full button set:
    // Resume / Passengers / Settings / Main Menu — plus the PassengerListPanel.
    // Safe to re-run whenever the pause menu needs updating.
    [MenuItem("Mousebus/Rebuild Pause Menu")]
    public static void RebuildPauseMenu()
    {
        // Remove old pause menu if present
        var existing = Object.FindFirstObjectByType<PauseMenuUI>();
        if (existing != null)
        {
            // Find the root canvas
            Canvas c = existing.GetComponentInParent<Canvas>();
            Undo.DestroyObjectImmediate(c != null ? c.gameObject : existing.gameObject);
        }
        CreatePauseMenu();
        EditorUtility.DisplayDialog("Done",
            "Pause menu rebuilt with Resume / Passengers / Settings / Main Menu.\n\n" +
            "The Passengers button opens a list of everyone currently on the bus.", "OK");
    }

    private static void CreatePauseMenu()
    {
        // Canvas — sort order 40, below cutscene canvas (50)
        GameObject canvasGO = new GameObject("PauseMenu");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        CanvasGroup cg = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        // Full-screen dark overlay
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        bgGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.72f);
        StretchToFill(bgGO.GetComponent<RectTransform>());

        // ── Main Panel (title + 4 buttons, hidden when passenger list is open) ──
        GameObject mainPanel = new GameObject("MainPanel");
        mainPanel.transform.SetParent(canvasGO.transform, false);
        var mpImg = mainPanel.AddComponent<UnityEngine.UI.Image>();
        mpImg.color = Color.clear; mpImg.raycastTarget = false;
        StretchToFill(mainPanel.GetComponent<RectTransform>());

        CreateLabel(mainPanel.transform, "TitleText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-200f, 80f), new Vector2(200f, 140f),
            "PAUSED", 48);

        // Buttons — centred, 60px tall, 10px gap, stacked downward from centre
        var resumeBtn     = CreateButton(mainPanel.transform, "ResumeButton",
            new Vector2(0.5f, 0.5f), new Vector2(-130f,  20f), new Vector2(130f,  70f), "Resume");
        var passengersBtn = CreateButton(mainPanel.transform, "PassengersButton",
            new Vector2(0.5f, 0.5f), new Vector2(-130f, -45f), new Vector2(130f,   5f), "Passengers");
        var settingsBtn   = CreateButton(mainPanel.transform, "SettingsButton",
            new Vector2(0.5f, 0.5f), new Vector2(-130f,-110f), new Vector2(130f, -60f), "Settings");
        var mainMenuBtn   = CreateButton(mainPanel.transform, "MainMenuButton",
            new Vector2(0.5f, 0.5f), new Vector2(-130f,-175f), new Vector2(130f,-125f), "Main Menu");

        // ── Passenger List Panel ──────────────────────────────────────────
        GameObject listRoot = new GameObject("PassengerListPanel");
        listRoot.transform.SetParent(canvasGO.transform, false);
        var listBg = listRoot.AddComponent<UnityEngine.UI.Image>();
        listBg.color = new Color(0.05f, 0.05f, 0.09f, 0.97f);
        var listRect = listRoot.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 0.5f);
        listRect.anchorMax = new Vector2(0.5f, 0.5f);
        listRect.pivot     = new Vector2(0.5f, 0.5f);
        listRect.sizeDelta = new Vector2(480f, 440f);

        // ── List Screen ───────────────────────────────────────────────────
        GameObject listScreen = new GameObject("ListScreen");
        listScreen.transform.SetParent(listRoot.transform, false);
        var lsImg = listScreen.AddComponent<UnityEngine.UI.Image>();
        lsImg.color = Color.clear; lsImg.raycastTarget = false;
        StretchToFill(listScreen.GetComponent<RectTransform>());

        TMP_Text headerText = CreateLabel(listScreen.transform, "HeaderText",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16f, -50f), new Vector2(-16f, -10f),
            "ON BUS  (0)", 22);
        headerText.color = new Color(0.5f, 0.75f, 1f);

        // Divider
        GameObject ldiv = new GameObject("Divider");
        ldiv.transform.SetParent(listScreen.transform, false);
        ldiv.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.1f);
        var ldivRect = ldiv.GetComponent<RectTransform>();
        ldivRect.anchorMin = new Vector2(0f, 1f); ldivRect.anchorMax = new Vector2(1f, 1f);
        ldivRect.pivot = new Vector2(0.5f, 1f);
        ldivRect.anchoredPosition = new Vector2(0f, -54f);
        ldivRect.sizeDelta = new Vector2(-32f, 1f);

        // Entry container (entries are spawned here at runtime)
        GameObject entryContainer = new GameObject("EntryContainer");
        entryContainer.transform.SetParent(listScreen.transform, false);
        var ecImg = entryContainer.AddComponent<UnityEngine.UI.Image>();
        ecImg.color = Color.clear; ecImg.raycastTarget = false;
        var ecRect = entryContainer.GetComponent<RectTransform>();
        ecRect.anchorMin = new Vector2(0f, 0f); ecRect.anchorMax = new Vector2(1f, 1f);
        ecRect.offsetMin = new Vector2(0f, 60f); ecRect.offsetMax = new Vector2(0f, -58f);

        // Pagination row — prev / page label / next, hidden when only 1 page
        GameObject pageRow = new GameObject("PaginationRow");
        pageRow.transform.SetParent(listScreen.transform, false);
        var prImg = pageRow.AddComponent<UnityEngine.UI.Image>();
        prImg.color = Color.clear; prImg.raycastTarget = false;
        var prRect = pageRow.GetComponent<RectTransform>();
        prRect.anchorMin = new Vector2(0f, 0f); prRect.anchorMax = new Vector2(1f, 0f);
        prRect.pivot = new Vector2(0.5f, 0f);
        prRect.anchoredPosition = new Vector2(0f, 58f);
        prRect.sizeDelta = new Vector2(0f, 40f);

        var prevBtn  = CreateButton(pageRow.transform, "PrevButton",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 40f), "←");
        var nextBtn  = CreateButton(pageRow.transform, "NextButton",
            new Vector2(1f, 0f), new Vector2(-48f, 0f), new Vector2(0f, 40f), "→");
        TMP_Text pageLabel = CreateLabel(pageRow.transform, "PageLabel",
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(52f, 0f), new Vector2(-52f, 0f),
            "1 / 1", 17);
        pageLabel.alignment = TMPro.TextAlignmentOptions.Center;

        // Back button (bottom of list screen)
        var listBackBtn = CreateButton(listScreen.transform, "BackButton",
            new Vector2(0.5f, 0f), new Vector2(-110f, 10f), new Vector2(110f, 50f), "← Back");

        // ── Detail Screen ──────────────────────────────────────────────────
        GameObject detailScreen = new GameObject("DetailScreen");
        detailScreen.transform.SetParent(listRoot.transform, false);
        var dsImg = detailScreen.AddComponent<UnityEngine.UI.Image>();
        dsImg.color = Color.clear; dsImg.raycastTarget = false;
        StretchToFill(detailScreen.GetComponent<RectTransform>());
        detailScreen.SetActive(false);

        float dy = -16f; float dh = 58f;
        TMP_Text dName  = CreateLabel(detailScreen.transform, "DetailName",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, dy-dh), new Vector2(-24f, dy), "", 22);
        dy -= dh;
        TMP_Text dAge   = CreateLabel(detailScreen.transform, "DetailAge",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, dy-dh), new Vector2(-24f, dy), "", 20);
        dy -= dh;
        TMP_Text dJob   = CreateLabel(detailScreen.transform, "DetailJob",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, dy-dh), new Vector2(-24f, dy), "", 20);
        dy -= dh;
        TMP_Text dHobby = CreateLabel(detailScreen.transform, "DetailHobbies",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, dy-dh-16f), new Vector2(-24f, dy), "", 20);
        dy -= dh + 16f;
        TMP_Text dTimes = CreateLabel(detailScreen.transform, "DetailTimes",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, dy-dh), new Vector2(-24f, dy), "", 20);

        var detailBackBtn = CreateButton(detailScreen.transform, "BackButton",
            new Vector2(0.5f, 0f), new Vector2(-110f, 10f), new Vector2(110f, 50f), "← Back to List");

        // ── Wire PassengerListPanel ────────────────────────────────────────
        PassengerListPanel plp = listRoot.AddComponent<PassengerListPanel>();
        SerializedObject plpSO = new SerializedObject(plp);
        plpSO.FindProperty("listScreen").objectReferenceValue       = listScreen;
        plpSO.FindProperty("headerText").objectReferenceValue       = headerText;
        plpSO.FindProperty("entryContainer").objectReferenceValue   = entryContainer.transform;
        plpSO.FindProperty("prevButton").objectReferenceValue       = prevBtn;
        plpSO.FindProperty("nextButton").objectReferenceValue       = nextBtn;
        plpSO.FindProperty("pageLabel").objectReferenceValue        = pageLabel;
        plpSO.FindProperty("listBackButton").objectReferenceValue   = listBackBtn;
        plpSO.FindProperty("detailScreen").objectReferenceValue     = detailScreen;
        plpSO.FindProperty("detailNameText").objectReferenceValue   = dName;
        plpSO.FindProperty("detailAgeText").objectReferenceValue    = dAge;
        plpSO.FindProperty("detailJobText").objectReferenceValue    = dJob;
        plpSO.FindProperty("detailHobbiesText").objectReferenceValue = dHobby;
        plpSO.FindProperty("detailTimesText").objectReferenceValue  = dTimes;
        plpSO.FindProperty("detailBackButton").objectReferenceValue = detailBackBtn;
        plpSO.ApplyModifiedProperties();

        // ── Settings Panel ─────────────────────────────────────────────────
        GameObject settingsRoot = new GameObject("SettingsPanel");
        settingsRoot.transform.SetParent(canvasGO.transform, false);
        settingsRoot.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.09f, 0.97f);
        var settingsRect = settingsRoot.GetComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(0.5f, 0.5f); settingsRect.anchorMax = new Vector2(0.5f, 0.5f);
        settingsRect.pivot     = new Vector2(0.5f, 0.5f);
        settingsRect.sizeDelta = new Vector2(400f, 320f);

        TMP_Text settingsHeader = CreateLabel(settingsRoot.transform, "HeaderText",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16f, -50f), new Vector2(-16f, -10f),
            "SETTINGS", 22);
        settingsHeader.color = new Color(0.5f, 0.75f, 1f);

        GameObject sdiv = new GameObject("Divider");
        sdiv.transform.SetParent(settingsRoot.transform, false);
        sdiv.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.1f);
        var sdivRect = sdiv.GetComponent<RectTransform>();
        sdivRect.anchorMin = new Vector2(0f, 1f); sdivRect.anchorMax = new Vector2(1f, 1f);
        sdivRect.pivot = new Vector2(0.5f, 1f);
        sdivRect.anchoredPosition = new Vector2(0f, -54f);
        sdivRect.sizeDelta = new Vector2(-32f, 1f);

        // Row 1 — Master Volume
        CreateLabel(settingsRoot.transform, "MasterVolumeLabel",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(16f, -100f), new Vector2(0f, -68f), "Master Volume", 16);
        Slider masterSlider = CreateSlider(settingsRoot.transform, "MasterVolumeSlider",
            new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -100f), new Vector2(-16f, -68f));

        // Row 2 — Music Volume
        CreateLabel(settingsRoot.transform, "MusicVolumeLabel",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(16f, -155f), new Vector2(0f, -123f), "Music Volume", 16);
        Slider musicSlider = CreateSlider(settingsRoot.transform, "MusicVolumeSlider",
            new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -155f), new Vector2(-16f, -123f));

        // Row 3 — SFX Volume
        CreateLabel(settingsRoot.transform, "SfxVolumeLabel",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(16f, -210f), new Vector2(0f, -178f), "SFX Volume", 16);
        Slider sfxSlider = CreateSlider(settingsRoot.transform, "SfxVolumeSlider",
            new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -210f), new Vector2(-16f, -178f));

        // Row 4 — Fullscreen
        CreateLabel(settingsRoot.transform, "FullscreenLabel",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(16f, -265f), new Vector2(0f, -233f), "Fullscreen", 16);
        Toggle fsToggle = CreateToggle(settingsRoot.transform, "FullscreenToggle",
            new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -265f), new Vector2(-16f, -233f));

        UnityEngine.UI.Button settingsBackBtn = CreateButton(settingsRoot.transform, "BackButton",
            new Vector2(0.5f, 0f), new Vector2(-110f, 10f), new Vector2(110f, 50f), "← Back");

        SettingsPanel sp = settingsRoot.AddComponent<SettingsPanel>();
        SerializedObject spSO = new SerializedObject(sp);
        spSO.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
        spSO.FindProperty("musicVolumeSlider").objectReferenceValue  = musicSlider;
        spSO.FindProperty("sfxVolumeSlider").objectReferenceValue    = sfxSlider;
        spSO.FindProperty("fullscreenToggle").objectReferenceValue   = fsToggle;
        spSO.FindProperty("backButton").objectReferenceValue         = settingsBackBtn;
        spSO.ApplyModifiedProperties();

        // ── Wire PauseMenuUI ───────────────────────────────────────────────
        PauseMenuUI pauseUI = canvasGO.AddComponent<PauseMenuUI>();
        SerializedObject pso = new SerializedObject(pauseUI);
        pso.FindProperty("canvasGroup").objectReferenceValue        = cg;
        pso.FindProperty("mainPanel").objectReferenceValue          = mainPanel;
        pso.FindProperty("resumeButton").objectReferenceValue       = resumeBtn;
        pso.FindProperty("passengersButton").objectReferenceValue   = passengersBtn;
        pso.FindProperty("settingsButton").objectReferenceValue     = settingsBtn;
        pso.FindProperty("mainMenuButton").objectReferenceValue     = mainMenuBtn;
        pso.FindProperty("passengerListPanel").objectReferenceValue = plp;
        pso.FindProperty("settingsPanel").objectReferenceValue      = sp;
        pso.ApplyModifiedProperties();

        Debug.Log("[Mousebus] PauseMenu created.");
    }

    // ── Level Complete Scene UI ───────────────────────────────────────────

    // Run this with the LevelComplete scene open to stamp in the completion screen.
    [MenuItem("Mousebus/Setup Level Complete Scene")]
    public static void SetupLevelCompleteScene()
    {
        if (Object.FindFirstObjectByType<LevelCompleteUI>() != null)
        {
            Debug.Log("[Mousebus] LevelCompleteUI already exists in this scene.");
            return;
        }

        // Canvas
        GameObject canvasGO = new GameObject("LevelCompleteUI");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        CanvasGroup cg = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Black background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        bgGO.AddComponent<UnityEngine.UI.Image>().color = Color.black;
        StretchToFill(bgGO.GetComponent<RectTransform>());

        // "LEVEL COMPLETE" title — slides in from above
        GameObject titleGO = new GameObject("TitleCard");
        titleGO.transform.SetParent(canvasGO.transform, false);
        RectTransform titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 80f);
        titleRect.sizeDelta        = new Vector2(600f, 80f);
        TMP_Text titleText = titleGO.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.text      = "LEVEL COMPLETE";
        titleText.fontSize  = 52;
        titleText.color     = Color.white;
        titleText.alignment = TMPro.TextAlignmentOptions.Center;

        // Level name subtitle
        TMP_Text levelNameText = CreateLabel(canvasGO.transform, "LevelNameText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-200f, 20f), new Vector2(200f, 65f),
            "", 32);

        // Grade label — "● GREEN  87%" coloured by result
        TMP_Text gradeText = CreateLabel(canvasGO.transform, "GradeText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-200f, -15f), new Vector2(200f, 15f),
            "", 30);
        gradeText.alignment = TMPro.TextAlignmentOptions.Center;

        // Buttons (shifted down slightly to make room for grade label)
        UnityEngine.UI.Button continueBtn = CreateButton(canvasGO.transform, "ContinueButton",
            new Vector2(0.5f, 0.5f), new Vector2(-150f, -75f), new Vector2(150f, -25f), "Continue");
        UnityEngine.UI.Button mainMenuBtn = CreateButton(canvasGO.transform, "MainMenuButton",
            new Vector2(0.5f, 0.5f), new Vector2(-150f, -135f), new Vector2(150f, -85f), "Main Menu");
        UnityEngine.UI.Button statsBtn = CreateButton(canvasGO.transform, "StatsButton",
            new Vector2(0.5f, 0.5f), new Vector2(-150f, -195f), new Vector2(150f, -145f), "Stats");

        // Stats breakdown — hidden by default, toggled by the Stats button
        TMP_Text statsText = CreateLabel(canvasGO.transform, "StatsText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-260f, -390f), new Vector2(260f, -210f),
            "", 22);
        statsText.alignment = TMPro.TextAlignmentOptions.Left;

        // ── Passengers Panel ──────────────────────────────────────────────
        var (lcPassPanel, lcPassComp) = CreateLCPassengerPanel(canvasGO.transform);

        // Passengers button — sits below Stats
        UnityEngine.UI.Button passengersBtn = CreateButton(canvasGO.transform, "PassengersButton",
            new Vector2(0.5f, 0.5f), new Vector2(-150f, -255f), new Vector2(150f, -205f), "Who Was On Board?");

        // Wire LevelCompleteUI
        LevelCompleteUI lcUI = canvasGO.AddComponent<LevelCompleteUI>();
        SerializedObject so = new SerializedObject(lcUI);
        so.FindProperty("levelNameText").objectReferenceValue  = levelNameText;
        so.FindProperty("gradeText").objectReferenceValue      = gradeText;
        so.FindProperty("statsText").objectReferenceValue      = statsText;
        so.FindProperty("canvasGroup").objectReferenceValue    = cg;
        so.FindProperty("titleRect").objectReferenceValue      = titleRect;
        so.FindProperty("continueButton").objectReferenceValue = continueBtn;
        so.FindProperty("mainMenuButton").objectReferenceValue = mainMenuBtn;
        so.FindProperty("statsButton").objectReferenceValue    = statsBtn;
        so.FindProperty("passengersButton").objectReferenceValue = passengersBtn;
        so.FindProperty("passengerPanel").objectReferenceValue   = lcPassComp;
        so.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(canvasGO);
        Debug.Log("[Mousebus] LevelComplete UI created. Open LevelComplete scene first.");
    }

    [MenuItem("Mousebus/Update Level Complete Scene (Add Passengers Button)")]
    public static void UpdateLevelCompletePassengers()
    {
        LevelCompleteUI lcUI = Object.FindFirstObjectByType<LevelCompleteUI>();
        if (lcUI == null)
        {
            EditorUtility.DisplayDialog("Scene Not Found",
                "Open the LevelComplete scene first, then run this.", "OK");
            return;
        }

        SerializedObject so = new SerializedObject(lcUI);

        if (so.FindProperty("passengerPanel").objectReferenceValue != null)
        {
            EditorUtility.DisplayDialog("Already Done",
                "Passenger panel already wired on this LevelCompleteUI.", "OK");
            return;
        }

        Canvas canvas = lcUI.GetComponentInParent<Canvas>();
        Transform root = canvas != null ? canvas.transform : lcUI.transform;

        var (_, lcPassComp) = CreateLCPassengerPanel(root);

        UnityEngine.UI.Button passengersBtn = CreateButton(root, "PassengersButton",
            new Vector2(0.5f, 0.5f), new Vector2(-150f, -255f), new Vector2(150f, -205f), "Who Was On Board?");

        so.FindProperty("passengersButton").objectReferenceValue = passengersBtn;
        so.FindProperty("passengerPanel").objectReferenceValue   = lcPassComp;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(lcUI);
        EditorSceneManager.MarkSceneDirty(lcUI.gameObject.scene);

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Done",
            "\"Who Was On Board?\" button and passenger panel added to LevelComplete.\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static (GameObject root, LevelCompletePassengerPanel comp) CreateLCPassengerPanel(Transform canvasParent)
    {
        GameObject panelRoot = new GameObject("LCPassengerPanel");
        panelRoot.transform.SetParent(canvasParent, false);
        panelRoot.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.09f, 0.97f);
        var panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot     = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(480f, 440f);

        // Header
        TMP_Text header = CreateLabel(panelRoot.transform, "HeaderText",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16f, -50f), new Vector2(-16f, -10f),
            "ON BOARD TODAY  (0)", 22);
        header.color = new Color(0.5f, 0.75f, 1f);

        // Divider
        GameObject div = new GameObject("Divider");
        div.transform.SetParent(panelRoot.transform, false);
        div.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.1f);
        var divRect = div.GetComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0f, 1f); divRect.anchorMax = new Vector2(1f, 1f);
        divRect.pivot = new Vector2(0.5f, 1f);
        divRect.anchoredPosition = new Vector2(0f, -54f);
        divRect.sizeDelta = new Vector2(-32f, 1f);

        // Entry container
        GameObject entries = new GameObject("EntryContainer");
        entries.transform.SetParent(panelRoot.transform, false);
        var eImg = entries.AddComponent<UnityEngine.UI.Image>();
        eImg.color = Color.clear; eImg.raycastTarget = false;
        var eRect = entries.GetComponent<RectTransform>();
        eRect.anchorMin = new Vector2(0f, 0f); eRect.anchorMax = new Vector2(1f, 1f);
        eRect.offsetMin = new Vector2(0f, 60f); eRect.offsetMax = new Vector2(0f, -58f);

        // Pagination row
        GameObject pageRow = new GameObject("PaginationRow");
        pageRow.transform.SetParent(panelRoot.transform, false);
        var prImg = pageRow.AddComponent<UnityEngine.UI.Image>();
        prImg.color = Color.clear; prImg.raycastTarget = false;
        var prRect = pageRow.GetComponent<RectTransform>();
        prRect.anchorMin = new Vector2(0f, 0f); prRect.anchorMax = new Vector2(1f, 0f);
        prRect.pivot = new Vector2(0.5f, 0f);
        prRect.anchoredPosition = new Vector2(0f, 58f);
        prRect.sizeDelta = new Vector2(0f, 40f);

        var prevBtn  = CreateButton(pageRow.transform, "PrevButton",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 40f), "←");
        var nextBtn  = CreateButton(pageRow.transform, "NextButton",
            new Vector2(1f, 0f), new Vector2(-48f, 0f), new Vector2(0f, 40f), "→");
        TMP_Text pageLabel = CreateLabel(pageRow.transform, "PageLabel",
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(52f, 0f), new Vector2(-52f, 0f), "1 / 1", 17);
        pageLabel.alignment = TMPro.TextAlignmentOptions.Center;

        var backBtn = CreateButton(panelRoot.transform, "BackButton",
            new Vector2(0.5f, 0f), new Vector2(-110f, 10f), new Vector2(110f, 50f), "← Back");

        LevelCompletePassengerPanel comp = panelRoot.AddComponent<LevelCompletePassengerPanel>();
        SerializedObject pso = new SerializedObject(comp);
        pso.FindProperty("headerText").objectReferenceValue    = header;
        pso.FindProperty("entryContainer").objectReferenceValue = entries.transform;
        pso.FindProperty("prevButton").objectReferenceValue    = prevBtn;
        pso.FindProperty("nextButton").objectReferenceValue    = nextBtn;
        pso.FindProperty("pageLabel").objectReferenceValue     = pageLabel;
        pso.FindProperty("backButton").objectReferenceValue    = backBtn;
        pso.ApplyModifiedProperties();

        return (panelRoot, comp);
    }

    // ── Main Menu UI ──────────────────────────────────────────────────────

    // Run this with the MainMenu scene open.
    // Creates a Splash canvas (add your logo images to Logo1 / Logo2 afterward)
    // and a Main Menu canvas with all buttons pre-wired.
    [MenuItem("Mousebus/Setup Main Menu UI")]
    public static void SetupMainMenuUI()
    {
        if (Object.FindFirstObjectByType<MainMenuUI>() != null)
        {
            Debug.Log("[Mousebus] MainMenuUI already exists in this scene.");
            return;
        }

        // ── Splash Canvas (sort order 90 — below loading screen at 100) ──
        GameObject splashCanvasGO = new GameObject("SplashCanvas");
        Canvas splashCanvas = splashCanvasGO.AddComponent<Canvas>();
        splashCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        splashCanvas.sortingOrder = 90;
        splashCanvasGO.AddComponent<GraphicRaycaster>();

        // Full-screen black overlay that SplashSequence fades out at the end
        GameObject overlayGO = new GameObject("SplashOverlay");
        overlayGO.transform.SetParent(splashCanvasGO.transform, false);
        overlayGO.AddComponent<Image>().color = Color.black;
        StretchToFill(overlayGO.GetComponent<RectTransform>());
        CanvasGroup overlayCG = overlayGO.AddComponent<CanvasGroup>();
        overlayCG.interactable   = false;
        overlayCG.blocksRaycasts = true;

        // Two logo slots — assign your studio and publisher logo Images here
        CanvasGroup logo1CG = CreateLogoSlot(splashCanvasGO.transform, "Logo1_Studio");
        CanvasGroup logo2CG = CreateLogoSlot(splashCanvasGO.transform, "Logo2_Publisher");

        // Wire SplashSequence
        SplashSequence splash = splashCanvasGO.AddComponent<SplashSequence>();
        SerializedObject splashSO = new SerializedObject(splash);
        splashSO.FindProperty("splashOverlay").objectReferenceValue = overlayCG;

        // logos is a SerializedProperty array
        SerializedProperty logosProp = splashSO.FindProperty("logos");
        logosProp.arraySize = 2;

        SerializedProperty l0 = logosProp.GetArrayElementAtIndex(0);
        l0.FindPropertyRelative("group").objectReferenceValue = logo1CG;
        l0.FindPropertyRelative("fadeDuration").floatValue    = 0.5f;
        l0.FindPropertyRelative("holdDuration").floatValue    = 2f;

        SerializedProperty l1 = logosProp.GetArrayElementAtIndex(1);
        l1.FindPropertyRelative("group").objectReferenceValue = logo2CG;
        l1.FindPropertyRelative("fadeDuration").floatValue    = 0.5f;
        l1.FindPropertyRelative("holdDuration").floatValue    = 2f;

        splashSO.ApplyModifiedProperties();

        // ── Main Menu Canvas (sort order 5) ──────────────────────────────
        GameObject menuCanvasGO = new GameObject("MainMenuCanvas");
        Canvas menuCanvas = menuCanvasGO.AddComponent<Canvas>();
        menuCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 5;
        menuCanvasGO.AddComponent<GraphicRaycaster>();

        // Translucent main panel — centred, lower-left quarter of the screen
        GameObject panelGO = new GameObject("MainPanel");
        panelGO.transform.SetParent(menuCanvasGO.transform, false);
        CanvasGroup panelCG = panelGO.AddComponent<CanvasGroup>();
        panelGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible background
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.05f, 0.2f);
        panelRect.anchorMax        = new Vector2(0.4f,  0.8f);
        panelRect.offsetMin        = Vector2.zero;
        panelRect.offsetMax        = Vector2.zero;

        // Main menu buttons
        Button newGameBtn    = CreateButton(panelGO.transform, "NewGameButton",
            new Vector2(0f, 1f), new Vector2(10f, -70f),  new Vector2(250f, -20f),  "New Game");
        Button continueBtn   = CreateButton(panelGO.transform, "ContinueButton",
            new Vector2(0f, 1f), new Vector2(10f, -130f), new Vector2(250f, -80f),  "Continue");
        Button levelSelBtn   = CreateButton(panelGO.transform, "LevelSelectButton",
            new Vector2(0f, 1f), new Vector2(10f, -190f), new Vector2(250f, -140f), "Level Select");
        Button quitBtn       = CreateButton(panelGO.transform, "QuitButton",
            new Vector2(0f, 1f), new Vector2(10f, -250f), new Vector2(250f, -200f), "Quit");

        // ── Level Select slide panel ──────────────────────────────────────
        // Starts off-screen to the right; MainMenuUI slides it in when Level Select is clicked.
        GameObject lsPanelGO = new GameObject("LevelSelectPanel");
        lsPanelGO.transform.SetParent(menuCanvasGO.transform, false);
        Image lsBg = lsPanelGO.AddComponent<Image>();
        lsBg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
        RectTransform lsRect = lsPanelGO.GetComponent<RectTransform>();
        lsRect.anchorMin        = new Vector2(0.6f, 0.1f);
        lsRect.anchorMax        = new Vector2(0.95f, 0.9f);
        lsRect.offsetMin        = Vector2.zero;
        lsRect.offsetMax        = Vector2.zero;

        Button lsCloseBtn = CreateButton(lsPanelGO.transform, "CloseButton",
            new Vector2(1f, 1f), new Vector2(-55f, -45f), new Vector2(-5f, -5f), "✕");

        // Container for generated level buttons (MainMenuUI populates at runtime)
        GameObject containerGO = new GameObject("LevelButtonContainer");
        containerGO.transform.SetParent(lsPanelGO.transform, false);
        RectTransform containerRect = containerGO.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.offsetMin = new Vector2(10f,  50f);
        containerRect.offsetMax = new Vector2(-10f, -10f);

        // Placeholder level button prefab (a simple button — replace with a real prefab later)
        // We leave levelButtonPrefab null here; the user can assign a prefab in the Inspector,
        // or the level-select list simply won't populate until one is assigned.

        // ── Wire MainMenuUI ───────────────────────────────────────────────
        MainMenuUI menuUI = menuCanvasGO.AddComponent<MainMenuUI>();
        SerializedObject menuSO = new SerializedObject(menuUI);
        menuSO.FindProperty("mainPanelGroup").objectReferenceValue      = panelCG;
        menuSO.FindProperty("newGameButton").objectReferenceValue        = newGameBtn;
        menuSO.FindProperty("continueButton").objectReferenceValue       = continueBtn;
        menuSO.FindProperty("levelSelectButton").objectReferenceValue    = levelSelBtn;
        menuSO.FindProperty("quitButton").objectReferenceValue           = quitBtn;
        menuSO.FindProperty("levelSelectPanel").objectReferenceValue     = lsRect;
        menuSO.FindProperty("levelSelectCloseButton").objectReferenceValue = lsCloseBtn;
        menuSO.FindProperty("levelButtonContainer").objectReferenceValue = containerGO.transform;
        // levelButtonPrefab left null — assign a Button prefab in the Inspector
        menuSO.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
        Debug.Log("[Mousebus] Main Menu UI created. " +
                  "Assign logo Images to SplashCanvas/Logo1_Studio and Logo2_Publisher. " +
                  "Assign a Button prefab to MainMenuUI → Level Button Prefab for the level list.");
    }

    private static CanvasGroup CreateLogoSlot(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        // Centred image — swap to your logo sprite in the Inspector
        Image img = go.AddComponent<Image>();
        img.color          = Color.white;
        img.preserveAspect = true;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.25f, 0.3f);
        rect.anchorMax = new Vector2(0.75f, 0.7f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CanvasGroup cg = go.AddComponent<CanvasGroup>();
        cg.alpha          = 0f; // hidden until SplashSequence fades it in
        cg.interactable   = false;
        cg.blocksRaycasts = false;
        return cg;
    }

    // ── Shared UI Helpers ─────────────────────────────────────────────────

    // Creates a TMP_Text label and returns it. anchorMin/Max are the pivot anchors;
    // offsetMin/Max are the pixel offsets from those anchors.
    private static TMP_Text CreateLabel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        string text, float fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TMP_Text tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.MidlineLeft;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        return tmp;
    }

    // Creates a UI Button with a TMP_Text label child.
    private static UnityEngine.UI.Button CreateButton(Transform parent, string name,
        Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax, string label)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        UnityEngine.UI.Image img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(1f, 1f, 1f, 0.15f); // subtle tinted background

        UnityEngine.UI.Button btn = go.AddComponent<UnityEngine.UI.Button>();

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = pivot;
        rect.anchorMax = pivot;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        // Label child
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        TMP_Text tmp = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 24;
        tmp.color     = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        StretchToFill(labelGO.GetComponent<RectTransform>());

        return btn;
    }

    // Creates a UI Slider with background, fill, and handle — returns the Slider component.
    // anchorMin/Max define which corners of the parent it's attached to.
    private static Slider CreateSlider(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        float defaultValue = 1f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<UnityEngine.UI.Image>().color = Color.clear;
        var slider = go.AddComponent<Slider>();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;

        // Track background
        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        bg.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.1f);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.3f); bgRect.anchorMax = new Vector2(1f, 0.7f);
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        // Fill area
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.3f); fillAreaRect.anchorMax = new Vector2(1f, 0.7f);
        fillAreaRect.offsetMin = new Vector2(5f, 0f); fillAreaRect.offsetMax = new Vector2(-15f, 0f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        fill.AddComponent<UnityEngine.UI.Image>().color = new Color(0.5f, 0.8f, 1f, 0.9f);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;

        // Handle slide area
        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(go.transform, false);
        var handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero; handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f); handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleImg = handle.AddComponent<UnityEngine.UI.Image>();
        handleImg.color = Color.white;
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0f); handleRect.anchorMax = new Vector2(0f, 1f);
        handleRect.offsetMin = new Vector2(-10f, 0f); handleRect.offsetMax = new Vector2(10f, 0f);

        slider.fillRect      = fillRect;
        slider.handleRect    = handleRect;
        slider.targetGraphic = handleImg;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue      = 0f;
        slider.maxValue      = 1f;
        slider.value         = defaultValue;

        return slider;
    }

    // Creates a UI Toggle (checkbox) — returns the Toggle component.
    private static Toggle CreateToggle(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<UnityEngine.UI.Image>().color = Color.clear;
        var toggle = go.AddComponent<Toggle>();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;

        // Checkbox box — fixed size, anchored to left-centre of the control area
        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.15f);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.5f); bgRect.anchorMax = new Vector2(0f, 0.5f);
        bgRect.pivot     = new Vector2(0f, 0.5f);
        bgRect.anchoredPosition = new Vector2(8f, 0f);
        bgRect.sizeDelta        = new Vector2(28f, 28f);

        // Checkmark — visible only when isOn = true
        var check = new GameObject("Checkmark");
        check.transform.SetParent(bg.transform, false);
        var checkImg = check.AddComponent<UnityEngine.UI.Image>();
        checkImg.color = new Color(0.5f, 0.8f, 1f);
        var checkRect = check.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.15f, 0.15f); checkRect.anchorMax = new Vector2(0.85f, 0.85f);
        checkRect.offsetMin = Vector2.zero; checkRect.offsetMax = Vector2.zero;

        toggle.targetGraphic = bgImg;
        toggle.graphic       = checkImg;
        toggle.isOn          = Screen.fullScreen;

        return toggle;
    }

    // ── Transparent Bus Material ──────────────────────────────────────────

    // Makes the bus semi-transparent so you can see passengers sitting inside.
    // Dev-only aid — swap for your final bus model/material when art is ready.
    [MenuItem("Mousebus/Make Bus Transparent")]
    public static void MakeBusTransparent()
    {
        var bus = Object.FindFirstObjectByType<BusController>();
        if (bus == null)
        {
            EditorUtility.DisplayDialog("No Bus", "No BusController found in the open scene.", "OK");
            return;
        }
        var mr = bus.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            EditorUtility.DisplayDialog("No Renderer", "BusController has no MeshRenderer on the same GameObject.", "OK");
            return;
        }

        EnsureFolder("Assets/_Mousebus/Art");
        EnsureFolder("Assets/_Mousebus/Art/Materials");
        string matPath = "Assets/_Mousebus/Art/Materials/Bus_Transparent.mat";

        Material mat;
        if (File.Exists(matPath))
        {
            mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");
            mat      = new Material(shader) { name = "Bus_Transparent" };

            // URP Lit transparent setup
            mat.SetFloat("_Surface", 1f);        // 1 = Transparent
            mat.SetFloat("_Blend",   0f);        // Alpha blend
            mat.SetInt("_SrcBlend",  5);         // SrcAlpha
            mat.SetInt("_DstBlend",  10);        // OneMinusSrcAlpha
            mat.SetInt("_ZWrite",    0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }

        mat.SetColor("_BaseColor", new Color(0.95f, 0.15f, 0.15f, 0.28f));
        EditorUtility.SetDirty(mat);

        Undo.RecordObject(mr, "Make Bus Transparent");
        mr.sharedMaterial = mat;
        EditorUtility.SetDirty(mr);

        Debug.Log("[Mousebus] Transparent material applied to bus.");
        EditorUtility.DisplayDialog("Done",
            "Transparent blue material applied.\n\nIf the bus still looks opaque, select the material in " +
            "Assets/_Mousebus/Art/Materials/ and confirm Surface Type = Transparent in the Inspector.", "OK");
    }

    // ── Time Of Day ───────────────────────────────────────────────────────

    [MenuItem("Mousebus/Add Time Of Day")]
    private static void AddTimeOfDay()
    {
        if (Object.FindFirstObjectByType<TimeOfDayController>() != null)
        {
            EditorUtility.DisplayDialog("Already exists",
                "A TimeOfDayController is already in this scene.", "OK");
            return;
        }

        var go = new GameObject("TimeOfDay");
        Undo.RegisterCreatedObjectUndo(go, "Add Time Of Day");
        var controller = go.AddComponent<TimeOfDayController>();

        // Auto-wire the scene's directional light
        Light sun = Object.FindFirstObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("sun").objectReferenceValue = sun;
            so.ApplyModifiedProperties();
        }

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Done",
            "TimeOfDay added.\n\n" +
            "• Drag the Preview slider in the Inspector to scrub through the day without entering Play mode.\n" +
            "• Set realSecondsPerGameHour low (e.g. 6) for fast testing.\n" +
            "• Save the scene now (Ctrl+S).", "OK");
    }

    // ── Sky and Depth of Field ────────────────────────────────────────────

    [MenuItem("Mousebus/Setup Sky and DoF")]
    private static void SetupSkyAndDoF()
    {
        EnsureFolder("Assets/_Mousebus/Art/Materials");
        EnsureFolder("Assets/_Mousebus/Data");

        // ── Skybox material ───────────────────────────────────────────────
        const string matPath = "Assets/_Mousebus/Art/Materials/Skybox_Procedural.mat";
        Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (skyMat == null)
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Shader missing",
                    "Could not find Skybox/Procedural. Make sure it is included in the project.", "OK");
                return;
            }
            skyMat = new Material(shader) { name = "Skybox_Procedural" };
            skyMat.SetFloat("_SunDisk", 2f);      // high quality sun disc
            skyMat.SetFloat("_SunSize", 0.04f);
            skyMat.SetFloat("_AtmosphereThickness", 1.0f);
            skyMat.SetColor("_SkyTint",    new Color(0.52f, 0.68f, 0.98f));
            skyMat.SetColor("_GroundColor", new Color(0.45f, 0.44f, 0.40f));
            skyMat.SetFloat("_Exposure", 1.3f);
            AssetDatabase.CreateAsset(skyMat, matPath);
            AssetDatabase.SaveAssets();
        }
        RenderSettings.skybox = skyMat;

        // Wire skybox into TimeOfDayController if one exists
        var tod = Object.FindFirstObjectByType<TimeOfDayController>();
        if (tod != null)
        {
            var so = new SerializedObject(tod);
            so.FindProperty("skyboxMaterial").objectReferenceValue = skyMat;
            so.ApplyModifiedProperties();
        }

        // ── Post-process Volume with Depth of Field ───────────────────────
        const string profilePath = "Assets/_Mousebus/Data/PP_LevelVolume.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        // Gaussian DoF — lightweight, perfect for Switch
        if (!profile.TryGet<DepthOfField>(out _))
        {
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(35f);    // blur starts ~3 blocks out
            dof.gaussianEnd.Override(90f);      // fully soft before fog takes over
            dof.gaussianMaxRadius.Override(1.5f);
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // Find or create the Global Volume in the scene
        Volume vol = Object.FindFirstObjectByType<Volume>();
        if (vol == null)
        {
            var volGO = new GameObject("PP_Volume");
            Undo.RegisterCreatedObjectUndo(volGO, "Create PP Volume");
            vol = volGO.AddComponent<Volume>();
        }
        vol.isGlobal = true;
        vol.priority = 1f;
        vol.sharedProfile = profile;
        EditorUtility.SetDirty(vol);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Done",
            "Skybox and Depth of Field set up.\n\n" +
            "Skybox: Skybox/Procedural material assigned. TimeOfDayController will drive its tint.\n\n" +
            "DoF: Gaussian blur starts at 35 units, fully blurred at 90. Adjust in:\n" +
            "  Assets/_Mousebus/Data/PP_LevelVolume.asset\n\n" +
            "Vancouver sky photo: when ready, create a Skybox/Panoramic material,\n" +
            "assign your 360° equirectangular image, and set it as the scene skybox.\n\n" +
            "Save the scene now (Ctrl+S).", "OK");
    }

    // ── Level Scene Audit ─────────────────────────────────────────────────

    // Run with any level scene open — logs a pass/fail for every required component.
    // Fix each ✗ before testing the full gameplay loop.
    [MenuItem("Mousebus/Audit Level Scene")]
    public static void AuditLevelScene()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Level Scene Audit ===\n");

        Check(sb, Object.FindFirstObjectByType<LevelManager>()    != null, "LevelManager",
              "Add an empty GameObject named 'LevelManager', attach LevelManager script, and wire Inspector fields.");
        Check(sb, Object.FindFirstObjectByType<CutscenePlayer>()  != null, "CutscenePlayer",
              "Add the CutscenePlayer prefab to the scene (drag from Prefabs folder).");
        Check(sb, Object.FindFirstObjectByType<BusController>()   != null, "BusController",
              "Run Mousebus → Create Bus (Test Cube).");
        Check(sb, Object.FindFirstObjectByType<HUD>()             != null, "HUD",
              "Run Mousebus → Setup Level Scene UI.");
        Check(sb, Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null, "EventSystem",
              "Right-click Hierarchy → UI → Event System.");

        bool hasHalfway = false, hasEnd = false;
        foreach (var t in Object.FindObjectsByType<LevelTrigger>(FindObjectsSortMode.None))
        {
            if (t.triggerId == "halfway") hasHalfway = true;
            if (t.triggerId == "end")     hasEnd     = true;
        }
        Check(sb, hasHalfway, "TRG_Halfway trigger", "Run Mousebus → Setup Level Triggers.");
        Check(sb, hasEnd,     "TRG_End trigger",     "Run Mousebus → Setup Level Triggers.");

        int stopCount = Object.FindObjectsByType<BusStop>(FindObjectsSortMode.None).Length;
        Check(sb, stopCount > 0, $"Bus stops ({stopCount} found)",
              "Run Mousebus → Create Bus Stop for each stop on your route.");

        var lm = Object.FindFirstObjectByType<LevelManager>();
        if (lm != null)
        {
            var so   = new SerializedObject(lm);
            bool hasCutscenePlayer = so.FindProperty("cutscenePlayer").objectReferenceValue != null;
            bool hasScoreConfig    = so.FindProperty("scoreConfig").objectReferenceValue    != null;
            Check(sb, hasCutscenePlayer, "LevelManager → CutscenePlayer wired",
                  "Drag the CutscenePlayer into the CutscenePlayer slot on LevelManager.");
            Check(sb, hasScoreConfig, "LevelManager → Score Config wired",
                  "Run Mousebus → Create Level Score Config (Tutorial), then drag it into the Score Config slot.");
        }

        string report = sb.ToString();
        Debug.Log("[Mousebus Audit]\n" + report);
        EditorUtility.DisplayDialog("Level Scene Audit", report, "OK");
    }

    private static void Check(System.Text.StringBuilder sb, bool pass, string label, string fix)
    {
        if (pass)
            sb.AppendLine($"✓  {label}");
        else
            sb.AppendLine($"✗  {label} MISSING\n   Fix: {fix}\n");
    }

    // ── Placeholder Cutscenes ─────────────────────────────────────────────

    // Creates three minimal CutsceneData assets for the Tutorial level so the
    // full phase loop (Intro → Drive → Midpoint → Drive back → Outro → Complete)
    // can be tested immediately. Replace the subtitle text and add artwork later.
    [MenuItem("Mousebus/Create Placeholder Cutscenes (Tutorial)")]
    public static void CreatePlaceholderCutscenes()
    {
        string folder = "Assets/_Mousebus/Data/Cutscenes/Tutorial";
        EnsureFolder("Assets/_Mousebus/Data/Cutscenes");
        EnsureFolder(folder);

        MakeCutscene(folder, "Tutorial_Intro",
            "Day 1. You're behind the wheel for the first time.\nPick up your passengers and get them home.");

        MakeCutscene(folder, "Tutorial_Midpoint",
            "Halfway there. Time to turn around and head back.");

        MakeCutscene(folder, "Tutorial_Outro",
            "Route complete. Not bad for a first run.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Mousebus] Tutorial cutscene assets created in " + folder);
        EditorUtility.DisplayDialog("Done",
            "3 placeholder cutscene assets created in:\n" + folder +
            "\n\nDrag Tutorial_Intro, Tutorial_Midpoint, and Tutorial_Outro into the matching slots on the LevelManager in Level_Tutorial.", "OK");
    }

    private static void MakeCutscene(string folder, string name, string subtitle)
    {
        string path = folder + "/" + name + ".asset";
        if (File.Exists(path)) { Debug.Log("[Mousebus] " + name + " already exists, skipping."); return; }

        var data   = ScriptableObject.CreateInstance<CutsceneData>();
        var slide  = new CutsceneSlide { duration = 4f };
        slide.subtitles = new[] { new SubtitleLine { text = subtitle, showAtTime = 0.5f } };
        data.slides = new[] { slide };

        AssetDatabase.CreateAsset(data, path);
        Debug.Log("[Mousebus] Created " + path);
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }

    // ── Level Score Config ────────────────────────────────────────────────

    [MenuItem("Mousebus/Create Level Score Config (Tutorial)")]
    public static void CreateTutorialScoreConfig()
    {
        string folder = "Assets/_Mousebus/Data/ScoreConfigs";
        EnsureFolder("Assets/_Mousebus/Data");
        EnsureFolder(folder);

        string path = folder + "/Tutorial_ScoreConfig.asset";
        if (File.Exists(path))
        {
            EditorUtility.DisplayDialog("Already Exists", path + " already exists.", "OK");
            return;
        }

        var cfg = ScriptableObject.CreateInstance<LevelScoreConfig>();
        cfg.stats.Add(new LevelScoreConfig.StatConfig
        {
            statId      = "passengers",
            displayName = "Passengers Picked Up",
            weight      = 1f,
        });

        AssetDatabase.CreateAsset(cfg, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = cfg;
        EditorGUIUtility.PingObject(cfg);

        Debug.Log("[Mousebus] Tutorial_ScoreConfig created at " + path);
        EditorUtility.DisplayDialog("Done",
            "Tutorial_ScoreConfig created.\n\nDrag it into the Score Config slot on the LevelManager in Level_Tutorial.\n\nTo add more stats later, add entries to the Stats list in the Inspector.", "OK");
    }

    // ── Level Lighting ────────────────────────────────────────────────────

    // Sets up a warm directional sun, a gradient ambient, and a light distance fog.
    // Gives the scene a natural outdoor feel without requiring any extra assets.
    // Run this with the level scene open — safe to re-run, adjusts existing lights.
    [MenuItem("Mousebus/Setup Level Lighting")]
    public static void SetupLevelLighting()
    {
        // Find or create the scene's directional light
        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }

        if (sun == null)
        {
            var go = new GameObject("Directional Light");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            Undo.RegisterCreatedObjectUndo(go, "Create Directional Light");
        }

        // Warm afternoon sun — angle gives long soft shadows without flattening the scene
        sun.color        = new Color(1.00f, 0.93f, 0.78f);
        sun.intensity    = 1.1f;
        sun.shadows      = LightShadows.Soft;
        sun.shadowStrength = 0.65f;
        sun.transform.rotation = Quaternion.Euler(48f, 30f, 0f);
        Undo.RecordObject(sun, "Setup Sun");

        // Trilight ambient — blue sky above, neutral horizon, dark ground bounce
        Undo.RecordObject(RenderSettings.skybox, "Setup Ambient");
        RenderSettings.ambientMode         = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.46f, 0.60f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.65f, 0.70f, 0.78f);
        RenderSettings.ambientGroundColor  = new Color(0.22f, 0.22f, 0.18f);

        // Atmospheric fog — starts close enough to be subtle, ends before the route does
        RenderSettings.fog              = true;
        RenderSettings.fogColor         = new Color(0.68f, 0.74f, 0.86f);
        RenderSettings.fogMode          = FogMode.Linear;
        RenderSettings.fogStartDistance = 120f;
        RenderSettings.fogEndDistance   = 500f;

        EditorUtility.SetDirty(sun.gameObject);
        Debug.Log("[Mousebus] Level lighting configured.");
        EditorUtility.DisplayDialog("Done",
            "Lighting applied:\n• Warm directional sun (48°, soft shadows)\n• Trilight ambient (blue sky / neutral / dark ground)\n• Linear fog 120–500 m\n\nTune all values in the Inspector and Lighting window.", "OK");
    }

    // ── Update Level Complete Scene ───────────────────────────────────────

    // Adds the gradeText and statsText labels to an existing LevelComplete hierarchy.
    // Run with the LevelComplete scene open if you set it up before the scoring system.
    [MenuItem("Mousebus/Update Level Complete Scene (Add Score Labels)")]
    public static void UpdateLevelCompleteScene()
    {
        LevelCompleteUI ui = Object.FindFirstObjectByType<LevelCompleteUI>();
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Scene Not Found",
                "Open the LevelComplete scene first, then run this.", "OK");
            return;
        }

        SerializedObject so = new SerializedObject(ui);

        // Grade label — sits between the level name and the buttons
        if (so.FindProperty("gradeText").objectReferenceValue == null)
        {
            TMP_Text grade = CreateLabel(ui.transform, "GradeText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200f, -15f), new Vector2(200f, 15f),
                "", 30);
            grade.alignment = TMPro.TextAlignmentOptions.Center;
            so.FindProperty("gradeText").objectReferenceValue = grade;
            Debug.Log("[Mousebus] GradeText added.");
        }
        else Debug.Log("[Mousebus] GradeText already wired — skipping.");

        // Stats breakdown label — below the buttons, hidden by default
        if (so.FindProperty("statsText").objectReferenceValue == null)
        {
            TMP_Text stats = CreateLabel(ui.transform, "StatsText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-260f, -390f), new Vector2(260f, -250f),
                "", 22);
            stats.alignment = TMPro.TextAlignmentOptions.Left;
            so.FindProperty("statsText").objectReferenceValue = stats;
            Debug.Log("[Mousebus] StatsText added.");
        }
        else Debug.Log("[Mousebus] StatsText already wired — skipping.");

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);

        EditorUtility.DisplayDialog("Done",
            "GradeText and StatsText labels added and wired.\nSave the LevelComplete scene (Ctrl+S).", "OK");
    }

    // ── Passenger Data Assets ─────────────────────────────────────────────

    // Creates 30 passenger ScriptableObjects + a Passengers_Global roster asset,
    // then auto-wires the roster to every BusStop in the open scene.
    // Run once — skips any files that already exist.
    [MenuItem("Mousebus/Create Passenger Data Assets")]
    public static void CreatePassengerDataAssets()
    {
        string folder = "Assets/_Mousebus/Data/Passengers";
        EnsureFolder("Assets/_Mousebus/Data");
        EnsureFolder(folder);

        // All 30 temp passengers — (filename, name, age, job, hobbies, timesRidden)
        var roster = new (string file, string name, int age, string job, string hobbies, int times)[]
        {
            ("Margaret_Chen",    "Margaret Chen",    67, "Retired Teacher",        "Reading, crossword puzzles, gardening",       847),
            ("Kofi_Mensah",      "Kofi Mensah",      28, "Software Developer",     "Rock climbing, vinyl collecting, chess",        23),
            ("Priya_Nair",       "Priya Nair",       41, "Nurse",                  "Yoga, cooking, podcast listening",             312),
            ("Dmitri_Volkov",    "Dmitri Volkov",    55, "Chef",                   "Hiking, photography, jazz piano",              188),
            ("Lily_Santos",      "Lily Santos",      19, "University Student",     "Skateboarding, digital art, anime",             94),
            ("Herbert_Walsh",    "Herbert Walsh",    72, "Retired Postal Worker",  "Bird watching, woodworking, fishing",         1203),
            ("Amara_Diallo",     "Amara Diallo",     34, "Graphic Designer",       "Running, ceramics, travelling",                 67),
            ("Owen_Park",        "Owen Park",        45, "Accountant",             "Golf, model trains, true crime podcasts",      156),
            ("Fatima_Al-Hassan", "Fatima Al-Hassan", 29, "Pharmacist",             "Calligraphy, baking, foreign films",            45),
            ("Tom_Briggs",       "Tom Briggs",       52, "Bus Driver",             "Football, pub quizzes, darts",                  22),
            ("Yuki_Tanaka",      "Yuki Tanaka",      23, "Barista",                "Illustration, houseplants, RPG games",         189),
            ("Caroline_West",    "Caroline West",    38, "Lawyer",                 "Cycling, wine tasting, theatre",                78),
            ("Marcus_Jones",     "Marcus Jones",     61, "Janitor",                "Blues guitar, dominoes, boxing",               934),
            ("Elena_Popescu",    "Elena Popescu",    44, "Architect",              "Jogging, origami, European cinema",            201),
            ("Samuel_Osei",      "Samuel Osei",      16, "High School Student",    "Basketball, beatboxing, comics",                31),
            ("Ruth_Hammond",     "Ruth Hammond",     80, "Retired",                "Knitting, soap operas, church choir",         2847),
            ("Devlin_Mohan",     "Devlin Mohan",     36, "Firefighter",            "Weightlifting, cooking, video games",           14),
            ("Nancy_Chu",        "Nancy Chu",        57, "Librarian",              "Historical fiction, bonsai, jigsaw puzzles",   623),
            ("Jake_Wheeler",     "Jake Wheeler",     26, "Delivery Driver",        "Skateboarding, street photography, coffee",      8),
            ("Isabelle_Martin",  "Isabelle Martin",  48, "Doctor",                 "Trail running, watercolour painting, meditation", 445),
            ("Clarence_Boyd",    "Clarence Boyd",    70, "Retired Mechanic",       "Fishing, woodcarving, jazz records",          1102),
            ("Sofia_Reyes",      "Sofia Reyes",      31, "Marketing Manager",      "Pilates, true crime, interior design",          56),
            ("Akira_Yamamoto",   "Akira Yamamoto",   40, "Translator",             "Calligraphy, tea ceremony, hiking",            388),
            ("Brendan_OSullivan","Brendan O'Sullivan",63,"Retired Police Officer", "Golf, crossword puzzles, gardening",           712),
            ("Mia_Thompson",     "Mia Thompson",     22, "Barista",                "Pottery, thrift shopping, indie music",         41),
            ("Victor_Huang",     "Victor Huang",     50, "Engineer",               "RC planes, chess, history books",              267),
            ("Leila_Rashidi",    "Leila Rashidi",    35, "Social Worker",          "Volunteering, salsa dancing, photography",     129),
            ("Patrick_Nolan",    "Patrick Nolan",    44, "Electrician",            "Football, DIY projects, podcast hosting",      203),
            ("Grace_Kim",        "Grace Kim",        27, "Teacher",                "Bullet journaling, baking, K-drama",            88),
            ("Harold_Brooks",    "Harold Brooks",    65, "Retired Banker",         "Birdwatching, model ships, classical music",   567),
        };

        var passengerAssets = new List<PassengerData>();

        foreach (var p in roster)
        {
            string path = $"{folder}/{p.file}.asset";
            PassengerData asset;

            if (File.Exists(path))
            {
                asset = AssetDatabase.LoadAssetAtPath<PassengerData>(path);
                Debug.Log($"[Mousebus] {p.file} already exists, skipping.");
            }
            else
            {
                asset               = ScriptableObject.CreateInstance<PassengerData>();
                asset.passengerName = p.name;
                asset.age           = p.age;
                asset.job           = p.job;
                asset.hobbies       = p.hobbies;
                asset.timesRidden   = p.times;
                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"[Mousebus] Created passenger: {p.name}");
            }

            if (asset != null) passengerAssets.Add(asset);
        }

        // ── Create or update the global roster ──
        string rosterPath = "Assets/_Mousebus/Data/Passengers_Global.asset";
        PassengerRoster rosterAsset;

        if (File.Exists(rosterPath))
        {
            rosterAsset = AssetDatabase.LoadAssetAtPath<PassengerRoster>(rosterPath);
        }
        else
        {
            rosterAsset = ScriptableObject.CreateInstance<PassengerRoster>();
            AssetDatabase.CreateAsset(rosterAsset, rosterPath);
        }

        rosterAsset.passengers = passengerAssets;
        EditorUtility.SetDirty(rosterAsset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── Auto-wire to all BusStops in the open scene ──
        int wired = 0;
        foreach (var stop in Object.FindObjectsByType<BusStop>(FindObjectsSortMode.None))
        {
            SerializedObject so   = new SerializedObject(stop);
            var rosterProp = so.FindProperty("passengerRoster");
            if (rosterProp != null && rosterProp.objectReferenceValue == null)
            {
                rosterProp.objectReferenceValue = rosterAsset;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(stop);
                wired++;
            }
        }

        string wiredMsg = wired > 0
            ? $"\n\nAuto-wired to {wired} BusStop(s) in the open scene."
            : "\n\nNo BusStops found in open scene — drag Passengers_Global onto each stop's Passenger Roster slot manually.";

        EditorUtility.DisplayDialog("Done",
            $"30 passengers + Passengers_Global roster created in:\n{folder}{wiredMsg}", "OK");
    }

    // ── Passenger Info Panel ──────────────────────────────────────────────

    // Stamps the left-side passenger card panel into the currently open scene.
    // Run this once per level scene after Setup Level Scene UI.
    [MenuItem("Mousebus/Setup Passenger Info Panel")]
    public static void SetupPassengerInfoPanel()
    {
        if (Object.FindFirstObjectByType<PassengerInfoPanel>() != null)
        {
            Debug.Log("[Mousebus] PassengerInfoPanel already exists in this scene.");
            return;
        }

        // Canvas — sort order 15, above HUD (10)
        GameObject canvasGO = new GameObject("PassengerCardCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Manager root — stretch to fill the canvas so child cards can use
        // centre-anchored positions relative to the screen centre.
        // Cards are spawned as children at runtime by PassengerInfoPanel.
        GameObject managerGO = new GameObject("PassengerInfoPanel");
        managerGO.transform.SetParent(canvasGO.transform, false);
        // Add a transparent Image so Unity creates the RectTransform
        var img = managerGO.AddComponent<UnityEngine.UI.Image>();
        img.color         = Color.clear;
        img.raycastTarget = false;
        StretchToFill(managerGO.GetComponent<RectTransform>());

        managerGO.AddComponent<PassengerInfoPanel>();

        Selection.activeGameObject = managerGO;
        AssetDatabase.SaveAssets();

        Debug.Log("[Mousebus] Passenger Info Panel created.");
        EditorUtility.DisplayDialog("Done",
            "Passenger card manager created.\n\n" +
            "Run Mousebus → Create Passenger Data Assets if you haven't already — " +
            "it auto-wires the roster to all BusStops in the open scene.\n\n" +
            "Cards spawn and stack automatically as each passenger boards. " +
            "Tune Slide In X, Base Y, and Stack Offset on the PassengerInfoPanel component.", "OK");
    }

    [MenuItem("Mousebus/Add Alighting Notice Panel")]
    public static void AddAlightingNoticePanel()
    {
        if (Object.FindFirstObjectByType<AlightingNoticePanel>() != null)
        {
            Debug.Log("[Mousebus] AlightingNoticePanel already exists in this scene.");
            return;
        }

        // Prefer the existing passenger card canvas so both live in the same draw layer.
        // Fall back to creating a new canvas if the boarding panel hasn't been set up yet.
        var existingCanvas = Object.FindFirstObjectByType<PassengerInfoPanel>();
        Canvas canvas;
        if (existingCanvas != null)
        {
            canvas = existingCanvas.GetComponentInParent<Canvas>();
        }
        else
        {
            var canvasGO = new GameObject("AlightingNoticeCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Root stretched to fill the canvas — child notices are centre-anchored.
        var rootGO = new GameObject("AlightingNoticePanel");
        rootGO.transform.SetParent(canvas.transform, false);
        var img = rootGO.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.clear; img.raycastTarget = false;
        StretchToFill(rootGO.GetComponent<RectTransform>());

        rootGO.AddComponent<AlightingNoticePanel>();

        Selection.activeGameObject = rootGO;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Done",
            "Alighting Notice Panel added.\n\n" +
            "Save the scene now (Ctrl+S) so the panel persists between sessions and when " +
            "loading from the main menu.\n\n" +
            "Tune Anchor X, Base Y, Stack Step, and timing on the AlightingNoticePanel component.", "OK");
    }

    // Variant of CreateLabel without the shared pivot shorthand, for panel-relative anchoring
    private static TMP_Text CreatePanelLabel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        string text, float fontSize,
        Color? colour = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TMP_Text tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text                = text;
        tmp.fontSize            = fontSize;
        tmp.color               = colour ?? Color.white;
        tmp.alignment           = TMPro.TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping  = true;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        return tmp;
    }

    // ── HUD Label (test helper) ───────────────────────────────────────────

    // Creates a centre-anchored label with a coloured background so the rect is
    // visible even if text fails to render. offsetMin/Max are pixel offsets from
    // screen centre (anchor 0.5, 0.5). Remove bgColor once HUD positions confirmed.
    private static TMP_Text CreateHUDLabel(Transform parent, string name,
        Vector2 offsetMin, Vector2 offsetMax,
        string text, float fontSize, Color bgColor)
    {
        // Background panel — makes the rect visible regardless of text rendering
        GameObject bgGO = new GameObject(name + "_BG");
        bgGO.transform.SetParent(parent, false);
        bgGO.AddComponent<UnityEngine.UI.Image>().color = bgColor;
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.offsetMin = offsetMin;
        bgRect.offsetMax = offsetMax;

        // Text label as child of the background panel
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(bgGO.transform, false);
        TMP_Text tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 2f);
        textRect.offsetMax = new Vector2(-4f, -2f);

        return tmp;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void StretchToFill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
