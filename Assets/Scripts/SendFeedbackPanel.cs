﻿using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.UI;

public class SendFeedbackPanel : MonoBehaviour
{

    public InputField headerInput;
    public InputField messageInput;

    [DllImport("__Internal")]
    private static extern void SendFeedback(string header, string message);

    public void Send()
    {
        SendFeedback(headerInput.text, messageInput.text);
    }
}