﻿using System;
using UnityEngine;

public abstract class PhysicsModel : MonoBehaviour
{
    [SerializeField]
    private MyNetworkView myNetworkView;



    protected virtual void Start()
    {
        MyComponents.PhysicsModelsManager.RegisterView(myNetworkView.ViewId, this);
    }

    public abstract void Simulate(short frameNumber, float dt, bool isRealSimulation);

    public abstract byte[] Serialize();

    public abstract int DeserializeAndRewind(short previousAcknowlegedFrame, short frameNumber, byte[] data, int offset);

    public abstract void CheckForPostSimulationActions();

    public abstract byte[] SerializeInputs(short frame);

    public abstract void CheckForPreSimulationActions();

    protected void OnDestroy()
    {
        MyComponents.PhysicsModelsManager.UnregisterView(myNetworkView.ViewId);
    }
}