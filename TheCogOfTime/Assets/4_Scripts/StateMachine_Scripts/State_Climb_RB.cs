using UnityEngine;

public class State_Climb_RB : State
{
    private readonly CharacterMotor m;
    Vector3 wallNormal, wallPoint;

    const float INPUT_DEADZONE = 0.2f;

    // ★ 진입 시점의 Y를 저장해서 '대기' 동안 고정
    float anchorY;

    public State_Climb_RB(CharacterMotor motor) { m = motor; }

    public override void OnEnter()
    {
        m.rb.useGravity = false;
        m.rb.linearVelocity = Vector3.zero;

        if (!m.ProbeWall(out var hit, out wallNormal, out wallPoint))
        {
            m.fsm.ChangeState(m.stAirborne);
            return;
        }

        // ★ 현재 높이를 앵커로 저장
        anchorY = m.rb.position.y;

        // 초기는 이동 없이 'Y 고정 스냅'
        m.SnapToWall(wallPoint, wallNormal, lockY: true, lockedY: anchorY);
    }

    public override void HandleInput()
    {
        // 수동 이탈(X)
        if (Input.GetKeyDown(KeyCode.X))
        {
            ExitToAir(backHop: true);
            return;
        }

        // 벽이 사라지면 자동 이탈
        if (!m.ProbeWall(out var hit, out wallNormal, out wallPoint))
        {
            ExitToAir();
            return;
        }

        // 입력 읽기
        Vector2 mv = m.input.move;
        bool moveUp = Mathf.Abs(mv.y) > INPUT_DEADZONE;
        bool moveSide = Mathf.Abs(mv.x) > INPUT_DEADZONE;
        bool hasInput = moveUp || moveSide;

        if (!hasInput)
        {
            // ===== 등반 대기 =====
            // 속도 0, Y 고정한 채로 벽에만 붙여둠
            m.rb.linearVelocity = Vector3.zero;
            m.SnapToWall(wallPoint, wallNormal, lockY: true, lockedY: anchorY);
            return;
        }

        // ===== 등반 이동 (입력이 있을 때만) =====
        float upSpeed = moveUp ? mv.y * m.climbUpSpeed : 0f;
        float sideSpeed = moveSide ? mv.x * m.climbSideSpeed : 0f;

        Vector3 lateral = Vector3.Cross(Vector3.up, wallNormal).normalized;
        Vector3 climbVel = Vector3.up * upSpeed + lateral * sideSpeed;

        // 이동 적용
        Vector3 next = m.rb.position + climbVel * Time.fixedDeltaTime;
        m.rb.MovePosition(next);

        // ★ 세로 입력 있을 때만 앵커Y 갱신
        if (moveUp)
            anchorY = next.y;

        // 거리/회전 유지 (Y는 입력 여부에 따라)
        m.SnapToWall(wallPoint, wallNormal, lockY: !moveUp, lockedY: anchorY);

        // 맨틀(턱 넘기): 'W'로 올릴 때만 허용
        if (mv.y > INPUT_DEADZONE && m.ProbeLedgeTop(wallNormal, out Vector3 top))
        {
            if (top.y > m.rb.position.y + 0.4f)
            {
                Mantle(top, wallNormal);
                return;
            }
        }

        // (선택) 점프로 이탈
        if (m.input.jumpPressed)
        {
            ExitToAir(backHop: true);
        }
    }

    public override void FixedTick()
    {
        m.rb.useGravity = false;
    }

    public override void OnExit()
    {
        m.rb.useGravity = true;
    }

    void Mantle(Vector3 top, Vector3 normal)
    {
        // W 입력으로 위로 갈 때만 맨틀
        Vector3 up = Vector3.up * m.mantleUpDistance;
        Vector3 fw = (-normal).normalized * m.mantleForwardDistance;
        m.rb.position = top + up + fw;
        m.rb.linearVelocity = Vector3.zero;

        // 맨틀 후 앵커Y 갱신 (지면 높이로)
        anchorY = m.rb.position.y;

        m.fsm.ChangeState(m.stGrounded);
    }

    void ExitToAir(bool backHop = false)
    {
        if (backHop)
        {
            Vector3 push = wallNormal * 3.5f + Vector3.up * 2.0f;
            m.rb.linearVelocity = Vector3.zero;
            m.rb.AddForce(push, ForceMode.VelocityChange);
        }
        m.fsm.ChangeState(m.stAirborne);
    }
}
