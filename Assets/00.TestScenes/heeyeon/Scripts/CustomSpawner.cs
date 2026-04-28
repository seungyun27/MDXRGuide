using Meta.XR.MRUtilityKit;
using UnityEngine;
using System.Collections.Generic;
using Meta.XR.Util;

public class CustomSpawner : MonoBehaviour
{

    public GameObject prefabToSpawn;

    public float spawnHeight = 0.2f;
    public MRUKAnchor.SceneLabels spawnLabels;
    public float normalOffset;

    [Tooltip("When the scene data is loaded, this controls what room(s) the prefabs will spawn in.")]
    public MRUK.RoomFilter SpawnOnStart = MRUK.RoomFilter.CurrentRoomOnly;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (MRUK.Instance && SpawnOnStart != MRUK.RoomFilter.None)
        {
            MRUK.Instance.RegisterSceneLoadedCallback(() =>
            {
                switch (SpawnOnStart)
                {
                    case MRUK.RoomFilter.AllRooms:
                        StartSpawn();
                        break;
                    case MRUK.RoomFilter.CurrentRoomOnly:
                        SpawnObject();
                        break;
                }
            });
        }
    }

    public void StartSpawn()
    {
        foreach (var room in MRUK.Instance.Rooms)
        {
            SpawnObject();
        }
    }


    // Update is called once per frame
    void Update()
    {
        if(!MRUK.Instance && !MRUK.Instance.IsInitialized)
        {
            return;
        }

    }

    public void SpawnObject()
    {
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        var labelFilter = new LabelFilter(spawnLabels);
        foreach (var anchor in room.Anchors)
        {
            if (!labelFilter.PassesFilter(anchor.Label)) continue;

            // 앵커의 월드 중심 위치
            Vector3 anchorCenter = anchor.transform.position;
            Vector3 spawnPos = anchorCenter + Vector3.up * normalOffset;

            Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            break;
        }

    }
}
