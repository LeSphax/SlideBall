﻿using Navigation;
using UnityEngine;

public class MenuInGame : MonoBehaviour
{


    public GameObject background;
    public MatchPanel matchPanel;
    SlideBallInputs.GUIPart previousPart;

    private void Start()
    {
        OpenMatch();
    }

    public void OpenSettings()
    {
        UserSettingsPanel.InstantiateSettingsPanel().transform.SetParent(transform.parent, false);
    }

    public void OpenMatch()
    {
        CloseMenu();
        matchPanel.Open(!matchPanel.gameObject.activeSelf);
    }

    public void OpenMenu()
    {
        background.SetActive(true);
        previousPart = SlideBallInputs.currentPart;
        SlideBallInputs.currentPart = SlideBallInputs.GUIPart.MENU;
    }

    public void CloseMenu()
    {
        background.SetActive(false);
        SlideBallInputs.currentPart = previousPart;
    }

    public void LeaveRoom()
    {
        MyComponents.ResetNetworkComponents();
        NavigationManager.LoadScene(Scenes.Lobby, true);
    }

    public void ReturnToRoom()
    {
        MyComponents.ResetGameComponents();
        NavigationManager.LoadScene(Scenes.Room, true);
    }

    private void OnEnable()
    {
        MyComponents.NetworkManagement.RoomClosed += LeaveRoom;
    }

    private void OnDisable()
    {
        MyComponents.NetworkManagement.RoomClosed -= LeaveRoom;

    }
}
