using UnityEngine;

public class CharacterInput : MonoBehaviour
{
    [HideInInspector] public Vector2 move;
    [HideInInspector] public bool runHeld;
    [HideInInspector] public bool jumpPressed;

    // ---- 입력 버퍼 (프레임 사이 입력 손실 방지) ----
    [Header("Input Buffers")]
    public float jumpBufferTime = 0.15f;
    public float exitBufferTime = 0.15f;

    float _jumpBufferCounter = -1f;
    float _exitBufferCounter = -1f;   // X키로 등반 이탈

    void Update()
    {
        // 이동/달리기(구 Input)
        move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        runHeld = Input.GetKey(KeyCode.LeftShift);

        // --- 점프 ---
        // 기존 호환 플래그(원하면 계속 사용 가능)
        jumpPressed = Input.GetButtonDown("Jump");
        if (jumpPressed)
            _jumpBufferCounter = jumpBufferTime;

        // --- 등반 이탈(X) ---
        if (Input.GetKeyDown(KeyCode.X))
            _exitBufferCounter = exitBufferTime;

        // 버퍼 시간 감소 (unscaled: 일시정지 등에서도 유지하려면 필요에 따라 Time.deltaTime로 바꿔도 OK)
        if (_jumpBufferCounter >= 0f) _jumpBufferCounter -= Time.unscaledDeltaTime;
        if (_exitBufferCounter >= 0f) _exitBufferCounter -= Time.unscaledDeltaTime;
    }

    /// <summary>
    /// 점프 입력을 1회 소비(버퍼 내에 있으면 true)
    /// 상태(State)에서 매 프레임 이걸 호출해 사용하세요.
    /// </summary>
    public bool ConsumeJump()
    {
        if (_jumpBufferCounter > 0f)
        {
            _jumpBufferCounter = -1f;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 등반 이탈(X) 입력을 1회 소비
    /// </summary>
    public bool ConsumeExitClimb()
    {
        if (_exitBufferCounter > 0f)
        {
            _exitBufferCounter = -1f;
            return true;
        }
        return false;
    }
}