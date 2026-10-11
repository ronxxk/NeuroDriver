using UnityEngine;
using UnityEngine.InputSystem;

/// 2D top-down drift car. Sprite must face UP (+Y).
/// Keyboard: W/S throttle/brake, A/D steer, SPACE handbrake.
/// AI: set aiControlled = true and call SetAIInput() every FixedUpdate.
[RequireComponent(typeof(Rigidbody2D))]
public class CarController2D : MonoBehaviour
{
    [Header("Engine")]
    public float acceleration = 7f;
    public float maxSpeed = 7f;
    public float reverseSpeed = 2.5f;
    public float brakePower = 11f;
    public float coastDrag = 2f;

    [Header("Steering")]
    public float turnSpeed = 170f;       // max deg/sec when gripping
    public float steerRamp = 4f;
    public float steerReturn = 8f;
    public float steerFullSpeed = 2f;    // below this speed steering is weaker

    [Header("Grip (per second)")]
    public float normalGrip = 14f;
    public float driftGrip = 0.8f;
    public float sustainGrip = 2.5f;
    public float gripRecovery = 2.5f;
    [Range(0f, 1f)] public float powerslide = 0.4f;
    public float sustainAngle = 10f;
    public float maxSlipAngle = 65f;

    [Header("Drift Feel")]
    public float driftTurnScale = 1.25f;
    public float handbrakeOversteer = 70f;
    public float yawAccel = 900f;
    public float driftYawAccel = 380f;
    public float driftSpeedLoss = 0.15f;
    public float driftThreshold = 1.25f;

    [Header("Control")]
    public bool aiControlled;

    [Header("Effects (optional)")]
    public ParticleSystem[] smoke;

    public bool IsDrifting { get; private set; }
    public bool HandbrakeHeld { get; private set; }
    public float ForwardSpeed { get; private set; }
    public float SlipAngle { get; private set; }

    Rigidbody2D rb;
    float throttle, rawSteer, steer, yawRate, currentGrip;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        currentGrip = normalGrip;
    }


    /// AI input: throttle -1..1, steer -1..1 (positive = left), handbrake on/off.
    public void SetAIInput(float throttleIn, float steerIn, bool handbrake)
    {
        throttle = Mathf.Clamp(throttleIn, -1f, 1f);
        rawSteer = Mathf.Clamp(steerIn, -1f, 1f);
        HandbrakeHeld = handbrake;
    }
    public void ResetControl()
    {
        throttle = rawSteer = steer = yawRate = 0f;
        currentGrip = normalGrip;
        HandbrakeHeld = false;
        IsDrifting = false;
    }

    void Update()
    {
        if (aiControlled) return;

        var k = Keyboard.current;
        if (k == null) return;

        throttle = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
        rawSteer = (k.aKey.isPressed ? 1f : 0f) - (k.dKey.isPressed ? 1f : 0f);
        HandbrakeHeld = k.spaceKey.isPressed;
    }

    static void Basis(float angleDeg, out Vector2 fwd, out Vector2 right)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        fwd = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
        right = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 vel = rb.linearVelocity;

        // Slip state with the current heading
        Basis(rb.rotation, out Vector2 fwd0, out Vector2 right0);
        float fwd0Speed = Vector2.Dot(vel, fwd0);
        float lat0Speed = Vector2.Dot(vel, right0);
        float speed = vel.magnitude;
        float speedPct = Mathf.Clamp01(speed / maxSpeed);
        float slipAngle = Mathf.Atan2(lat0Speed, Mathf.Max(Mathf.Abs(fwd0Speed), 0.05f)) * Mathf.Rad2Deg;
        SlipAngle = slipAngle;
        float absSlip = Mathf.Abs(slipAngle);

        // Target grip
        float targetGrip = normalGrip;
        if (HandbrakeHeld)
            targetGrip = Mathf.Lerp(normalGrip, driftGrip, Mathf.Clamp01(speed / (maxSpeed * 0.35f)));
        else if (throttle > 0f && absSlip > sustainAngle && speedPct > 0.25f)
            targetGrip = sustainGrip;
        else if (throttle > 0f)
            targetGrip = Mathf.Lerp(normalGrip, sustainGrip, powerslide * speedPct * speedPct * Mathf.Abs(steer));

        float gripRate = targetGrip < currentGrip ? 12f : gripRecovery;
        currentGrip = Mathf.Lerp(currentGrip, targetGrip, gripRate * dt);
        float driftAmount = Mathf.InverseLerp(normalGrip, driftGrip, currentGrip);

        // Steering
        float steerRateIn = Mathf.Approximately(rawSteer, 0f) ? steerReturn : steerRamp;
        steer = Mathf.MoveTowards(steer, rawSteer, steerRateIn * dt);

        float authority = Mathf.Clamp01(speed / steerFullSpeed);
        float dir = fwd0Speed >= -0.05f ? 1f : -1f;
        float turn = turnSpeed * Mathf.Lerp(1f, 0.65f, speedPct) * Mathf.Lerp(1f, driftTurnScale, driftAmount);
        if (HandbrakeHeld) turn += handbrakeOversteer * speedPct;
        float targetYaw = steer * turn * authority * dir;

        yawRate = Mathf.MoveTowards(yawRate, targetYaw, Mathf.Lerp(yawAccel, driftYawAccel, driftAmount) * dt);

        // Stop endless spinning
        if (fwd0Speed > 0.5f && Mathf.Sign(slipAngle) * yawRate > 0f)
            yawRate *= 1f - Mathf.InverseLerp(maxSlipAngle * 0.75f, maxSlipAngle, absSlip);

        float newRot = rb.rotation + yawRate * dt;
        rb.MoveRotation(newRot);

        // Velocity uses the new heading
        Basis(newRot, out Vector2 fwd, out Vector2 right);
        float fwdSpeed = Vector2.Dot(vel, fwd);
        float latSpeed = Vector2.Dot(vel, right);

        // Engine
        if (throttle > 0f)
        {
            if (fwdSpeed < maxSpeed) fwdSpeed += acceleration * dt;
        }
        else if (throttle < 0f)
        {
            if (fwdSpeed > 0.25f) fwdSpeed -= brakePower * dt;
            else if (fwdSpeed > -reverseSpeed) fwdSpeed -= acceleration * 0.6f * dt;
        }
        else
        {
            fwdSpeed = Mathf.MoveTowards(fwdSpeed, 0f, coastDrag * dt);
        }

        if (HandbrakeHeld) fwdSpeed = Mathf.MoveTowards(fwdSpeed, 0f, 2f * dt);

        float speedBefore = Mathf.Sqrt(fwdSpeed * fwdSpeed + latSpeed * latSpeed);

        // Tires kill sideways velocity
        latSpeed *= Mathf.Exp(-currentGrip * dt);
        Vector2 newVel = fwd * fwdSpeed + right * latSpeed;

        // Gripping converts sideways speed to forward speed; sliding doesn't
        float mag = newVel.magnitude;
        if (mag > 0.001f)
        {
            float keep = 0.92f * (1f - driftAmount);
            float targetMag = Mathf.Lerp(mag, speedBefore, keep);
            targetMag = Mathf.Max(0f, targetMag - Mathf.Abs(latSpeed) * driftSpeedLoss * dt);
            newVel *= targetMag / mag;
        }

        rb.linearVelocity = newVel;
        ForwardSpeed = Vector2.Dot(newVel, fwd);
        IsDrifting = Mathf.Abs(Vector2.Dot(newVel, right)) > driftThreshold && newVel.magnitude > 1f;

        foreach (var p in smoke)
        {
            if (!p) continue;
            var e = p.emission;
            e.enabled = IsDrifting;
            if (IsDrifting && !p.isPlaying) p.Play();
        }
    }
}