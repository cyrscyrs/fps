using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine
{
    public ZombieState currentState { get; private set; }

    public void Initialize(ZombieState _startState)
    {
        currentState = _startState;
        currentState.Enter();
    }
    public void ChangeState(ZombieState _newState)
    {
        currentState.Exit();
        currentState = _newState;
        currentState.Enter();
    }
}
