using UnityEngine;



[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(StateMachine))]
[RequireComponent(typeof(CharacterInput))]
public class CharacterMotor : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;

    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public CharacterInput input;
    [HideInInspector] public StateMachine fsm;

    [Header("Movement")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6.5f;
    public float acceleration = 14f;
    public float rotationLerp = 12f;
    public float airControl = 0.5f;

    [Header("Jump/Gravity")]
    public float jumpForce = 7f;
    public float groundCheckDistance = 0.2f;
    public LayerMask groundMask;

    [Header("Runtime")]
    public bool isGrounded;
    public Vector3 moveDir;
    public Vector3 planarVel;

    // States
    [HideInInspector] public State_Grounded_RB stGrounded;
    [HideInInspector] public State_Airborne_RB stAirborne;

    // ==== ?? CLIMB: 새 상태 참조 ====
    [HideInInspector] public State_Climb_RB stClimb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        input = GetComponent<CharacterInput>();
        fsm = GetComponent<StateMachine>();

        stGrounded = new State_Grounded_RB(this);
        stAirborne = new State_Airborne_RB(this);
        stClimb = new State_Climb_RB(this); // ★ 등반 상태 생성
    }

    void Start()
    {
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        fsm.ChangeState(stGrounded);
    }

    // ===== Ground Check =====
    [Header("Ground Check")]
    public CapsuleCollider capsule;      // Inspector에 할당
    public float groundPadding = 0.05f;

    public bool CheckGrounded()
    {
        if (!capsule)
            return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down,
                                   groundCheckDistance + 0.1f, groundMask);

        float radius = capsule.radius * Mathf.Abs(transform.localScale.x);
        float height = (capsule.height * Mathf.Abs(transform.localScale.y)) - 2f * radius;
        Vector3 center = capsule.transform.TransformPoint(capsule.center);

        Vector3 top = center + Vector3.up * (height * 0.5f);
        Vector3 bottom = center - Vector3.up * (height * 0.5f);

        return Physics.CheckCapsule(top, bottom - Vector3.up * groundPadding,
                                    radius - 0.01f, groundMask, QueryTriggerInteraction.Ignore);
    }

    public Vector3 CameraRelativeXZ(Vector2 inputDir)
    {
        Vector3 fwd = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized : Vector3.right;
        Vector3 dir = right * inputDir.x + fwd * inputDir.y;
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    public void ApplyRotation()
    {
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationLerp * Time.deltaTime);
        }
    }

    // ===== ?? CLIMB SETTINGS & UTILITIES =====
    [Header("Climb Settings")]
    public LayerMask climbableMask;       // 등반 가능 벽 레이어
    public float maxGrabDistance = 0.9f;  // 벽까지 최대 감지 거리
    public float maxGrabAngle = 60f;      // 전방과 벽 노멀 각 허용치
    public float wallSnapSpeed = 18f;     // 벽에 들러붙는 스냅 속도
    public float wallStickDistance = 0.35f;// 캐릭터-벽 간 유지 거리
    public float climbUpSpeed = 2.2f;     // 위/아래 속도
    public float climbSideSpeed = 2.8f;   // 좌/우 속도
    public float ledgeProbeUp = 1.2f;     // 머리 위 ledge 탐색 높이
    public float ledgeProbeForward = 0.5f;// ledge 앞 여유
    public float mantleUpDistance = 1.0f; // 턱 넘기 상승량
    public float mantleForwardDistance = 0.5f; // 턱 넘기 전진량

    // 벽 감지(자동 진입용): 전방 SphereCast
    public bool ProbeWall(out RaycastHit hit, out Vector3 wallNormal, out Vector3 hitPoint)
    {
        Vector3 origin = transform.position + Vector3.up * (capsule ? Mathf.Max(1.0f, capsule.height * 0.5f) : 1.0f);
        Vector3 forward = transform.forward;
        if (Physics.SphereCast(origin, 0.2f, forward, out hit, maxGrabDistance, climbableMask, QueryTriggerInteraction.Ignore))
        {
            wallNormal = hit.normal;
            hitPoint = hit.point;
            float ang = Vector3.Angle(-forward, wallNormal); // 정면일수록 0°
            return ang <= maxGrabAngle;
        }
        wallNormal = Vector3.zero; hitPoint = Vector3.zero; return false;
    }

    // ledge 상단 탐지(위로 레이 쏴서 턱 확인)
    public bool ProbeLedgeTop(Vector3 wallNormal, out Vector3 topPoint)
    {
        Vector3 start = transform.position + Vector3.up * ledgeProbeUp - wallNormal * ledgeProbeForward;
        if (Physics.Raycast(start, Vector3.down, out var hit, ledgeProbeUp + 1.0f, climbableMask | groundMask, QueryTriggerInteraction.Ignore))
        {
            topPoint = hit.point;
            return true;
        }
        topPoint = Vector3.zero; return false;
    }

    // 벽에 스냅(거리 유지 + 벽 바라보게)
    public void SnapToWall(Vector3 wallPoint, Vector3 wallNormal)
    {
        Vector3 targetPos = wallPoint + wallNormal * wallStickDistance;
        targetPos.y = Mathf.Lerp(transform.position.y, targetPos.y, 0.6f);
        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, wallSnapSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        Quaternion look = Quaternion.LookRotation(-wallNormal, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, 12f * Time.fixedDeltaTime));
    }

    public void SnapToWall(Vector3 wallPoint, Vector3 wallNormal, bool lockY, float lockedY)
    {
        // 기본 목표 위치
        Vector3 targetPos = wallPoint + wallNormal * wallStickDistance;

        // ★ 입력이 없을 때는 Y를 고정
        if (lockY) targetPos.y = lockedY;
        else targetPos.y = Mathf.Lerp(transform.position.y, targetPos.y, 0.6f);

        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, wallSnapSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        // 벽을 바라보게 회전
        Quaternion look = Quaternion.LookRotation(-wallNormal, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, 12f * Time.fixedDeltaTime));
    }
}