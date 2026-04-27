using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scene Understanding으로 TABLE 시맨틱 레이블을 탐지하고
/// 앵커 Transform을 ContentSpawner에 직접 전달합니다.
/// TABLE이 없으면 카메라 정면에 Fallback 앵커를 배치해 에디터/빌드 테스트 가능합니다.
/// </summary>
public class SceneAnchorManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OVRSceneManager sceneManager;
    [SerializeField] private ContentSpawner contentSpawner;

    [Header("Settings")]
    [Tooltip("테이블 상면 기준 콘텐츠 배치 높이 오프셋 (m)")]
    [SerializeField] private float heightOffset = 0.05f;

    [Header("Events (선택)")]
    public UnityEvent OnTableFound;
    public UnityEvent OnNoTableFound;

    private Transform _tableAnchorPoint;
    public Transform TableAnchorPoint => _tableAnchorPoint;
    public bool IsTableFound => _tableAnchorPoint != null;

    private void Awake()
    {
        if (sceneManager == null)
            sceneManager = FindObjectOfType<OVRSceneManager>();
        if (contentSpawner == null)
            contentSpawner = FindObjectOfType<ContentSpawner>();
    }

    private void OnEnable()
    {
        if (sceneManager == null) return;
        sceneManager.SceneModelLoadedSuccessfully += HandleSceneLoaded;
        sceneManager.NoSceneModelToLoad += HandleNoSceneModel;
    }

    private void OnDisable()
    {
        if (sceneManager == null) return;
        sceneManager.SceneModelLoadedSuccessfully -= HandleSceneLoaded;
        sceneManager.NoSceneModelToLoad -= HandleNoSceneModel;
    }

    private void HandleSceneLoaded()
    {
        Debug.Log("[SceneAnchorManager] Scene model loaded. TABLE 탐색 시작...");
        StartCoroutine(SearchTableDelayed());
    }

    private void HandleNoSceneModel()
    {
        Debug.LogWarning("[SceneAnchorManager] No scene model. 헤드셋에서 Space Setup을 먼저 실행하세요.");
        OnNoTableFound?.Invoke();
    }

    private IEnumerator SearchTableDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        LocateTableAnchor();
    }

    /// <summary>씬에서 TABLE 시맨틱 앵커를 탐색하고 앵커 포인트를 생성합니다.</summary>
    public void LocateTableAnchor()
    {
        foreach (var anchor in FindObjectsOfType<OVRSceneAnchor>())
        {
            var classification = anchor.GetComponent<OVRSemanticClassification>();
            if (classification != null && classification.Contains("TABLE"))
            {
                var anchorPoint = new GameObject("TableAnchorPoint");
                anchorPoint.transform.SetParent(anchor.transform);
                anchorPoint.transform.localPosition = new Vector3(0f, heightOffset, 0f);
                anchorPoint.transform.localRotation = Quaternion.identity;

                SetAnchorPoint(anchorPoint.transform);
                Debug.Log($"[SceneAnchorManager] TABLE 발견: {anchor.transform.position}");
                return;
            }
        }

        Debug.LogWarning("[SceneAnchorManager] TABLE 미발견. Space Setup에서 책상이 등록됐는지 확인하세요.");
        OnNoTableFound?.Invoke();
    }

    [ContextMenu("Test: Place Fallback Anchor")]
    public void PlaceFallbackAnchor()
    {
        if (_tableAnchorPoint != null) return;

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[SceneAnchorManager] Main Camera를 찾을 수 없습니다.");
            return;
        }

        var fallback = new GameObject("FallbackAnchorPoint");
        var fwd = cam.transform.forward;
        fwd.y = 0f;
        fwd.Normalize();
        fallback.transform.position = cam.transform.position + fwd * 0.7f + Vector3.down * 0.3f;

        SetAnchorPoint(fallback.transform);
        Debug.Log($"[SceneAnchorManager] Fallback 앵커 배치: {fallback.transform.position}");
    }

    private void SetAnchorPoint(Transform anchor)
    {
        _tableAnchorPoint = anchor;
        contentSpawner?.SetAnchor(anchor);
        OnTableFound?.Invoke();
    }
}
