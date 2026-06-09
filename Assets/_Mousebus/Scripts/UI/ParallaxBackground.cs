using UnityEngine;

// Attach to: any GameObject in the MainMenu scene.
// Moves UI layers at different speeds as the mouse moves, creating a parallax depth effect.
//
// Setup:
//   1. Add your background layers as children of the Canvas.
//   2. Add each layer's RectTransform to the Layers array.
//   3. Set Speed Multiplier per layer: 0 = stationary, 1 = full movement.
//      Typical setup: sky = 0.1, buildings = 0.3, foreground = 0.6
public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public struct ParallaxLayer
    {
        [Tooltip("RectTransform of this background layer")]
        public RectTransform rect;
        [Tooltip("How far this layer shifts relative to the base strength")]
        [Range(0f, 1f)] public float speedMultiplier;
    }

    [SerializeField] private ParallaxLayer[] layers;

    [Tooltip("Max pixels the furthest layer moves at full mouse deflection")]
    [SerializeField] private float strength = 20f;

    [Tooltip("Smoothing speed — higher = snappier tracking")]
    [SerializeField] private float smoothing = 8f;

    // Resting anchoredPositions recorded at startup so offsets are always relative to origin
    private Vector2[] _basePositions;
    private Vector2   _currentOffset;

    private void Awake()
    {
        _basePositions = new Vector2[layers.Length];
        for (int i = 0; i < layers.Length; i++)
            if (layers[i].rect != null)
                _basePositions[i] = layers[i].rect.anchoredPosition;
    }

    private void Update()
    {
        // Normalize mouse position: (0,0) = bottom-left, (1,1) = top-right → remap to -1..1
        Vector2 screen    = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 mouseNorm = ((Vector2)Input.mousePosition - screen) / screen;
        mouseNorm = Vector2.ClampMagnitude(mouseNorm, 1f);

        // Smooth so layers glide rather than snap
        _currentOffset = Vector2.Lerp(_currentOffset, mouseNorm * strength, Time.unscaledDeltaTime * smoothing);

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].rect == null) continue;
            layers[i].rect.anchoredPosition = _basePositions[i] + _currentOffset * layers[i].speedMultiplier;
        }
    }
}
