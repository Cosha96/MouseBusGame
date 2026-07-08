using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Created by LevelManager just before the LevelComplete scene loads.
// Carries the level music across the scene boundary so it keeps playing on the
// end screen, then fades out when the player navigates away.
public class LevelMusicCarrier : MonoBehaviour
{
    private AudioSource _source;
    private float       _fadeOut;

    public void Initialize(AudioClip clip, float volume, float time, float fadeOut)
    {
        _fadeOut       = fadeOut;
        _source        = gameObject.AddComponent<AudioSource>();
        _source.clip   = clip;
        _source.loop   = true;
        _source.volume = volume;
        _source.time   = time;
        _source.spatialBlend = 0f;
        _source.Play();
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Stay alive on the level complete screen; fade out for everything else
        if (scene.name == "LevelComplete") return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float start = _source.volume;
        float t     = 0f;
        while (t < _fadeOut)
        {
            t += Time.unscaledDeltaTime;
            _source.volume = Mathf.Lerp(start, 0f, t / _fadeOut);
            yield return null;
        }
        Destroy(gameObject);
    }
}
