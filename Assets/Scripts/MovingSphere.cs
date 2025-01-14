using UnityEngine;

public class MovingSphere : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f;

    [SerializeField, Range(0f, 100f)]
    float maxAcceleration = 10f, maxAirAcceleration = 1f;

    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;

    [SerializeField, Range(0, 5)]
    int maxAirJumps = 0;

    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f;

    Rigidbody body;

    Vector3 velocity, desiredVelocity;

    bool desiredJump;

    int groundContactCount;

    bool OnGround => groundContactCount > 0;

    int jumpPhase;

    float minGropundDotProduct;

    Vector3 contactNormal;

    void OnValidate() {
        minGropundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void Awake() {
        body = GetComponent<Rigidbody>();
        OnValidate();
    }

    void OnCollisionEnter(Collision collision) {
        EvaluateCollision(collision);
    }

    void OnCollisionStay(Collision collision) {
        EvaluateCollision(collision);
    }

    void EvaluateCollision(Collision collision) {
        for (int i = 0; i < collision.contactCount; i++) {
            Vector3 normal = collision.GetContact(i).normal;
            if (normal.y >= minGropundDotProduct) {
                groundContactCount += 1;
                contactNormal += normal;
            }
        }
    }

    void Update() {
        Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);
        desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;

        desiredJump |= Input.GetButtonDown("Jump");
    }

    Vector3 ProjectOnContactPlane(Vector3 vector) {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    void AdjustVelocity() {
        var xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        var zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        var currentX = Vector3.Dot(velocity, xAxis);
        var currentZ = Vector3.Dot(velocity, zAxis);
        
        var acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        var maxSpeedChange = acceleration * Time.deltaTime;
        var newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        var newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }

    void FixedUpdate() {
        UpdateState();
        AdjustVelocity();

        if (desiredJump) {
            desiredJump = false;
            Jump();
        }

        // GetComponent<Renderer>().material.SetColor(
        //     "_Color", Color.white * (groundContactCount * 0.25f)
        // );

        body.velocity = velocity;
        ClearState();
    }

    void ClearState() {
        groundContactCount = 0;
        contactNormal = Vector3.zero;
    }

    void UpdateState() {
        velocity = body.velocity;
        if (OnGround) {
            jumpPhase = 0;
            if (groundContactCount > 1) {
                contactNormal.Normalize();
            }
        }
        else {
            contactNormal = Vector3.up;
        }
    }

    void Jump() {
        if (OnGround || jumpPhase < maxAirJumps) {
            jumpPhase += 1;
            var jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            var alignedSpeed = Vector3.Dot(velocity, contactNormal);
            if (alignedSpeed > 0f) {
                jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
            }
            velocity += contactNormal * jumpSpeed;
        }
    }
}
