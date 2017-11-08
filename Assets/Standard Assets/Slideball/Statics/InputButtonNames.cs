﻿using System;
using UnityEngine;

public static class SlideBallInputs
{
    public static GUIPart currentPart;

    public enum GUIPart
    {
        ABILITY,
        CHAT,
        MENU,
    }

    public const string RETURN = "Return";

    public static bool AnyEnterDown()
    {
        return Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Return);
    }

    public static bool AnyShift()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    public static bool GetKey(string keyCode, GUIPart part)
    {
        return Input.GetKey(keyCode) && part == currentPart;
    }

    public static bool GetKeyDown(string keycode,GUIPart part)
    {
        return Input.GetKeyDown(keycode) && part == currentPart;
    }

    public static bool GetKeyUp(string keycode, GUIPart part)
    {
        return Input.GetKeyUp(keycode) && part == currentPart;
    }
}
