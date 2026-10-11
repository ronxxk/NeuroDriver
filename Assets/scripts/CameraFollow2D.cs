using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Smooth 2D follow camera. Never rotates, so the world stays fixed while the car turns.
/// Mouse wheel zooms in/out. Put on the Main Camera and assign the car as Target.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;
    [Tooltip("Higher = snappier. 0 = locked to the car.")]
    public float followSmoothing = 8f;
    public Vector2 offset = Vector2.zero;
    [Tooltip("Camera leads ahead in the direction the car is moving.")]
    public float lookAhead = 1.5f;
    public float lookAheadSmoothing = 3f;

    [Header("Zoom (mouse wheel)")]
    public float startZoom = 7f;
    public float minZoom = 3f;
    public float maxZoom = 18f;
    public float scrollSensitivity = 1.2f;
    public float zoomSmoothing = 10f;

    [Header("Speed Zoom")]
    [Tooltip("Extra zoom-out at top speed. 0 = off.")]
    public float speedZoomOut = 1.5f;
    public float speedForMaxZoom = 14f;

    Camera cam;
    Rigidbody2D targetRb;
    float zoomTarget;
    Vector2 lookOffset;
    float speedZoom;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        zoomTarget = Mathf.Clamp(startZoom, minZoom, maxZoom);
        cam.orthographicSize = zoomTarget;
        transform.rotation = Quaternion.identity;

        if (target)
        {
            targetRb = target.GetComponent<Rigidbody2D>();
            // prevents camera jitter against a physics-driven car
            if (targetRb) targetRb.interpolation = RigidbodyInterpolation2D.Interpolate;
            SnapToTarget();
        }
    }

    void Update()
    {
        float scroll = ReadScroll();
        if (Mathf.Abs(scroll) > 0.001f)
        {
            // multiplicative zoom feels even at every level
            zoomTarget *= Mathf.Exp(-scroll * scrollSensitivity * 0.15f);
            zoomTarget = Mathf.Clamp(zoomTarget, minZoom, maxZoom);
        }
    }

    void LateUpdate()
    {
        if (!target) return;
        float dt = Time.deltaTime;

        // --- look ahead + speed zoom ---
        Vector2 vel = targetRb ? GetVelocity(targetRb) : Vector2.zero;
        Vector2 wantLook = vel.sqrMagnitude > 0.01f
            ? vel.normalized * lookAhead * Mathf.Clamp01(vel.magnitude / Mathf.Max(0.01f, speedForMaxZoom))
            : Vector2.zero;
        lookOffset = Vector2.Lerp(lookOffset, wantLook, 1f - Mathf.Exp(-lookAheadSmoothing * dt));

        float wantSpeedZoom = speedZoomOut * Mathf.Clamp01(vel.magnitude / Mathf.Max(0.01f, speedForMaxZoom));
        speedZoom = Mathf.Lerp(speedZoom, wantSpeedZoom, 1f - Mathf.Exp(-2f * dt));

        // --- position ---
        Vector2 desired = (Vector2)target.position + offset + lookOffset;
        Vector3 pos = transform.position;
        if (followSmoothing <= 0f)
        {
            pos.x = desired.x; pos.y = desired.y;
        }
        else
        {
            float t = 1f - Mathf.Exp(-followSmoothing * dt); // frame-rate independent
            pos.x = Mathf.Lerp(pos.x, desired.x, t);
            pos.y = Mathf.Lerp(pos.y, desired.y, t);
        }
        transform.position = pos;
        transform.rotation = Quaternion.identity; // never rotate

        // --- zoom ---
        float goal = Mathf.Clamp(zoomTarget + speedZoom, minZoom, maxZoom + speedZoomOut);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, goal, 1f - Mathf.Exp(-zoomSmoothing * dt));
    }

    public void SnapToTarget()
    {
        if (!target) return;
        Vector3 p = target.position;
        transform.position = new Vector3(p.x + offset.x, p.y + offset.y, transform.position.z);
    }

    float ReadScroll()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    static Vector2 GetVelocity(Rigidbody2D rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }
}
