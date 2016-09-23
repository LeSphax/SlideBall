﻿using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
class PlayerMovementPhotonView : Photon.MonoBehaviour
{

    Rigidbody myRigidbody;

    private float speed = 70;

    private float MAX_VELOCITY = 45;
    private float ANGULAR_SPEED = 400;
    public Vector3? targetPosition;
    public float timeLastUpdate;
    private int currentId = 0;

    private const float FRAME_DURATION = 0.02f;
    private double CLIENT_DELAY
    {
        get
        {
            return 3* 1f/PhotonNetwork.sendRate + PhotonNetwork.GetPing()/1000f;
        }
    }
    private double simulationTime
    {
        get
        {
            if (photonView.isMine)
            {
                return PhotonNetwork.time;
            }
            else
            {
                return PhotonNetwork.time - CLIENT_DELAY;
            }
        }
    }

    private Queue<PlayerPacket> StateBuffer = new Queue<PlayerPacket>();
    private PlayerPacket? currentPacket = null;

    private PlayerPacket? nextPacket
    {
        get
        {
            if (StateBuffer.Count > 0)
            {
                return StateBuffer.Peek();
            }
            else
            {
                return currentPacket;
            }
        }
    }

    void Awake()
    {
        myRigidbody = GetComponent<Rigidbody>();
    }

    void Start()
    {
        float delay = 0f;
        if (!photonView.isMine)
            delay = (float)CLIENT_DELAY;
        Invoke("StartUpdating", delay);
    }

    private bool startUpdating = false;

    private void StartUpdating() { startUpdating = true; }

    void FixedUpdate()
    {
        if (startUpdating)
        {
            if (photonView.isMine)
            {
                OwnerUpdate();
            }
            else
            {
                Debug.Log(CLIENT_DELAY);
                SimulationUpdate();
            }
        }
    }

    private void OwnerUpdate()
    {
        if (targetPosition != null)
        {
            var lookPos = targetPosition.Value - transform.position;
            lookPos.y = 0;
            var targetRotation = Quaternion.LookRotation(lookPos);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, FRAME_DURATION * ANGULAR_SPEED);
            myRigidbody.AddForce(transform.forward * speed* FRAME_DURATION, ForceMode.VelocityChange);
        }
        myRigidbody.velocity *= Mathf.Min(1.0f, MAX_VELOCITY / myRigidbody.velocity.magnitude);
        //myRigidbody.CustomUpdate();
    }

    private void SimulationUpdate()
    {
        if (StateBuffer.Count > 0)
        {
            if (simulationTime >= StateBuffer.Peek().timeSent)
            {
                currentPacket = StateBuffer.Dequeue();
                //Debug.Log("Packet Consumed " + currentPacket.Value.id);
            }
        }
        else
        {
            Debug.LogError("No Packet in buffer !!! " + currentPacket.Value.id + "   " + gameObject.name);
        }

        if (currentPacket != null)
        {
            double deltaTime = nextPacket.Value.timeSent - currentPacket.Value.timeSent;
            float completion = 0;
            if (deltaTime != 0)
                completion = (float)((simulationTime - currentPacket.Value.timeSent) / deltaTime);
            transform.position = Vector3.Lerp(currentPacket.Value.position, nextPacket.Value.position, completion);
            transform.rotation = Quaternion.Lerp(currentPacket.Value.rotation, nextPacket.Value.rotation, completion);
        }
        else
        {
            Debug.LogWarning("No Packets for currentTime " + simulationTime);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.isWriting)
        {
            stream.SendNext(myRigidbody.velocity);
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(targetPosition);
            stream.SendNext(currentId);
            currentId++;
        }

        else if (stream.isReading)
        {
            PlayerPacket newPacket = new PlayerPacket();
            newPacket.velocity = (Vector3)stream.ReceiveNext();
            newPacket.position = (Vector3)stream.ReceiveNext();
            newPacket.rotation = (Quaternion)stream.ReceiveNext();
            newPacket.target = (Vector3?)stream.ReceiveNext();
            newPacket.id = (int)stream.ReceiveNext();
            while (newPacket.id != currentId)
            {
                Debug.LogWarning("Lost Packet " + currentId);
                currentId++;
            }
           // Debug.Log("Received Packet " + currentId);
            currentId++;
            newPacket.timeSent = info.timestamp;
            StateBuffer.Enqueue(newPacket);
            //RefreshSimulation();
        }
    }

    //private void RefreshSimulation()
    //{
    //    transform.position = myNetworkPosition;
    //    myRigidbody.velocity = myNetworkVelocity;
    //    targetPosition = myNetworkTarget;
    //    float timePassed = PhotonNetwork.GetPing() / 1000f;
    //    Simulate(timePassed);
    //}


    ///*
    // * Simulate the passaing of time seconds
    // */
    //public void Simulate(float time)
    //{
    //    int numberFrames = (int)(time / FRAME_DURATION);
    //    float residualTime = time % FRAME_DURATION;
    //    for (int i = 0; i < numberFrames; i++)
    //    {
    //        CustomUpdate();
    //    }
    //    CancelInvoke("CustomUpdate");
    //    InvokeRepeating("CustomUpdate", residualTime, FRAME_DURATION);
    //}
}

public struct PlayerPacket
{
    public Vector3 velocity;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3? target;
    public double timeSent;
    public int id;
}

