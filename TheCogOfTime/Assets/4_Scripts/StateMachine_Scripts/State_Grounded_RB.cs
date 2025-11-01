using UnityEngine;


public class State_Grounded_RB : State
{

    private readonly CharacterMotor m;

    public State_Grounded_RB(CharacterMotor motor)
    {
        m = motor;
    }

    public override void OnEnter()
    {
        // 지면 안정화
        m.isGrounded = true;
    }

    public override void HandleInput()
    {
        bool wantForward = !m.requireForwardInputToReenter || m.input.move.y > 0.1f;

        if (m.CanAutoClimb() && wantForward && m.ProbeWall(out _, out _, out _))
        {
            m.fsm.ChangeState(m.stClimb);
            return;
        }

        m.isGrounded = m.CheckGrounded();

        if (!m.isGrounded)
        {
            m.fsm.ChangeState(m.stAirborne);
            return;
        }

        // 카메라 기준 이동
        Vector3 wishDir = m.CameraRelativeXZ(m.input.move);
        m.moveDir = wishDir;

        float targetSpeed = m.input.runHeld ? m.runSpeed : m.walkSpeed;

        Vector3 targetVel = wishDir * targetSpeed;
        Vector3 velocity = m.rb.linearVelocity;
        Vector3 velocityXZ = new Vector3(velocity.x, 0, velocity.z);

        // 가속보정
        Vector3 newVel = Vector3.MoveTowards(velocityXZ, targetVel, m.acceleration * Time.fixedDeltaTime);
        m.rb.linearVelocity = new Vector3(newVel.x, velocity.y, newVel.z);

        m.ApplyRotation();

        // 점프
        if (m.input.jumpPressed)
        {
            Vector3 vel = m.rb.linearVelocity;
            vel.y = 0;
            m.rb.linearVelocity = vel;
            m.rb.AddForce(Vector3.up * m.jumpForce, ForceMode.VelocityChange);
            m.fsm.ChangeState(m.stAirborne);
        }
    }

    public override void FixedTick()
    {
        // 착지 유지용
        if (!m.isGrounded)
            m.fsm.ChangeState(m.stAirborne);
    }
}