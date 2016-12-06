﻿using UnityEngine;
using System.Collections;
using PlayerManagement;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class VictoryPose : SlideBall.MonoBehaviour
{
    public GameObject victoryCamera;
    public Transform goaliePosition;
    public Transform frontPosition;
    public Transform leftPosition;
    public Transform rightPosition;

    public Text victoryText;

    private void Start()
    {
        victoryCamera.SetActive(false);
    }

    [MyRPC]
    public void SetVictoryPose(Team team)
    {
        Debug.LogError("SetVictoryPose");
        switch (team)
        {
            case Team.BLUE:
                victoryCamera.transform.rotation *= Quaternion.Euler(victoryCamera.transform.rotation.eulerAngles.x, 180, victoryCamera.transform.rotation.eulerAngles.z);
                victoryCamera.transform.position = victoryCamera.transform.position - victoryCamera.transform.position.z * Vector3.forward + Vector3.forward * -50;
                break;
            case Team.RED:
                victoryCamera.transform.rotation *= Quaternion.Euler(victoryCamera.transform.rotation.eulerAngles.x, 0, victoryCamera.transform.rotation.eulerAngles.z);
                victoryCamera.transform.position = victoryCamera.transform.position - victoryCamera.transform.position.z * Vector3.forward + Vector3.forward * 50;
                break;
            default:
                Debug.LogError("This isn't a valid team " + team);
                break;
        }
        MyComponents.AbilitiesFactory.gameObject.SetActive(false);
        Camera.main.gameObject.SetActive(false);
        victoryCamera.gameObject.SetActive(true);
        //
        MyComponents.VictoryUI.SetVictoryText(team);


        if (MyComponents.NetworkManagement.isServer)
        {
            List<Player> players = Players.GetPlayersInTeam(team);
            int attackersPutInPlace = 0;
            foreach (Player player in players)
            {
                player.CurrentState = Player.State.FROZEN;
                if (player.AvatarSettingsType == AvatarSettings.AvatarSettingsTypes.GOALIE)
                {
                    player.controller.transform.position = goaliePosition.position;
                }
                else if (players.Count <= 2 || (players.Count == 4 && attackersPutInPlace == 0))
                {
                    player.controller.transform.position = frontPosition.position;
                    attackersPutInPlace++;
                }
                else if ((players.Count == 3 && attackersPutInPlace == 0) || (players.Count == 4 && attackersPutInPlace == 1))
                {
                    player.controller.transform.position = leftPosition.position;
                    attackersPutInPlace++;
                }
                else if ((players.Count == 3 && attackersPutInPlace == 1) || (players.Count == 4 && attackersPutInPlace == 2))
                {
                    player.controller.transform.position = leftPosition.position;
                    attackersPutInPlace++;
                }
            }
            Assert.IsTrue(attackersPutInPlace == players.Count - 1 || (attackersPutInPlace == players.Count && players.Count == 1));
        }
    }

}
