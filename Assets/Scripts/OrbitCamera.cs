using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitCamera: MonoBehaviour
{
    [SerializeField]
    Transform focus = default;

    [SerializeField, Range(1f, 20f)]
    float distance = 5f;

    [SerializeField, Min(0f)]
    float focusRadius = 1f;

    [SerializeField, Range(0f, 1f)]
    float focusCentering = 0.5f;

    [SerializeField, Range(1f, 360f)]
    float rotationSpeed = 90f;

    [SerializeField, Range(-89f, 89f)]
    float minVerticalAngle = -30f, maxVerticalAngle = 60f;

    [SerializeField, Min(0f)]
    float alignDelay = 5f;

    [SerializeField, Range(0f, 90f)]
    float alignSmoothRange = 45f;

    [SerializeField, Min(0f)]
    float upAlignmentSpeed = 360f;

    [SerializeField]
    LayerMask obstructionMask = -1;

    Vector3 focusPoint, previousFocusPoint;

    Vector2 orbitAngles = new Vector2(45f, 0f);

    float lastManualRotationTime;

    Camera regularCamera;

    Quaternion gravityAlignment = Quaternion.identity;

    Quaternion orbitRotation;

    Vector3 CameraHalfExtends {
        get {
            Vector3 halfExtends;
            halfExtends.y = regularCamera.nearClipPlane * Mathf.Tan(0.5f * Mathf.Deg2Rad * regularCamera.fieldOfView);
            halfExtends.x = halfExtends.y * regularCamera.aspect;
            halfExtends.z = 0f;
            return halfExtends;
        }
    }

    void OnValidate() {
        if (maxVerticalAngle < minVerticalAngle) {
            maxVerticalAngle = minVerticalAngle;
        }
    }

    void Awake() {
        regularCamera = GetComponent<Camera>();
        focusPoint = focus.position;
        transform.localRotation = orbitRotation = Quaternion.Euler(orbitAngles);
    }

    void LateUpdate() {
        UpdateGravityAlignment();
        UpdateFocusPoint();
        if (ManualRotation() || AutomaticRotation()) {
            ConstrainAngles();
            orbitRotation = Quaternion.Euler(orbitAngles);
        }
        var lookRotation = gravityAlignment * orbitRotation;
        var lookDirection = lookRotation * Vector3.forward;
        var lookPosition = focusPoint - lookDirection * distance;

        var rectOffset = lookDirection * regularCamera.nearClipPlane;
        var rectPosition = lookPosition + rectOffset;
        var castFrom = focus.position;
        var castLine = rectPosition - castFrom;
        var castDistance = castLine.magnitude;
        var castDirection = castLine / castDistance;

        if (Physics.BoxCast(
            castFrom, CameraHalfExtends, castDirection, out RaycastHit hit, lookRotation, castDistance, obstructionMask, QueryTriggerInteraction.Ignore
        )) {
            rectPosition = castFrom + castDirection * hit.distance;
            lookPosition = rectPosition - rectOffset;
        }

        transform.SetPositionAndRotation(lookPosition, lookRotation);
    }
    
    void UpdateGravityAlignment() {
        var fromUp = gravityAlignment * Vector3.up;
        var toUp = CustomGravity.GetUpAxis(focusPoint);
        var dot = Mathf.Clamp(Vector3.Dot(fromUp, toUp), -1f, 1f);
        var angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        var maxAngle = upAlignmentSpeed * Time.deltaTime;

        var newAlignment = Quaternion.FromToRotation(fromUp, toUp) * gravityAlignment;
        if (angle < maxAngle) {
            gravityAlignment = newAlignment;
        }
        else {
            gravityAlignment = Quaternion.SlerpUnclamped(gravityAlignment, newAlignment, maxAngle / angle);
        }
    }

    void UpdateFocusPoint() {
        previousFocusPoint = focusPoint;
        var targetPoint = focus.position;
        if (focusRadius > 0f) {
            var distance = Vector3.Distance(targetPoint, focusPoint);
            float t = 1f;
            if (distance > 0.01f && focusCentering > 0f) {
                t = Mathf.Pow(1f - focusCentering, Time.unscaledDeltaTime);
            }
            if (distance > focusRadius) {
                t = Mathf.Min(t, focusRadius / distance);
            }
            focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);
        }
        else {
            focusPoint = targetPoint;
        }
    }

    bool ManualRotation() {
        Vector2 input = new Vector2(
            Input.GetAxis("Vertical Camera"),
            Input.GetAxis("Horizontal Camera")
        );
        const float e = 0.001f;
        if (input.x < -e || input.x > e || input.y < -e || input.y > e) {
            orbitAngles += rotationSpeed * Time.unscaledDeltaTime * input;
            lastManualRotationTime = Time.unscaledTime;
            return true;
        }
        return false;
    }

    static float GetAngle(Vector2 direction) {
        var angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
        return direction.x < 0f ? 360f - angle : angle;
    }

    bool AutomaticRotation() {
        if (Time.unscaledTime - lastManualRotationTime < alignDelay) {
            return false;
        }

        var alignedDelta = Quaternion.Inverse(gravityAlignment) * (focusPoint - previousFocusPoint);
        var movement = new Vector2(alignedDelta.x, alignedDelta.z);
        var movementDeltaSqr = movement.sqrMagnitude;
        if (movementDeltaSqr < 0.0001f) {
            return false;
        }

        var headingAngle = GetAngle(movement / Mathf.Sqrt(movementDeltaSqr));
        var deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y, headingAngle));
        var rotationChange = rotationSpeed * Mathf.Min(Time.unscaledDeltaTime, movementDeltaSqr);
        if (deltaAbs < alignSmoothRange) {
            rotationChange *= deltaAbs / alignSmoothRange;
        }
        else if (180f - deltaAbs < alignSmoothRange) {
            rotationChange *= (180f - deltaAbs) / alignSmoothRange;
        }
        orbitAngles.y = Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange);

        return true;
    }

    void ConstrainAngles() {
        orbitAngles.x = Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);
        if (orbitAngles.y < 0f) {
            orbitAngles.y += 360f;
        }
        else if (orbitAngles.y >= 360f) {
            orbitAngles.y -= 360f;
        }
    }


}
