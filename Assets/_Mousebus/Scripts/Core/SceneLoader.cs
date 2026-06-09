using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Handles async scene transitions with a fade-to-black loading screen.
// Attach alongside GameManager.cs on the same persistent prefab.
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    // Assign the loading screen Canvas's CanvasGroup here in the Inspector.
    // CanvasGroup lets us control alpha, raycast blocking, and interactability together.
    [SerializeField] private CanvasGroup loadingScreen;
    [SerializeField] private float fadeDuration = 0.35f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // DontDestroyOnLoad is already called by GameManager.Awake() on this same
        // GameObject — calling it twice would be harmless but redundant.

        if (loadingScreen != null)
        {
            loadingScreen.alpha = 0f;
            loadingScreen.blocksRaycasts = false; // don't eat clicks when invisible
            loadingScreen.interactable = false;   // it's a cover, never needs interaction
        }
    }

    public void LoadSceneAsync(string sceneName)
    {
        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        // 1. Fade IN — cover the outgoing scene before we tear it down
        yield return StartCoroutine(Fade(1f));

        // 2. Begin loading the new scene without activating it yet.
        //    allowSceneActivation = false holds Unity at exactly 90% loaded,
        //    giving us full control over when the visual swap happens.
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);
        loadOp.allowSceneActivation = false;

        while (loadOp.progress < 0.9f)
            yield return null;

        // 3. Activate — Unity swaps scenes, calls Awake/Start in the new scene
        loadOp.allowSceneActivation = true;
        yield return null; // let Awake/Start finish before we reveal anything

        // 4. Fade OUT — reveal the new scene
        yield return StartCoroutine(Fade(0f));
    }

    private IEnumerator Fade(float targetAlpha)
    {
        float startAlpha = loadingScreen.alpha;
        float elapsed = 0f;

        loadingScreen.blocksRaycasts = (targetAlpha > 0f);

        while (elapsed < fadeDuration)
        {
            // unscaledDeltaTime means this fade works even when timeScale == 0.
            // Useful if a transition ever triggers while the game is paused.
            elapsed += Time.unscaledDeltaTime;
            loadingScreen.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        loadingScreen.alpha = targetAlpha; // snap to exact value, avoid float drift
    }
}
