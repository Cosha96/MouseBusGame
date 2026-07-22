using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Attach to: the root GameObject in the LevelComplete scene alongside LevelCompleteUI.
// Drives the layered anime-outro effect:
//   Bottom — a looping background video (MP4 from Premiere, e.g. photos / credits roll)
//   Mid    — an optional character playblast video rendered over the background
//   Music  — an outro track that crossfades over the carried level music
//
// Setup: run Mousebus → Setup Outro Layers to stamp the canvas layers automatically.
// Then assign your VideoClips and AudioClip in the Inspector.
public class OutroController : MonoBehaviour
{
    [Header("Background Video")]
    [Tooltip("VideoPlayer whose output feeds the background layer")]
    [SerializeField] private VideoPlayer backgroundPlayer;
    [Tooltip("Full-screen RawImage that displays the background video")]
    [SerializeField] private RawImage    backgroundImage;

    [Header("Character Video (optional)")]
    [Tooltip("Leave unassigned if you have no mid-layer playblast yet")]
    [SerializeField] private VideoPlayer characterPlayer;
    [Tooltip("RawImage for the character playblast — positioned however you like in the Canvas")]
    [SerializeField] private RawImage    characterImage;

    [Header("Outro Music")]
    [SerializeField] private AudioSource outroAudioSource;
    [Tooltip("The seasonal outro track to play (Summer / Fall / Winter / Spring — assign the right one per level)")]
    [SerializeField] private AudioClip   outroMusicClip;
    [Tooltip("Seconds to fade the outro music in")]
    [SerializeField] private float       musicFadeInDuration = 1.5f;
    [Tooltip("Seconds to crossfade the level music out while the outro music fades in")]
    [SerializeField] private float       levelMusicFadeOutDuration = 2f;

    private RenderTexture _bgRT;
    private RenderTexture _charRT;

    private void Start()
    {
        SetupVideo(backgroundPlayer, backgroundImage, ref _bgRT);
        SetupVideo(characterPlayer,  characterImage,  ref _charRT);
        StartCoroutine(CrossfadeToOutroMusic());
    }

    private void SetupVideo(VideoPlayer player, RawImage image, ref RenderTexture rt)
    {
        if (player == null || image == null) return;

        // Match the render texture to the video's native resolution if it's already prepared,
        // otherwise default to 1920x1080 — Unity resizes automatically on first frame.
        rt = new RenderTexture(1920, 1080, 0);
        player.targetTexture = rt;
        image.texture        = rt;
        player.isLooping     = true;
        player.Play();
    }

    // Fades the carried level music out while fading the outro track in.
    private IEnumerator CrossfadeToOutroMusic()
    {
        // Find the LevelMusicCarrier that DontDestroyOnLoad brought in from the level.
        var carrier = FindFirstObjectByType<LevelMusicCarrier>();
        var levelSource = carrier != null ? carrier.GetComponent<AudioSource>() : null;
        float levelStartVol = levelSource != null ? levelSource.volume : 0f;

        // Start outro music at zero volume.
        if (outroAudioSource != null && outroMusicClip != null)
        {
            outroAudioSource.clip   = outroMusicClip;
            outroAudioSource.volume = 0f;
            outroAudioSource.loop   = true;
            outroAudioSource.Play();
        }

        float fadeOutT  = 0f;
        float fadeInT   = 0f;
        bool  outroPlaying = outroAudioSource != null && outroMusicClip != null;

        while (fadeOutT < levelMusicFadeOutDuration || fadeInT < musicFadeInDuration)
        {
            float dt = Time.unscaledDeltaTime;

            if (fadeOutT < levelMusicFadeOutDuration)
            {
                fadeOutT += dt;
                if (levelSource != null)
                    levelSource.volume = Mathf.Lerp(levelStartVol, 0f,
                        fadeOutT / levelMusicFadeOutDuration);
            }

            if (outroPlaying && fadeInT < musicFadeInDuration)
            {
                fadeInT += dt;
                outroAudioSource.volume = Mathf.Clamp01(fadeInT / musicFadeInDuration);
            }

            yield return null;
        }

        // Destroy the carrier once it's silent — it's no longer needed.
        if (carrier != null) Destroy(carrier.gameObject);
    }

    private void OnDestroy()
    {
        if (_bgRT   != null) { _bgRT.Release();   Destroy(_bgRT);   }
        if (_charRT != null) { _charRT.Release();  Destroy(_charRT); }
    }
}
