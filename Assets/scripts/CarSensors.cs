using UnityEngine;
using System.Collections.Generic;

public class CarSensors : MonoBehaviour
{
    public static readonly List<CarSensors> All = new List<CarSensors>();
    static int nextId = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        All.Clear();
        nextId = 1;
    }

    public string carId;

    [Header("Raycast Settings")]
    public LayerMask wallLayer;
    public float rayLength = 5f;

    [Header("Body Offset (distance from car center to its edge)")]
    public float bodyOffsetFront = 0.8f;
    public float bodyOffsetSide = 0.5f;

    [Header("Movement")]
    public float maxSpeed = 7f;
    public float speed;
    public float forwardSpeed;
    public float sidewaysSpeed;

    [Header("AI Control (set by SensorSender)")]
    public float aiSteer;
    public float aiThrottle;

    [Header("Fitness")]
    public float stallTime = 6f;
    public int passed;

    public float[] rayDistances = new float[7];
    public bool crashed;

    private Rigidbody2D rb;
    private CarController2D controller;

    private bool frozen;
    private float lastProgressTime;
    private Checkpoint nextCp;
    private Vector2 startPos, prevPos;
    private Quaternion startRot;

    private readonly float[] rayAngles = { -60f, -40f, -20f, 0f, 20f, 40f, 60f };

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<CarController2D>();
        if (controller != null)
        {
            controller.aiControlled = true;
            maxSpeed = controller.maxSpeed;
        }

        if (string.IsNullOrEmpty(carId))
            carId = "car" + (nextId++);

        Physics2D.queriesStartInColliders = false;
    }

    void Start()
    {
        startPos = prevPos = transform.position;
        startRot = transform.rotation;
        nextCp = Checkpoint.Get(0);
        lastProgressTime = Time.time;
    }

    void OnEnable()  { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (((1 << col.gameObject.layer) & wallLayer) != 0)
            crashed = true;
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (crashed) return;
        var cp = col.GetComponent<Checkpoint>();
        if (cp == null || cp != nextCp) return;

        passed++;
        prevPos = cp.transform.position;
        nextCp = Checkpoint.Get(passed % Checkpoint.Count);
        lastProgressTime = Time.time;
    }

    public float Fitness
    {
        get
        {
            if (nextCp == null) return passed;
            float seg = Vector2.Distance(prevPos, nextCp.transform.position);
            float d = Vector2.Distance(transform.position, nextCp.transform.position);
            return passed + (seg > 0.01f ? Mathf.Clamp01(1f - d / seg) : 0f);
        }
    }

    public void SetAI(float throttle, float steer)
    {
        if (controller == null) return;
        if (crashed) { controller.SetAIInput(0f, 0f, false); return; }
        controller.SetAIInput(throttle, -steer, false);
    }

    void Freeze()
    {
        frozen = true;
        if (controller != null) controller.enabled = false;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;
        speed = forwardSpeed = sidewaysSpeed = 0f;
        aiSteer = aiThrottle = 0f;
    }

    // Teleports back to where this car spawned and clears everything
    public void ResetState()
    {
        rb.simulated = true;
        rb.position = startPos;
        rb.rotation = startRot.eulerAngles.z;
        transform.SetPositionAndRotation(startPos, startRot);
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (controller != null)
        {
            controller.ResetControl();
            controller.enabled = true;
        }

        frozen = false;
        crashed = false;
        speed = forwardSpeed = sidewaysSpeed = 0f;
        aiSteer = aiThrottle = 0f;
        passed = 0;
        prevPos = startPos;
        nextCp = Checkpoint.Get(0);
        lastProgressTime = Time.time;
    }

    void FixedUpdate()
    {
        if (!crashed && Time.time - lastProgressTime > stallTime) crashed = true;
        if (crashed && !frozen) Freeze();
        if (frozen) return;

        UpdateSensors();
    }

    void UpdateSensors()
    {
        Vector2 position = transform.position;

        for (int i = 0; i < rayAngles.Length; i++)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, rayAngles[i]) * transform.up;

            float t = Mathf.Abs(rayAngles[i]) / 60f;
            float offset = Mathf.Lerp(bodyOffsetFront, bodyOffsetSide, t);

            RaycastHit2D hit = Physics2D.Raycast(position, direction, rayLength, wallLayer);

            if (hit.collider != null)
            {
                float d = Mathf.Max(0f, hit.distance - offset);
                rayDistances[i] = Mathf.Clamp01(d / (rayLength - offset));
                Debug.DrawRay(position, direction * hit.distance, Color.red);
            }
            else
            {
                rayDistances[i] = 1f;
                Debug.DrawRay(position, direction * rayLength, Color.green);
            }
        }

        Vector2 velocity = rb.linearVelocity;

        speed = velocity.magnitude;
        forwardSpeed = Vector2.Dot(velocity, transform.up);
        sidewaysSpeed = Vector2.Dot(velocity, transform.right);

        if (Mathf.Abs(speed) < 0.001f) speed = 0f;
        if (Mathf.Abs(forwardSpeed) < 0.001f) forwardSpeed = 0f;
        if (Mathf.Abs(sidewaysSpeed) < 0.001f) sidewaysSpeed = 0f;
    }

    public float[] GetSensorData()
    {
        return new float[]
        {
            rayDistances[0], rayDistances[1], rayDistances[2],
            rayDistances[3],
            rayDistances[4], rayDistances[5], rayDistances[6],

            Mathf.Clamp(forwardSpeed / maxSpeed, -1f, 1f),
            Mathf.Clamp(sidewaysSpeed / maxSpeed, -1f, 1f)
        };
    }
}