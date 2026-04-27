using System.Collections;
using UnityEngine;
using Oculus.Interaction;

/// <summary>
/// 판옥선 개별 동작: 편대 목표 추적 + 파도 흔들림 + 그랩 인터랙션.
///
/// 상태 흐름:
///   Free         : 편대 목표로 followSpeed로 이동
///   Grabbed      : 이동 중단 (손이 제어), 편대에서 제외
///   WaitingReturn: 손에서 놓인 직후 returnDelay 동안 frozen (이동 안함)
///                  편대 카운트에는 즉시 포함 → 나머지 배가 미리 자리를 비켜줌
///   Returning    : frozen 해제 후 returnFollowSpeed로 부드럽게 귀환
/// </summary>
public class PanokseonFloater : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────
    [Header("Follow")]
    [SerializeField] private float followSpeed       = 7f;
    [SerializeField] private float returnFollowSpeed = 3.5f;  // 귀환 시 느리게 → 나머지가 먼저 자리 잡음
    [SerializeField] private float rotationSpeed     = 5f;

    [Header("Wave Rocking")]
    [SerializeField] private float rollAmplitude  = 4.5f;
    [SerializeField] private float pitchAmplitude = 2.5f;
    [SerializeField] private float rockSpeed      = 0.85f;

    [Header("Return")]
    [Tooltip("손에서 놓은 뒤 frozen 유지 시간(초). 이 동안 나머지 배들이 자리를 비껴줌.")]
    [SerializeField] private float returnDelay = 2f;

    // ─── 상태 읽기 ────────────────────────────────────────────────────
    public bool IsGrabbed         { get; private set; }
    public bool IsWaitingToReturn { get; private set; }  // frozen 중: 편대 카운트는 포함, 이동은 안 함
    public bool IsReturning       { get; private set; }  // frozen 해제 후 귀환 이동 중

    public PanokseonFormation Formation { get; set; }

    // ─── 내부 ─────────────────────────────────────────────────────────
    private Vector3    _targetPos;
    private Quaternion _targetRot = Quaternion.identity;
    private float      _phaseOffset;
    private Coroutine  _returnCoroutine;
    private Grabbable  _grabbable;

    // ─── Unity ────────────────────────────────────────────────────────

    private void Awake()
    {
        _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        _targetPos   = transform.position;
    }

    private void Start()
    {
        _grabbable = GetComponent<Grabbable>()
                  ?? GetComponentInChildren<Grabbable>(true);

        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy()
    {
        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void Update()
    {
        // 그랩 중 or 대기 frozen → 이동 없음
        if (IsGrabbed || IsWaitingToReturn) return;

        float speed = IsReturning ? returnFollowSpeed : followSpeed;

        // ── 위치 이동 ─────────────────────────────────────────────────
        transform.position = Vector3.Lerp(
            transform.position, _targetPos, Time.deltaTime * speed);

        // 귀환 완료 판정
        if (IsReturning && Vector3.Distance(transform.position, _targetPos) < 0.006f)
            IsReturning = false;

        // ── 파도 흔들림 ───────────────────────────────────────────────
        float t     = Time.time * rockSpeed + _phaseOffset;
        float roll  =  Mathf.Sin(t)        * rollAmplitude;
        float pitch =  Mathf.Cos(t * 1.3f) * pitchAmplitude;

        Quaternion baseRot = IsReturning ? Quaternion.identity : _targetRot;
        Quaternion rockRot = baseRot * Quaternion.Euler(pitch, 0f, roll);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, rockRot, Time.deltaTime * rotationSpeed);
    }

    // ─── 편대 인터페이스 ──────────────────────────────────────────────

    public void SetFormationTarget(Vector3 worldPos, Quaternion baseRot)
    {
        _targetPos = worldPos;
        _targetRot = baseRot;
    }

    // ─── 그랩 이벤트 ─────────────────────────────────────────────────

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            // 잡힘: 모든 이동 중단, 편대에서 제외
            IsGrabbed         = true;
            IsWaitingToReturn = false;
            IsReturning       = false;
            if (_returnCoroutine != null) StopCoroutine(_returnCoroutine);
        }
        else if (evt.Type == PointerEventType.Unselect)
        {
            // 놓임: 즉시 편대 카운트에 재합류(나머지 배가 미리 자리를 비킴)
            //       하지만 이 배 자체는 returnDelay 동안 frozen 유지
            IsGrabbed         = false;
            IsWaitingToReturn = true;   // ← frozen 시작 (이동 안 함)
            IsReturning       = false;
            _returnCoroutine  = StartCoroutine(WaitThenReturn());
        }
    }

    private IEnumerator WaitThenReturn()
    {
        // frozen 대기: 이 동안 나머지 배들이 4-ship 간격으로 이동 완료
        yield return new WaitForSeconds(returnDelay);

        if (IsGrabbed) { IsWaitingToReturn = false; yield break; }

        // frozen 해제 → 귀환 이동 시작 (느린 속도)
        IsWaitingToReturn = false;
        IsReturning       = true;
        // Formation이 SetFormationTarget을 계속 호출 → Update에서 천천히 따라감
    }
}
