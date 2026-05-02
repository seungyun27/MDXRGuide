using Meta.XR.MRUtilityKit;
using UnityEngine;
using System.Collections.Generic;
using Meta.XR.Util;

public class CustomSpawner : MonoBehaviour
{

    [Header("프리팹 설정")]
    [SerializeField] private GameObject geobukseonPrefab;  // Geobukseon용
    [SerializeField] private GameObject bugeoPrefab;       // Bugeo용

    public float spawnHeight = 0.2f;
    public MRUKAnchor.SceneLabels spawnLabels;
    public float normalOffset;

    [Tooltip("When the scene data is loaded, this controls what room(s) the prefabs will spawn in.")]
    public MRUK.RoomFilter SpawnOnStart = MRUK.RoomFilter.CurrentRoomOnly;

    private ContentType _currentType = ContentType.None;
    private GameObject _spawnedInstance = null;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
#if UNITY_EDITOR
        if (MRUK.Instance && SpawnOnStart != MRUK.RoomFilter.None)
        {
            MRUK.Instance.RegisterSceneLoadedCallback(() =>
            {
                _currentType = ContentType.Geobukseon;
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
#endif
    }

    public void StartSpawn(ContentType type = ContentType.None)
    {
        if (type != ContentType.None)
            _currentType = type;

        if (_currentType == ContentType.None)
        {
            Debug.LogWarning("[CustomSpawner] ContentType이 지정되지 않아 스폰 생략");
            return;
        }

        // 기존 스폰 오브젝트 제거 후 재스폰
        ClearSpawned();

        foreach (var room in MRUK.Instance.Rooms)
        {
            SpawnObject(room);
        }
    }
    // Update is called once per frame


    public void SpawnObject(MRUKRoom room = null)
    {
        GameObject prefabToSpawn = _currentType switch
        {
            ContentType.Geobukseon => geobukseonPrefab,
            ContentType.Bugeo => bugeoPrefab,
            _ => geobukseonPrefab
        };

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[CustomSpawner] {_currentType}에 해당하는 프리팹이 없습니다.");
            return;
        }

        room ??= MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogWarning("[CustomSpawner] 현재 방을 찾을 수 없습니다.");
            return;
        }

        var labelFilter = new LabelFilter(spawnLabels);
        foreach (var anchor in room.Anchors)
        {
            if (!labelFilter.PassesFilter(anchor.Label)) continue;

            Vector3 spawnPos = anchor.transform.position + Vector3.up * normalOffset;
            _spawnedInstance = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            Debug.Log($"[CustomSpawner] 스폰 완료: {_currentType} @ {spawnPos}");
            break;
        }
    }

    public void ClearSpawned()
    {
        if (_spawnedInstance != null)
        {
            Destroy(_spawnedInstance);
            _spawnedInstance = null;
        }
    }
}
