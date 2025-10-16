using UnityEngine;

public class State_Airborne_RB : State
{
    private readonly CharacterMotor m;

    public State_Airborne_RB(CharacterMotor motor)
    {
        m = motor;
    }

    public override void HandleInput()
    {
        if (m.ProbeWall(out _, out _, out _))
        {
            m.fsm.ChangeState(m.stClimb);
            return;
        }

        m.isGrounded = m.CheckGrounded();

        if (m.isGrounded && m.rb.linearVelocity.y <= 0f)
        {
            m.fsm.ChangeState(m.stGrounded);
            return;
        }

        // 공중 제어
        Vector3 wishDir = m.CameraRelativeXZ(m.input.move);
        m.moveDir = wishDir;
        float targetSpeed = m.input.runHeld ? m.runSpeed : m.walkSpeed;
        Vector3 velocity = m.rb.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

        Vector3 targetVel = wishDir * targetSpeed;
        Vector3 newVel = Vector3.MoveTowards(horizontal, targetVel, m.acceleration * m.airControl * Time.fixedDeltaTime);

        m.rb.linearVelocity = new Vector3(newVel.x, velocity.y, newVel.z);
        m.ApplyRotation();
    }
}