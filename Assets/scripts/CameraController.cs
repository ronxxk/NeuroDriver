using UnityEngine;
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
        transform.position = Vector3.Lerp(transform.position, targetPosition, movementSmoothing);

        if (cam.orthographic)
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, zoomSmoothing);
        }
    }

    void HandleNewMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return; 

        Vector3 direction = Vector3.zero;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) direction.y += 1;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) direction.y -= 1;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction.x -= 1;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction.x += 1;

        Vector3 moveDirection = direction.normalized;
        targetPosition += moveDirection * moveSpeed * Time.deltaTime;
    }

    void HandleNewZoomInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 scrollVector = mouse.scroll.ReadValue();

        if (scrollVector.y != 0f && cam.orthographic)
        {
            float scrollInput = Mathf.Sign(scrollVector.y); 

            targetZoom -= scrollInput * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }
    }
}