using UnityEngine;

public abstract class State
{
    protected StateMachine fsm;

    public virtual void OnEnter() { }
    public virtual void OnExit() { }
    // 입력 처리(선택)
    public virtual void HandleInput() { }
    // 프레임 갱신
    public virtual void Tick() { }
    // 물리 갱신(캐릭터컨트롤러 Move 호출 전후용)
    public virtual void FixedTick() { }

    public void SetMachine(StateMachine machine) => fsm = machine;
}

public class StateMachine : MonoBehaviour
{
    public State Current { get; private set; }

    public void ChangeState(State next)
    {
        if (Current == next) return;
        Current?.OnExit();
        Current = next;
        Current.SetMachine(this);
        Current.OnEnter();
    }

    void Update()
    {
        Current?.HandleInput();
        Current?.Tick();
    }

    void FixedUpdate()
    {
        Current?.FixedTick();
    }
}
