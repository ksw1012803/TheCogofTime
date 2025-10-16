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

    static float DeadzoneSigned(float v, float dz) => (Mathf.Abs(v) < dz) ? 0f : v;

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
        float upInput = DeadzoneSigned(mv.y, INPUT_DEADZONE);   // W=+, S=−
        float sideInput = DeadzoneSigned(mv.x, INPUT_DEADZONE);   // D=+, A=−
        bool hasInput = (upInput != 0f) || (sideInput != 0f);

        if (!hasInput)
        {
            // ===== 등반 대기 =====
            // 속도 0, Y 고정한 채로 벽에만 붙여둠
            m.rb.linearVelocity = Vector3.zero;
            m.SnapToWall(wallPoint, wallNormal, lockY: true, lockedY: anchorY);
            return;
        }

        // === 등반 이동 ===
        float upSpeed = upInput * m.climbUpSpeed;   // +위 / −아래
        float sideSpeed = sideInput * m.climbSideSpeed; // +오른쪽 / −왼쪽

        Vector3 lateral = Vector3.Cross(Vector3.up, wallNormal).normalized;
        Vector3 climbVel = Vector3.up * upSpeed + lateral * sideSpeed;

        Vector3 next = m.rb.position + climbVel * Time.fixedDeltaTime;
        m.rb.MovePosition(next);

        // ★ 위/아래 어느 방향이든, 실제 이동 높이를 앵커로 갱신
        if (upInput != 0f) anchorY = next.y;

        // ★ 스냅 시 Y를 '현재 이동 높이'에 고정해, S(아래) 입력이 스냅에 의해 취소되지 않게 함
        m.SnapToWall(wallPoint, wallNormal, lockY: true, lockedY: next.y);

        // 맨틀: 위(+Y) 입력일 때만
        if (upInput > 0f && m.ProbeLedgeTop(wallNormal, out Vector3 top))
        {
            if (top.y > m.rb.position.y + 0.4f) { Mantle(top, wallNormal); return; }
        }

        // (선택) 점프로 이탈
        if (m.input.jumpPressed) ExitToAir(backHop: true);
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
        // 재진입 쿨다운 시작
        m.nextClimbAllowedTime = Time.time + m.climbReenterBlock;

        // 벽에서 즉시 떼기(ProbeWall 범위 밖으로 이동)
        m.EnsureClearFromWall(wallNormal);

        // 속도 초기화 후, 충분히 큰 반발력 추가
        m.rb.linearVelocity = Vector3.zero;

        if (backHop)
        {
            Vector3 impulse = wallNormal * m.exitBackHopForce + Vector3.up * m.exitBackHopUpForce;
            m.rb.AddForce(impulse, ForceMode.VelocityChange);
        }

        // 공중 상태로 전환
        m.fsm.ChangeState(m.stAirborne);
    }
}
