﻿using UnityEngine;
using UnityEngine.UI;
public class Scoreboard : SlideBall.MonoBehaviour
{

    private static int[] scores = new int[2] { 0, 0 };

    private static Text textScoreLeft;
    private static Text textScoreRight;

    private static GameObject scoreboard;

    private static float timeLastGoal = 0;

    private AudioSource myAudio;
    private AudioSource Audio
    {
        get
        {
            if (myAudio == null)
            {
                myAudio = GetComponent<AudioSource>();
                AudioClip but = Resources.Load<AudioClip>("Audio/But");
                myAudio.clip = but;
            }
            return myAudio;
        }
    }

    void Awake()
    {
        scoreboard = gameObject;
        textScoreLeft = transform.GetChild(0).GetComponent<Text>();
        textScoreRight = transform.GetChild(1).gameObject.GetComponent<Text>();
    }

    [MyRPC]
    public void UpdateScoreBoard(int[] scores, bool playAudio)
    {
        Scoreboard.scores = scores;
        textScoreLeft.text = "" + scores[0];
        textScoreRight.text = "" + scores[1];
        if (playAudio)
            Audio.Play();
    }

    public static void IncrementTeamScore(int teamNumber)
    {
        if (MyComponents.NetworkManagement.isServer)
        {
            if (Time.realtimeSinceStartup - timeLastGoal > 1)
            {
                timeLastGoal = Time.realtimeSinceStartup;
                scores[teamNumber] += 1;
                scoreboard.GetNetworkView().RPC("UpdateScoreBoard", RPCTargets.All, scores, true);
            }
        }

    }

    public static void ResetScore()
    {
        scoreboard.GetNetworkView().RPC("UpdateScoreBoard", RPCTargets.All, scores, false);
    }

}
