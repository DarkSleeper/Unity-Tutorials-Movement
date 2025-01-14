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
    float maxGroundAngle = 25f, maxStairsAngle = 50f;

    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f;

    [SerializeField, Min(0f)]
    float probeDistance = 1f;

    [SerializeField]
    LayerMask probeMask = -1, stairsMask = -1;

    Rigidbody body;

    Vector3 velocity, desiredVelocity;

    bool desiredJump;

    int jumpPhase;

    float minGropundDotProduct, minStairsDotProduct;

    Vector3 contactNormal, steepNormal;

    int groundContactCount, steepContactCount;

    bool OnGround => groundContactCount > 0;

    bool OnSteep => steepContactCount > 0;

    int stepsSinceLastGrounded, stepsSinceLastJump;

    float getMinDot(int layer) {
        return (stairsMask & (1 << layer)) == 0 ?
            minGropundDotProduct : minStairsDotProduct;
    }

    void OnValidate() {
        minGropundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
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
        float minDot = getMinDot(collision.gameObject.layer);
        for (int i = 0; i < collision.contactCount; i++) {
            Vector3 normal = collision.GetContact(i).normal;
            if (normal.y >= minDot) {
                groundContactCount += 1;
                contactNormal += normal;
            } else if (normal.y > -0.01f) {
                steepContactCount += 1;
                steepNormal += normal;
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

        GetComponent<Renderer>().material.SetColor(
            "_Color", OnGround ? Color.black : Color.white
        );
    }

    void FixedUpdate() {
        UpdateState();
        AdjustVelocity();

        if (desiredJump) {
            desiredJump = false;
            Jump();
        }

        body.velocity = velocity;
        ClearState();
    }

    void ClearState() {
        groundContactCount = steepContactCount = 0;
        contactNormal = steepNormal = Vector3.zero;
    }

    void UpdateState() {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity;
        if (OnGround || SnapToGround() || CheckSteepContacts()) {
            stepsSinceLastGrounded = 0;
            if (stepsSinceLastJump > 1) {
                jumpPhase = 0;
            }
            if (groundContactCount > 1) {
                contactNormal.Normalize();
            }
        }
        else {
            contactNormal = Vector3.up;
        }
    }

    void Jump() {
        Vector3 jumpDirection;
        if (OnGround) {
            jumpDirection = contactNormal;
        } else if (OnSteep) {
            jumpDirection = steepNormal;
            jumpPhase = 0;
        } else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps) {
            if (jumpPhase == 0) {
                jumpPhase = 1;
            }
            jumpDirection = contactNormal;
        } else {
            return;
        }
        
        stepsSinceLastJump = 0;
        jumpPhase += 1;
        var jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
        jumpDirection = (jumpDirection + Vector3.up).normalized;
        var alignedSpeed = Vector3.Dot(velocity, jumpDirection);
        if (alignedSpeed > 0f) {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
        }
        velocity += jumpDirection * jumpSpeed;
            
    }

    bool SnapToGround() {
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2) {
            return false;
        }
        var speed = velocity.magnitude;
        if (speed > maxSnapSpeed) {
            return false;
        }
        if (!Physics.Raycast(
            body.position, Vector3.down, out RaycastHit hit, probeDistance, probeMask
        )) {
            return false;
        }
        if (hit.normal.y < getMinDot(hit.collider.gameObject.layer)) {
            return false;
        }
        groundContactCount = 1;
        contactNormal = hit.normal;
        var dot = Vector3.Dot(velocity, hit.normal);
        if (dot > 0f) {
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }
        return true;
    }

    bool CheckSteepContacts() {
        if (steepContactCount > 1) {
            steepNormal.Normalize();
            if (steepNormal.y >= minGropundDotProduct) {
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }
        return false;
    }
}
