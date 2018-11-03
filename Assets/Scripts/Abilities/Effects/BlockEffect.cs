﻿using PlayerManagement;
using UnityEngine;

public class BlockEffect : AbilityEffect
{

    public override void ApplyOnTarget(params object[] parameters)
    {
        base.ApplyOnTarget(parameters);
        MyComponents.MyPlayer.controller.View.RPC("Block", RPCTargets.Server);
    }

}
