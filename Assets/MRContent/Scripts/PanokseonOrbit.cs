using System.Collections;
using UnityEngine;
using Oculus.Interaction;

/// <summary>
/// 판옥선 개별 궤도 + 그랩 인터랙션.
///
/// 상태 흐름:
///   Free      → Grabbed  : 자유 풀에서 제거, 나머지 천천히 펼쳐짐
///   Grabbed   → Waiting  : 놓는 순간. returnDelay 동안 자유 풀 미포함 (자리 비운 채 유지)
///   Waiting   → Returning: delay 만료 후 자유 풀 재합류, 천천히 대열로 돌아옴
///   Returning → Free     : 목표 각도에 수렴하면 완료
/// </summary>
public class PanokseonOrbit : MonoBehaviour
{
    [SerializeField] private float returnDelay      = 3f;
    [SerializeField] private float freeTrackSpeed   = 2f;   // 자유 오비터가 목표에 수렴하는 속도 (낮을수록 재배분이 느리고 자연스러움)
    [SerializeField] private float returnTrackSpeed = 1.2f; // 귀환 오비터의 수렴 속도 (더 천천히)

    private Transform _center;
    private float     _radius;

    private float _angleDeg;        // 현재 렌더 각도
    private float _targetAngleDeg;  // 매니저가 할당한 목표 각도

    // 외부에서 읽는 상태
    public bool IsGrabbed         { get; private set; }
    public bool IsWaitingToReturn { get; private set; }  // 대기 중 → 자유 풀 미포함

    private bool      _isReturning;
    private Coroutine _returnCoroutine;
    private Grabbable _grabbable;

    // ─── 초기화 ──────────────────────────────────────────────────────

    public void Init(Transform center, float radius, float startAngleDeg)
    {
        _center         = center;
        _radius         = radius;
        _angleDeg       = startAngleDeg;
        _targetAngleDeg = startAngleDeg;
    }

    // ─── Unity ───────────────────────────────────────────────────────

    private void Start()
    {
        _grabbable = GetComponent<Grabbable>();
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
        if (_center == null || IsGrabbed || IsWaitingToReturn) return;

        // 목표 각도로 천천히 수렴 (귀환 중이면 더 느리게)
        float speed = _isReturning ? returnTrackSpeed : freeTrackSpeed;
        _angleDeg = Mathf.LerpAngle(_angleDeg, _targetAngleDeg, Time.deltaTime * speed);
        transform.position = GetOrbitPosition(_angleDeg);

        if (_isReturning)
        {
            // 회전을 upright(Quaternion.identity)로 천천히 복원
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.identity,
                Time.deltaTime * returnTrackSpeed);

            // 목표에 충분히 수렴하면 귀환 완료
            if (Mathf.Abs(Mathf.DeltaAngle(_angleDeg, _targetAngleDeg)) < 1f)
            {
                transform.rotation = Quaternion.identity;
                _isReturning = false;
            }
        }
    }

    // ─── 매니저 인터페이스 ───────────────────────────────────────────

    /// <summary>PanokseonOrbitManager가 매 프레임 호출합니다.</summary>
    public void SetTargetAngle(float deg) => _targetAngleDeg = deg;

    // ─── 그랩 이벤트 ─────────────────────────────────────────────────

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            IsGrabbed         = true;
            IsWaitingToReturn = false;
            _isReturning      = false;
            if (_returnCoroutine != null) StopCoroutine(_returnCoroutine);
        }
        else if (evt.Type == PointerEventType.Unselect)
        {
            IsGrabbed         = false;
            IsWaitingToReturn = true;   // 대기 시작 — 자유 풀에서 제외 유지
            _returnCoroutine  = StartCoroutine(WaitThenReturn());
        }
    }

    private IEnumerator WaitThenReturn()
    {
        // 대기 중: 나머지 3개가 120° 간격을 유지한 채 도는 상태
        yield return new WaitForSeconds(returnDelay);

        if (IsGrabbed) { IsWaitingToReturn = false; yield break; }

        // 현재 월드 위치에서 가장 가까운 궤도 각도를 렌더 시작점으로
        Vector3 flat = transform.position - _center.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
            _angleDeg = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;

        IsWaitingToReturn = false;
        _isReturning      = true;
        // 이제 자유 풀 재합류 → 매니저가 목표 각도 할당 → 천천히 대열에 합류
    }

    // ─── 헬퍼 ───────────────────────────────────────────────────────

    private Vector3 GetOrbitPosition(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return _center.position + new Vector3(Mathf.Sin(rad) * _radius, 0f, Mathf.Cos(rad) * _radius);
    }
}
