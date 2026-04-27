using System.Collections.Generic;
using UnityEngine;
using Meta.XR.BuildingBlocks.AIBlocks;

/// <summary>
/// Object Detection → ContentSpawner 연결 브릿지.
/// - 최초 stableFramesRequired 프레임 연속 탐지 시 콘텐츠 스폰
/// - 이후 followMode ON이면 SmoothUpdateTarget() + UpdateOccluderScale() 호출
/// - 탐지 노이즈로 인한 떨림은 ContentSpawner 내 2단계 EMA 필터로 처리
/// </summary>
public class ObjectDetectionBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ContentSpawner contentSpawner;
    [SerializeField] private ObjectDetectionAgent detectionAgent;
    [SerializeField] private ObjectDetectionVisualizer visualizer;

    [Header("Detection Labels")]
    [SerializeField] private List<string> geobukseonLabels = new List<string> { "keyring", "turtle_ship", "geobukseon" };
    [SerializeField] private List<string> bugeoLabels      = new List<string> { "bugeo", "dried_fish", "pollock" };

    [Header("Settings")]
    [Tooltip("최초 스폰에 필요한 연속 탐지 프레임 수")]
    [SerializeField] private int stableFramesRequired = 5;
    [Tooltip("탐지가 끊긴 후 이 프레임 수 이상 지나면 위치 추적 정지")]
    [SerializeField] private int lostFramesUntilFreeze = 10;

    [Header("Follow Mode")]
    [Tooltip("ON: 실물 객체를 따라 가상 콘텐츠 이동\nOFF: 최초 탐지 위치에 고정 (ReDetectButton으로 수동 재정렬)")]
    [SerializeField] private bool followMode = false;

    private int         _stableCount   = 0;
    private int         _lostCount     = 0;
    private ContentType _candidate     = ContentType.None;
    private bool        _contentActive = false;

    // 마지막으로 탐지된 위치 (followMode=false일 때도 항상 갱신)
    private Vector3    _lastDetectedPos = Vector3.zero;
    private Quaternion _lastDetectedRot = Quaternion.identity;
    private bool       _hasDetection    = false;

    private void Awake()
    {
        if (contentSpawner == null) contentSpawner = FindObjectOfType<ContentSpawner>();
        if (detectionAgent  == null) detectionAgent  = GetComponent<ObjectDetectionAgent>();
        if (visualizer      == null) visualizer      = GetComponent<ObjectDetectionVisualizer>();
    }

    private void OnEnable()
    {
        if (detectionAgent != null) detectionAgent.OnBoxesUpdated += HandleBoxesUpdated;
    }

    private void OnDisable()
    {
        if (detectionAgent != null) detectionAgent.OnBoxesUpdated -= HandleBoxesUpdated;
    }

    private void HandleBoxesUpdated(List<BoxData> batch)
    {
        if (batch == null || batch.Count == 0)
        {
            OnDetectionLost();
            return;
        }

        ContentType detected = ContentType.None;
        BoxData targetBox = default;

        foreach (var box in batch)
        {
            var label = ExtractLabel(box.label);
            var type  = MatchLabel(label);
            if (type != ContentType.None)
            {
                detected  = type;
                targetBox = box;
                break;
            }
        }

        if (detected == ContentType.None)
        {
            OnDetectionLost();
            return;
        }

        _lostCount = 0;

        Vector3    worldPos;
        Quaternion worldRot;
        Vector3    worldScale;
        if (!TryGetWorldPos(targetBox, out worldPos, out worldRot, out worldScale)) return;

        // followMode 여부와 관계없이 항상 최신 탐지 위치를 저장
        _lastDetectedPos = worldPos;
        _lastDetectedRot = worldRot;
        _hasDetection    = true;

        if (!_contentActive)
        {
            if (detected == _candidate) _stableCount++;
            else { _candidate = detected; _stableCount = 1; }

            Debug.Log($"[ObjectDetectionBridge] 안정화: {_stableCount}/{stableFramesRequired} ({detected})");

            if (_stableCount < stableFramesRequired) return;

            // 탐지 스케일 먼저 전달 → SpawnOccluder에서 사용
            contentSpawner?.UpdateOccluderScale(worldScale);

            if (contentSpawner != null && contentSpawner.HasSceneAnchor)
            {
                contentSpawner.Activate(detected);
                Debug.Log($"[ObjectDetectionBridge] 씬 앵커 기준 스폰: type={detected}");
            }
            else
            {
                contentSpawner?.PlaceAndActivate(worldPos, worldRot, detected);
                Debug.Log($"[ObjectDetectionBridge] TryProject 기준 스폰: {worldPos}, type={detected}");
            }
            _contentActive = true;
        }
        else
        {
            if (followMode && contentSpawner != null && !contentSpawner.HasSceneAnchor)
            {
                contentSpawner.SmoothUpdateTarget(worldPos, worldRot);
                // 오클루더도 탐지 크기에 맞게 계속 업데이트
                contentSpawner.UpdateOccluderScale(worldScale);
            }
        }
    }

    private void OnDetectionLost()
    {
        if (!_contentActive) { ResetStable(); return; }

        _lostCount++;
        if (_lostCount >= lostFramesUntilFreeze)
            Debug.Log($"[ObjectDetectionBridge] 탐지 끊김 {_lostCount}프레임 — 위치 고정");
    }

    /// <summary>
    /// 콘텐츠 재생성 없이 마지막 탐지 위치로 앵커를 즉시 재정렬합니다.
    /// ReDetectButton 또는 UI 버튼에서 호출합니다.
    /// </summary>
    public void ReSnapToLatestDetection()
    {
        if (!_hasDetection || contentSpawner == null)
        {
            Debug.LogWarning("[ObjectDetectionBridge] 아직 탐지 위치가 없습니다.");
            return;
        }
        contentSpawner.SnapAnchor(_lastDetectedPos, _lastDetectedRot);
        Debug.Log($"[ObjectDetectionBridge] 재스냅 → {_lastDetectedPos}");
    }

    /// <summary>콘텐츠를 지우고 처음부터 재탐지합니다.</summary>
    public void ResetAnchor()
    {
        _contentActive = false;
        _stableCount   = 0;
        _lostCount     = 0;
        _candidate     = ContentType.None;

        if (contentSpawner != null && contentSpawner.HasSceneAnchor)
            contentSpawner.ClearContent();
        else
            contentSpawner?.ClearAll();

        Debug.Log("[ObjectDetectionBridge] 리셋 — 재탐지 대기");
    }

    private bool TryGetWorldPos(BoxData box, out Vector3 worldPos, out Quaternion worldRot,
                                 out Vector3 worldScale)
    {
        worldPos  = Vector3.zero;
        worldRot  = Quaternion.identity;
        worldScale = Vector3.zero;

        if (visualizer != null &&
            visualizer.TryProject(box.position.x, box.position.y,
                                   box.scale.x,    box.scale.y,
                                   out worldPos, out worldRot, out worldScale))
        {
            return true;
        }

        // TryProject 실패 → 카메라 정면 폴백
        var cam = Camera.main;
        if (cam != null)
        {
            var fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
            worldPos  = cam.transform.position + fwd * 0.8f;
            worldRot  = Quaternion.LookRotation(fwd);
            worldScale = Vector3.zero; // 폴백 시 스케일 미제공
            return true;
        }
        return false;
    }

    private void ResetStable()
    {
        _stableCount = 0;
        _candidate   = ContentType.None;
    }

    private ContentType MatchLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return ContentType.None;
        var lower = label.ToLowerInvariant();
        foreach (var g in geobukseonLabels)
            if (lower.Contains(g.ToLowerInvariant())) return ContentType.Geobukseon;
        foreach (var b in bugeoLabels)
            if (lower.Contains(b.ToLowerInvariant())) return ContentType.Bugeo;
        return ContentType.None;
    }

    private static string ExtractLabel(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var parts = raw.Split(' ');
        return parts.Length > 0 ? parts[0].Trim() : raw.Trim();
    }

    [ContextMenu("Debug: Reset Anchor")]
    private void DebugReset() => ResetAnchor();
}
