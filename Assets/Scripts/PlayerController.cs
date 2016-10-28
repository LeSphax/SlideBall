﻿using Byn.Net;
using UnityEngine;
using UnityEngine.UI;
using PlayerManagement;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMovementView))]
public class PlayerController : PlayerView
{
    public GameObject targetPrefab;

    private GameObject Mesh;

    public Text playerName;

    private GameObject _target;
    private GameObject target
    {
        get
        {
            return _target;
        }
        set
        {
            if (value == null)
            {
                movementManager.targetPosition = null;
            }
            else
            {
                movementManager.targetPosition = value.transform.position;
            }
            _target = value;
        }
    }
    PlayerMovementView movementManager;

    private MoveInput moveInput;

    void Awake()
    {
        movementManager = GetComponent<PlayerMovementView>();
        moveInput = gameObject.AddComponent<MoveInput>();
        target = null;
    }

    void Update()
    {
        if (View.isMine)
        {
            if (moveInput.Activate())
            {
                CreateTarget();
            }
        }
    }

    internal void DestroyTarget()
    {
        Destroy(target);
        target = null;
    }

    public void CreateTarget()
    {
        Vector3 position = Functions.GetMouseWorldPosition();
        CreateTarget(position);
    }

    public void CreateTarget(Vector3 position)
    {
        DestroyTarget();
        target = (GameObject)Instantiate(targetPrefab, position, Quaternion.identity);
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.tag == Tags.Target)
        {
            DestroyTarget();
        }
    }

    public void Init(ConnectionId id, int teamNumber, string name)
    {
        View.RPC("InitPlayer", RPCTargets.AllBuffered, id);
    }

    [MyRPC]
    private void InitPlayer(ConnectionId id)
    {
        playerConnectionId = id;

        ResetPlayer();
    }

    [MyRPC]
    public void ResetPlayer()
    {
        if (Mesh != null)
        {
            Destroy(Mesh);
        }
        if (View.isMine)
        {
            tag = Tags.MyPlayer;
            PutAtStartPosition();
        }

        gameObject.name = Player.Nickname;
        playerName.text = Player.Nickname;

        CreateMesh();
        ConfigureColliders();

        MyGameObjects.AbilitiesFactory.RecreateAbilities();
    }

    private void ConfigureColliders()
    {
        GetComponent<CapsuleCollider>().radius = Player.MyAvatarSettings.catchColliderRadius;
        GetComponent<CapsuleCollider>().center = Vector3.forward * Player.MyAvatarSettings.catchColliderZPos;
        int layer = -1;
        if (Player.AvatarSettingsType == AvatarSettings.AvatarSettingsTypes.GOALIE)
            layer = LayersGetter.players[(int)Player.Team];
        else
            layer = LayersGetter.players[2];
        gameObject.layer = layer;
        Functions.SetLayer(Mesh.transform, layer);
    }

    private void CreateMesh()
    {
        GameObject meshPrefab = Resources.Load<GameObject>(Player.MyAvatarSettings.MESH_NAME);
        Mesh = Instantiate(meshPrefab);
        Mesh.transform.SetParent(transform, false);
        SetMaterials(Mesh);
    }

    private void SetMaterials(GameObject mesh)
    {
        if (View.isMine)
        {
            mesh.transform.GetChild(0).GetComponent<Renderer>().material = ResourcesGetter.OutLineMaterial();
        }
        Color teamColor = Colors.Teams[(int)Player.Team];
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) { if (renderer.tag == Tags.TeamColored) renderer.material.color = teamColor; }
        playerName.color = teamColor;
    }

    public void PutAtStartPosition()
    {
        transform.position = MyGameObjects.Spawns.GetSpawn(Player.Team, Player.SpawnNumber);
        transform.LookAt(Vector3.zero);
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        DestroyTarget();
    }
}
