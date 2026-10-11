using UnityEngine;

/// <summary>
/// Procedural skid marks for CarController2D.
/// - Color picker + fade settings in the Inspector
/// - Tire texture is generated in code (Smooth / Tread / Dashed), no assets needed
/// Add to the same GameObject as CarController2D. Leave CarController2D.skidTrails EMPTY.
/// </summary>
[DisallowMultipleComponent]
public class SkidMarks2D : MonoBehaviour
{
    public enum TextureStyle { Smooth, Tread, Dashed }

    [Header("Look")]
    public Color markColor = new Color(0.07f, 0.07f, 0.07f, 0.85f);
    public TextureStyle style = TextureStyle.Tread;
    [Tooltip("Optional. Overrides the generated texture. Should be white + alpha so the color tints it.")]
    public Texture2D customTexture;
    [Min(0.02f)] public float width = 0.18f;
    [Tooltip("World units per texture repeat along the trail.")]
    [Min(0.05f)] public float tileLength = 0.5f;
    [Min(0.1f)] public float fadeTime = 4f;
    [Min(0.01f)] public float minVertexDistance = 0.08f;

    [Header("Generated Texture")]
    [Range(1, 24)] public int treadCount = 6;       // chevrons / dashes per tile
    [Range(0f, 1f)] public float wear = 0.35f;      // how patchy/worn the rubber looks
    [Range(0.05f, 0.5f)] public float edgeSoftness = 0.25f;
    public int textureWidth = 64;
    public int textureHeight = 32;

    [Header("Wheels")]
    [Tooltip("Leave empty to auto-create two rear wheels from the offset below.")]
    public Transform[] wheels;
    public Vector2 rearWheelOffset = new Vector2(0.35f, -0.5f); // x = half track width, y = rear axle (negative = behind center)

    [Header("Trigger")]
    public bool markOnHandbrake = true;
    public float minSpeedForHandbrakeMark = 2f;

    [Header("Rendering")]
    public string sortingLayerName = "Default";
    public int sortingOrder = -1; // under the car sprite

    CarController2D car;
    TrailRenderer[] trails;
    Material mat;
    Texture2D generatedTex;

    void Awake()
    {
        car = GetComponent<CarController2D>();
        if (!car) car = GetComponentInParent<CarController2D>();
        BuildWheels();
        BuildTrails();
        ApplySettings();
    }

    void Update()
    {
        if (!car || trails == null) return;

        bool emit = car.IsDrifting ||
                    (markOnHandbrake && car.HandbrakeHeld && Mathf.Abs(car.ForwardSpeed) > minSpeedForHandbrakeMark);

        foreach (var t in trails) if (t) t.emitting = emit;
    }

    // ---------------- Setup ----------------

    void BuildWheels()
    {
        if (wheels != null && wheels.Length > 0) return;

        var left = new GameObject("Wheel_RL").transform;
        var right = new GameObject("Wheel_RR").transform;
        left.SetParent(transform, false);
        right.SetParent(transform, false);
        left.localPosition = new Vector3(-rearWheelOffset.x, rearWheelOffset.y, 0f);
        right.localPosition = new Vector3(rearWheelOffset.x, rearWheelOffset.y, 0f);
        wheels = new[] { left, right };
    }

    void BuildTrails()
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (!sh) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        mat = new Material(sh);

        trails = new TrailRenderer[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            if (!wheels[i]) continue;
            var t = wheels[i].GetComponent<TrailRenderer>();
            if (!t) t = wheels[i].gameObject.AddComponent<TrailRenderer>();
            t.emitting = false;
            t.alignment = LineAlignment.TransformZ; // flat on the 2D plane
            t.textureMode = LineTextureMode.Tile;
            t.numCapVertices = 2;
            t.numCornerVertices = 2;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            t.sharedMaterial = mat;
            trails[i] = t;
        }
    }

    // Re-applies every inspector value. Safe to call anytime.
    void ApplySettings()
    {
        if (trails == null || mat == null) return;

        Texture2D tex = customTexture ? customTexture : GenerateTexture();
        mat.mainTexture = tex;
        mat.mainTextureScale = new Vector2(1f / Mathf.Max(0.05f, tileLength), 1f);

        // Gradient: quick fade-in at the wheel, solid middle, fade-out at the old end.
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(markColor, 0f), new GradientColorKey(markColor, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(markColor.a, 0.04f),
                new GradientAlphaKey(markColor.a, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });

        int layerId = SortingLayer.NameToID(sortingLayerName);
        bool layerOk = SortingLayer.IsValid(layerId);

        foreach (var t in trails)
        {
            if (!t) continue;
            t.colorGradient = g;
            t.widthMultiplier = width;
            t.time = fadeTime;
            t.minVertexDistance = minVertexDistance;
            if (layerOk) t.sortingLayerID = layerId;
            t.sortingOrder = sortingOrder;
        }
    }

    // ---------------- Procedural texture ----------------
    // U (x) runs along the trail and tiles seamlessly, V (y) runs across the width.

    Texture2D GenerateTexture()
    {
        int w = Mathf.Max(8, textureWidth);
        int h = Mathf.Max(8, textureHeight);

        if (generatedTex) Destroy(generatedTex);
        generatedTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "SkidMarkTex (generated)",
            wrapModeU = TextureWrapMode.Repeat,
            wrapModeV = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            float centerDist = Mathf.Abs(v - 0.5f) * 2f;      // 0 center -> 1 edge
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - centerDist) / edgeSoftness));

            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;

                // seamless wear noise: sample perlin on a circle around the U axis
                float ang = u * Mathf.PI * 2f;
                float n = Mathf.PerlinNoise(Mathf.Cos(ang) * 2.5f + 20f, Mathf.Sin(ang) * 2.5f + v * 4f);
                float wearMul = 1f - wear * Mathf.SmoothStep(0.35f, 0.85f, n); // patchy gaps

                float pattern = 1f;
                switch (style)
                {
                    case TextureStyle.Tread:
                    {
                        // chevron grooves: integer repeat count keeps it seamless
                        float s = Mathf.Repeat((u + centerDist * 0.45f) * treadCount, 1f);
                        float stripe = Mathf.Abs(s - 0.5f) * 2f;
                        pattern = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0.3f, 0.7f, stripe));
                        break;
                    }
                    case TextureStyle.Dashed:
                    {
                        float d = Mathf.Repeat(u * treadCount, 1f);
                        pattern = Mathf.SmoothStep(0f, 0.08f, d) * (1f - Mathf.SmoothStep(0.55f, 0.63f, d));
                        break;
                    }
                }

                float a = Mathf.Clamp01(edge * pattern * wearMul);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }

        generatedTex.SetPixels(px);
        generatedTex.Apply();
        return generatedTex;
    }

    // ---------------- Editor helpers ----------------

    // Live-tweak in Play mode: changing any inspector value updates the marks.
    void OnValidate()
    {
        textureWidth = Mathf.Max(8, textureWidth);
        textureHeight = Mathf.Max(8, textureHeight);
        if (Application.isPlaying && trails != null) ApplySettings();
    }

    [ContextMenu("Clear All Skid Marks")]
    void ClearMarks()
    {
        if (trails == null) return;
        foreach (var t in trails) if (t) t.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (wheels != null && wheels.Length > 0 && wheels[0])
        {
            foreach (var wh in wheels) if (wh) Gizmos.DrawWireSphere(wh.position, 0.08f);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.TransformPoint(new Vector3(-rearWheelOffset.x, rearWheelOffset.y, 0f)), 0.08f);
            Gizmos.DrawWireSphere(transform.TransformPoint(new Vector3(rearWheelOffset.x, rearWheelOffset.y, 0f)), 0.08f);
        }
    }

    void OnDestroy()
    {
        if (generatedTex) Destroy(generatedTex);
        if (mat) Destroy(mat);
    }
}
