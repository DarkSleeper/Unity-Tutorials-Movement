using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class StableFloatingRigidBody : MonoBehaviour
{
    Rigidbody body;

    float floatDelay;

    [SerializeField]
    bool floatToSleep = false;

    [SerializeField]
    float submergenceOffset = 0.5f;

    [SerializeField, Min(0.1f)]
    float submergenceRange = 1f;

    [SerializeField, Min(0f)]
    float buoyancy = 1f;

    [SerializeField, Range(0f, 10f)]
    float waterDrag = 1f;

    [SerializeField]
    LayerMask waterMask = 0;

    [SerializeField]
    Vector3[] buoyancyOffsets = default;

    [SerializeField]
    bool safeFloating = false;

    float[] submergence;

    Vector3 gravity;

    void Awake() {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        floatToSleep = true;
        submergence = new float[buoyancyOffsets.Length];
    }

    void OnTriggerEnter(Collider other) {
        if ((waterMask & (1 << other.gameObject.layer)) != 0) {
            EvaluateSubmergence(other);
        }
    }
    
    void OnTriggerStay(Collider other) {
        if (!body.IsSleeping() && (waterMask & (1 << other.gameObject.layer)) != 0) {
            EvaluateSubmergence(other);
        }
    }

    void EvaluateSubmergence(Collider collider) {
        var down = gravity.normalized;
        var offset = down * -submergenceOffset;
        for (int i = 0; i < buoyancyOffsets.Length; i++) {
            var p = offset + transform.TransformPoint(buoyancyOffsets[i]);
            if (Physics.Raycast(
                p, down, out RaycastHit hit, submergenceRange + 1f,
                waterMask, QueryTriggerInteraction.Collide
            )) {
                submergence[i] = 1f - hit.distance / submergenceRange;
            }
            else if (
                !safeFloating || Physics.CheckSphere(
                    p, 0.01f, waterMask, QueryTriggerInteraction.Collide
                )
            ) {
                submergence[i] = 1f;
            }
        }
    }

    void FixedUpdate() {
        if (floatToSleep) {
            if (body.IsSleeping()) {
                floatDelay = 0f;
                return;
            }

            if (body.velocity.sqrMagnitude < 0.0001f) {
                floatDelay += Time.deltaTime;
                if (floatDelay >= 1f) {
                    return;
                }
            }
            else {
                floatDelay = 0f;
            }
        }

        gravity = CustomGravity.GetGravity(body.position);
        var dragFactor = waterDrag * Time.deltaTime / buoyancyOffsets.Length;
        var buoyancyFactor = -buoyancy / buoyancyOffsets.Length;
        for (int i = 0; i < buoyancyOffsets.Length; i++) {
            if (submergence[i] > 0f) {
                var drag = Mathf.Max(0f, 1f - dragFactor * submergence[i]);
                body.velocity *= drag;
                body.angularVelocity *= drag;
                body.AddForceAtPosition(
                    gravity * (buoyancyFactor * submergence[i]),
                    transform.TransformPoint(buoyancyOffsets[i]),
                    ForceMode.Acceleration
                );
                submergence[i] = 0f;
            }
        }
        body.AddForce(gravity, ForceMode.Acceleration);
    }
}
