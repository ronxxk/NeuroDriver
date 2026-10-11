using UnityEngine;
// 1. MUST INCLUDE THIS LINE AT THE TOP FOR THE NEW INPUT SYSTEM
using UnityEngine.InputSystem; 

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 15f;
    [Range(0.01f, 1f)]
    public float movementSmoothing = 0.1f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 8f;
    public float minZoom = 2f;
    public float maxZoom = 20f;
    [Range(0.01f, 1f)]
    public float zoomSmoothing = 0.15f;

    private Camera cam;
    private Vector3 targetPosition;
    private float targetZoom;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetPosition = transform.position;
        if (cam.orthographic) targetZoom = cam.orthographicSize;
    }

    void Update()
    {
        HandleNewMovementInput();
        HandleNewZoomInput();
    }

    void FixedUpdate()
    {
        // Smoothly glide position
        transform.position = Vector3.Lerp(transform.position, targetPosition, movementSmoothing);

        // Smoothly glide zoom
        if (cam.orthographic)
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, zoomSmoothing);
        }
    }

    void HandleNewMovementInput()
    {
        // Access the current keyboard hardware layout directly
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return; // Safeguard if no keyboard is plugged in

        Vector3 direction = Vector3.zero;

        // Read keys using the new Input System syntax
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) direction.y += 1;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) direction.y -= 1;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction.x -= 1;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction.x += 1;

        Vector3 moveDirection = direction.normalized;
        targetPosition += moveDirection * moveSpeed * Time.deltaTime;
    }

    void HandleNewZoomInput()
    {
        // Access the current mouse hardware layout directly
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // Read the mouse scroll wheel vector
        Vector2 scrollVector = mouse.scroll.ReadValue();

        // scrollVector.y returns a positive or negative number when scrolling
        if (scrollVector.y != 0f && cam.orthographic)
        {
            // Normalize the input since different mice send vastly different values (e.g. 120 vs 1)
            float scrollInput = Mathf.Sign(scrollVector.y); 

            targetZoom -= scrollInput * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }
    }
}