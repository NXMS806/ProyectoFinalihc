using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Launcher : MonoBehaviourPunCallbacks
{
    [Header("Player Prefabs")]
    [Tooltip("Prefab del Jugador 1 - LIMPIADOR (3D con gestos)")]
    public string cleanerPrefabName = "Player1";
    
    [Tooltip("Prefab del Jugador 2 - CONTAMINADOR (2D estratégico)")]
    public string pollutorPrefabName = "Player2";
    
    [Header("Spawn Points")]
    public Transform cleanerSpawnPoint;  // Punto de spawn para Limpiador
    public Transform pollutorSpawnPoint; // Punto de spawn para Contaminador
    
    [Header("Game Settings")]
    [Tooltip("Máximo de jugadores permitidos en una sala")]
    public byte maxPlayersPerRoom = 2;

    void Start()
    {
        // Configurar límite de jugadores
        PhotonNetwork.ConnectUsingSettings();
        
        Debug.Log("Clean Ocean VR - Conectando a Photon...");
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Conectado al Master Server");
        
        PhotonNetwork.JoinRandomOrCreateRoom(
            roomOptions: new Photon.Realtime.RoomOptions { 
                MaxPlayers = maxPlayersPerRoom 
            }
        );
    }

    public override void OnJoinedRoom()
    {
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        
        Debug.Log($"Jugadores en sala: {playerCount}/{maxPlayersPerRoom}");
        
        // ASIGNACIÓN DE ROLES POR ORDEN DE LLEGADA
        if (playerCount == 1)
        {
            // PRIMER JUGADOR = LIMPIADOR (3D)
            SpawnCleanerPlayer();
        }
        else if (playerCount == 2)
        {
            // SEGUNDO JUGADOR = CONTAMINADOR (2D)
            SpawnPollutorPlayer();
        }
        else
        {
            Debug.LogError("Sala llena. Saliendo...");
            PhotonNetwork.LeaveRoom();
        }
    }
    
    void SpawnCleanerPlayer()
    {
        Vector3 spawnPos = cleanerSpawnPoint != null ? 
            cleanerSpawnPoint.position : Vector3.zero;
        Quaternion spawnRot = cleanerSpawnPoint != null ? 
            cleanerSpawnPoint.rotation : Quaternion.identity;
        
        PhotonNetwork.Instantiate(cleanerPrefabName, spawnPos, spawnRot);
        
        Debug.Log("Rol asignado: LIMPIADOR (Jugador 1 - 3D)");
        Debug.Log("   Controls: Gestos de manos o celular");
        Debug.Log("   Objetivo: Limpiar el oceano");
    }
    
    void SpawnPollutorPlayer()
    {
        Vector3 spawnPos = pollutorSpawnPoint != null ? 
            pollutorSpawnPoint.position : new Vector3(0, 50, 0);
        Quaternion spawnRot = pollutorSpawnPoint != null ? 
            pollutorSpawnPoint.rotation : Quaternion.Euler(90, 0, 0);
        
        PhotonNetwork.Instantiate(pollutorPrefabName, spawnPos, spawnRot);
        
        Debug.Log("Rol asignado: CONTAMINADOR (Jugador 2 - 2D)");
        Debug.Log("   Controls: Mouse/Teclado");
        Debug.Log("   Objetivo: Ensuciar el oceano");
    }
    
    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        Debug.Log($"Jugador conectado: {newPlayer.NickName}");
        Debug.Log($"Total jugadores: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }
    
    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        Debug.LogWarning($"Jugador desconectado: {otherPlayer.NickName}");
        Debug.LogWarning($"Jugadores restantes: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }
}
