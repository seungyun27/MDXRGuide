using UnityEngine;

/// <summary>
/// 앵커 재정렬 트리거 — 합장 제스처 또는 컨트롤러 버튼.
///
/// 합장 감지 원리:
///   양 손목(OVRInput.Controller.LHand / RHand) 간 거리가
///   prayerDistanceThreshold 이하인 상태를 prayerHoldDuration 초간 유지하면 트리거.
///   핀치/그랩 제스처와 겹치지 않으며, 자연스러운 '합장' 동작으로 구별됩니다.
///
/// 컨트롤러 사용 시: A(오른손) / X(왼손) 버튼
/// 에디터: R 키
/// </summary>
public class ReDetectButton : MonoBehaviour
{
    [SerializeField] private ObjectDetectionBridge detectionBridge;

    [Header("합장 제스처 설정")]
    [Tooltip("양 손목 간 거리 임계값 (m). 이 값 이하이면 합장으로 인식.\n" +
             "너무 크면 오인식, 너무 작으면 인식 어려움. 권장: 0.10~0.15")]
    [SerializeField] private float prayerDistanceThreshold = 0.12f;

    [Tooltip("합장을 유지해야 하는 시간 (초). 짧으면 오인식 위험.")]
    [SerializeField] private float prayerHoldDuration = 0.8f;

    [Tooltip("트리거 직후 재인식 방지 쿨다운 (초).")]
    [SerializeField] private float cooldownSeconds = 2.5f;

    private float _prayerTimer   = 0f;
    private float _cooldownTimer = 0f;

    // 합장 진행률을 디버그 로그로 확인하고 싶을 때 true로
    [SerializeField] private bool debugLogProgress = false;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (detectionBridge == null)
            detectionBridge = FindObjectOfType<ObjectDetectionBridge>();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R)) TriggerReSnap();
        return;
#endif

        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
            return;
        }

        // 컨트롤러 버튼 (즉시)
        if (OVRInput.GetDown(OVRInput.Button.One,   OVRInput.Controller.RTouch) ||
            OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.LTouch))
        {
            TriggerReSnap();
            return;
        }

        // 합장 제스처 (핸드트래킹)
        CheckPrayerGesture();
    }

    private void CheckPrayerGesture()
    {
        // 양손 핸드트래킹이 모두 활성화된 경우에만 체크
        var active = OVRInput.GetActiveController();
        bool leftTracked  = (active & OVRInput.Controller.LHand) != 0;
        bool rightTracked = (active & OVRInput.Controller.RHand) != 0;

        if (!leftTracked || !rightTracked)
        {
            if (_prayerTimer > 0f) _prayerTimer = 0f;
            return;
        }

        // 손목 위치 (트래킹 공간 기준, 같은 좌표계이므로 거리 비교 유효)
        Vector3 leftPos  = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LHand);
        Vector3 rightPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RHand);
        float dist = Vector3.Distance(leftPos, rightPos);

        if (dist <= prayerDistanceThreshold)
        {
            _prayerTimer += Time.deltaTime;

            if (debugLogProgress)
                Debug.Log($"[ReDetectButton] 합장 유지 {_prayerTimer:F2}/{prayerHoldDuration:F2}s  (거리: {dist:F3}m)");

            if (_prayerTimer >= prayerHoldDuration)
            {
                _prayerTimer   = 0f;
                _cooldownTimer = cooldownSeconds;
                TriggerReSnap();
            }
        }
        else
        {
            if (_prayerTimer > 0f)
            {
                if (debugLogProgress)
                    Debug.Log($"[ReDetectButton] 합장 해제 (거리: {dist:F3}m, 진행: {_prayerTimer:F2}s 리셋)");
                _prayerTimer = 0f;
            }
        }
    }

    /// <summary>외부 UI 버튼(OnClick 이벤트)에서도 호출 가능합니다.</summary>
    public void TriggerReSnap()
    {
        detectionBridge?.ResetAnchor();
        Debug.Log("[ReDetectButton] 디텍션 초기화 실행");
    }
}
