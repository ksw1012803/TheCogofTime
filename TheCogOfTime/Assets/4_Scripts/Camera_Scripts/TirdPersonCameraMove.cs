using UnityEngine;

public partial class TirdPersonCameraMove : MonoBehaviour
{
    [Header("Target")]
    public Transform target;             // 따라갈 캐릭터(머리나 몸 중심)
    public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f); // 캐릭터 머리 높이 등

    [Header("Orbit")]
    public float mouseXSensitivity = 180f;   // deg/sec
    public float mouseYSensitivity = 120f;   // deg/sec
    public bool invertY = false;
    public float minPitch = -30f;
    public float maxPitch = 70f;

    [Header("Distance")]
    public float distance = 4.0f;        // 기본 거리(줌 아웃 값)
    public float minDistance = 0.5f;     // 충돌 시 밀려 들어갈 최소 거리
    public float maxDistance = 6.0f;     // 휠로 확대/축소 허용 최대
    public float zoomScrollSpeed = 3.0f; // 마우스 휠 줌 속도

    [Header("Collision (Push-In)")]
    public LayerMask obstructionMask;    // 환경/지형 레이어 지정
    public float collisionRadius = 0.2f; // 카메라 주변 체크 반경(스피어캐스트)
    public float collisionOffset = 0.1f; // 여유 거리(벽에 박히지 않게)
    public float collisionLerp = 20f;    // 충돌 반응 스무딩(가까워질 때)
    public float returnLerp = 6f;        // 복귀(멀어질 때) 스무딩

    [Header("Misc")]
    public bool lockCursor = true;       // 플레이 중 마우스 커서 잠금
    public float yaw;                    // 디버그용 현재 각도
    public float pitch;

    float currentDistance;

    void Start()
    {
        currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 초기 yaw/pitch를 현 카메라 기준으로 세팅(선택)
        if (target)
        {
            Vector3 toCam = (transform.position - (target.position + targetOffset));
            if (toCam.sqrMagnitude > 0.001f)
            {
                Vector3 flat = Vector3.ProjectOnPlane(toCam, Vector3.up);
                yaw = Quaternion.LookRotation(flat, Vector3.up).eulerAngles.y;
                float flatMag = flat.magnitude;
                pitch = Mathf.Rad2Deg * Mathf.Atan2(toCam.y, flatMag);
            }
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        // 1) 입력: 마우스 회전 & 줌
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        yaw += mouseX * mouseXSensitivity * Time.deltaTime;
        pitch += (invertY ? mouseY : -mouseY) * mouseYSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        distance = Mathf.Clamp(distance - scroll * zoomScrollSpeed, minDistance, maxDistance);

        // 2) 회전 행렬
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

        // 3) 이상적 카메라 위치(충돌 고려 전)
        Vector3 focus = target.position + targetOffset;
        Vector3 desiredPos = focus - (rot * Vector3.forward) * distance;

        // 4) 충돌 검사(스피어 캐스트로 장애물에 닿으면 앞으로 ‘밀림’)
        float desiredDist = distance;
        float hitDist = desiredDist;

        if (Physics.SphereCast(focus, collisionRadius, (desiredPos - focus).normalized, out RaycastHit hit, desiredDist, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            hitDist = Mathf.Max(minDistance, hit.distance - collisionOffset);
        }

        // 5) 스무딩: 가까워질 때는 빠르게(collisionLerp), 멀어질 때는 천천히(returnLerp)
        float targetDist = Mathf.Min(desiredDist, hitDist);
        bool isColliding = targetDist < desiredDist - 0.001f;

        float lerp = (isColliding ? collisionLerp : returnLerp) * Time.deltaTime;
        currentDistance = Mathf.Lerp(currentDistance, targetDist, lerp);
        currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

        // 6) 최종 위치/회전 적용
        Vector3 finalPos = focus - (rot * Vector3.forward) * currentDistance;

        transform.SetPositionAndRotation(finalPos, rot);

        // 7) (선택) 캐릭터가 벽에 가까울 때 카메라 흔들림 방지용 소폭 보정
        // 필요시 Raycast로 카메라-포커스 사이 투명 처리(See-Through) 등을 구현 가능
    }

}