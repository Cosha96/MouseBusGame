using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

    // ── CutscenePlayer Prefab ─────────────────────────────────────────────

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

        // ── Slide image (the PNG itself) ──
        // preserveAspect = true keeps portrait/landscape PNGs from stretching
        GameObject slideGO = new GameObject("SlideImage");
        slideGO.transform.SetParent(canvasGO.transform, false);
        Image slideImage = slideGO.AddComponent<Image>();
        slideImage.color = Color.white;
        slideImage.preserveAspect = true;
        StretchToFill(slideGO.GetComponent<RectTransform>());

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
        so.FindProperty("slideImage").objectReferenceValue = slideImage;
        so.FindProperty("subtitleText").objectReferenceValue = subtitleText;
        so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
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
        // Scale approximates a real bus footprint — swap with your model later
        GameObject bus = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bus.name = "Bus";
        bus.transform.localScale = new Vector3(2.5f, 2f, 6f);
        bus.transform.position   = new Vector3(0f, 1f, 0f); // sit flush on the ground

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

        AssetDatabase.SaveAssets();
        Debug.Log("[Mousebus] Level Scene UI created. Assign a BusController to the HUD if not auto-found.");
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
        canvas.sortingOrder = 10; // above world, below cutscene (50) and loading (100)
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        CanvasGroup cg = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;

        // Speed label — top-left corner
        TMP_Text speedText = CreateLabel(canvasGO.transform, "SpeedText",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -10f), new Vector2(200f, -50f),
            "0 km/h", 28);

        // Bus count — top-centre
        TMP_Text busText = CreateLabel(canvasGO.transform, "BusCountText",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-100f, -10f), new Vector2(100f, -50f),
            "0/40", 28);

        // Clock — top-right corner
        TMP_Text clockText = CreateLabel(canvasGO.transform, "ClockText",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-210f, -10f), new Vector2(-10f, -50f),
            "00:00", 28);

        // Wire HUD component
        HUD hud = canvasGO.AddComponent<HUD>();
        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("speedText").objectReferenceValue   = speedText;
        so.FindProperty("busCountText").objectReferenceValue = busText;
        so.FindProperty("clockText").objectReferenceValue   = clockText;
        so.FindProperty("canvasGroup").objectReferenceValue = cg;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = canvasGO;
        Debug.Log("[Mousebus] HUD created.");
    }

    private static void CreatePauseMenu()
    {
        if (Object.FindFirstObjectByType<PauseMenuUI>() != null)
        {
            Debug.Log("[Mousebus] PauseMenu already exists in this scene.");
            return;
        }

        // Canvas — sort order 40, below cutscene canvas (50)
        GameObject canvasGO = new GameObject("PauseMenu");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        CanvasGroup cg = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;

        // Semi-transparent dark panel
        GameObject panelGO = new GameObject("Background");
        panelGO.transform.SetParent(canvasGO.transform, false);
        UnityEngine.UI.Image bg = panelGO.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        StretchToFill(panelGO.GetComponent<RectTransform>());

        // "PAUSED" title
        CreateLabel(canvasGO.transform, "TitleText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-200f, 60f), new Vector2(200f, 120f),
            "PAUSED", 48);

        // Buttons
        UnityEngine.UI.Button resumeBtn = CreateButton(canvasGO.transform, "ResumeButton",
            new Vector2(0.5f, 0.5f), new Vector2(-100f, -10f), new Vector2(100f, 30f), "Resume");
        UnityEngine.UI.Button mainMenuBtn = CreateButton(canvasGO.transform, "MainMenuButton",
            new Vector2(0.5f, 0.5f), new Vector2(-100f, -60f), new Vector2(100f, -20f), "Main Menu");
        UnityEngine.UI.Button quitBtn = CreateButton(canvasGO.transform, "QuitButton",
            new Vector2(0.5f, 0.5f), new Vector2(-100f, -110f), new Vector2(100f, -70f), "Quit");

        // Wire PauseMenuUI
        PauseMenuUI pauseUI = canvasGO.AddComponent<PauseMenuUI>();
        SerializedObject so = new SerializedObject(pauseUI);
        so.FindProperty("canvasGroup").objectReferenceValue  = cg;
        so.FindProperty("resumeButton").objectReferenceValue    = resumeBtn;
        so.FindProperty("mainMenuButton").objectReferenceValue  = mainMenuBtn;
        so.FindProperty("quitButton").objectReferenceValue      = quitBtn;
        so.ApplyModifiedProperties();

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
            new Vector2(-200f, 10f), new Vector2(200f, 60f),
            "", 32);

        // Buttons
        UnityEngine.UI.Button continueBtn = CreateButton(canvasGO.transform, "ContinueButton",
            new Vector2(0.5f, 0.5f), new Vector2(-150f, -60f), new Vector2(150f, -10f), "Continue");
        UnityEngine.UI.Button mainMenuBtn = CreateButton(canvasGO.transform, "MainMenuButton",
            new Vector2(0.5f, 0.5f), new Vector2(-150f, -120f), new Vector2(150f, -70f), "Main Menu");
        UnityEngine.UI.Button statsBtn = CreateButton(canvasGO.transform, "StatsButton",
            new Vector2(0.5f, 0.5f), new Vector2(-150f, -180f), new Vector2(150f, -130f), "Stats");

        // Wire LevelCompleteUI
        LevelCompleteUI lcUI = canvasGO.AddComponent<LevelCompleteUI>();
        SerializedObject so = new SerializedObject(lcUI);
        so.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        so.FindProperty("canvasGroup").objectReferenceValue   = cg;
        so.FindProperty("titleRect").objectReferenceValue     = titleRect;
        so.FindProperty("continueButton").objectReferenceValue = continueBtn;
        so.FindProperty("mainMenuButton").objectReferenceValue = mainMenuBtn;
        so.FindProperty("statsButton").objectReferenceValue    = statsBtn;
        so.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(canvasGO);
        Debug.Log("[Mousebus] LevelComplete UI created. Open LevelComplete scene first.");
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

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void StretchToFill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
