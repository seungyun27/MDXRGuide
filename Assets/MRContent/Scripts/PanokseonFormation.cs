using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 학익진(鶴翼陣) ∧ 편대 관리자.
///
/// 배치 원칙:
///   - 중심(거북선 자리)은 비워 둠
///   - 내측 판옥선 → 거북선보다 약간 앞(viewer 방향)
///   - 외측 판옥선 → 내측보다 조금 더 앞
///   - 그랩 해제 즉시 나머지 배 재배치 (대기 중인 배도 카운트에 포함)
/// </summary>
public class PanokseonFormation : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────
    [Header("Formation (학익진 ∧)")]
    [Tooltip("좌우 spread 반각 (°). 전체 각도 = 2 × 이 값.")]
    public float halfSpreadDeg    = 85f;
    [Tooltip("중심에서 가장 가까운 배의 각도 (°). 클수록 중심 빈 공간이 넓어짐.")]
    public float centerGapDeg     = 20f;
    [Tooltip("편대 원호 반지름 (m). 거북선 중심에서 배까지 거리.")]
    public float arcRadius        = 0.60f;
    [Tooltip("내측 배가 앞으로 나오는 거리 (m).")]
    public float innerForward     = 0.03f;
    [Tooltip("외측 배가 앞으로 나오는 거리 (m). inner보다 크게 → V자 깊이.")]
    public float outerForward     = 0.18f;

    [Header("Water Bobbing")]
    public float bobbingAmplitude = 0.008f;
    public float bobbingSpeed     = 1.10f;
    public float bobbingPhaseStep = 1.40f;
    [Tooltip("앵커 기준 물 표면 Y 오프셋 (m). 배가 물 위에 떠 있는 높이.")]
    public float waterSurfaceOffset = 0.02f;

    // ─── 물 셰이더 파형 동기화 ─────────────────────────────────────────
    // ContentSpawner가 WaterQuest3 머티리얼을 할당합니다.
    [HideInInspector] public Material waterMaterial;
    [HideInInspector] public float baseYOffset = 0f;

    // ─── 내부 ─────────────────────────────────────────────────────────
    private readonly List<PanokseonFloater> _all = new List<PanokseonFloater>();
    private Transform _anchor;

    // ─── 초기화 ───────────────────────────────────────────────────────

    public void Initialize(Transform anchor)
    {
        _anchor = anchor;
    }

    public void Register(PanokseonFloater floater)
    {
        if (!_all.Contains(floater))
            _all.Add(floater);
        floater.Formation = this;
    }

    // ─── Update ───────────────────────────────────────────────────────

    private void Update()
    {
        if (_anchor == null || _all.Count == 0) return;

        // 그랩 중이 아닌 배 전체 포함 (대기 중인 배도 포함 → 즉시 재배치)
        var active = new List<PanokseonFloater>();
        foreach (var f in _all)
            if (!f.IsGrabbed)
                active.Add(f);

        int n = active.Count;
        if (n == 0) return;

        Vector3[] positions = BuildFormationPositions(n);

        for (int i = 0; i < n; i++)
        {
            Vector3 worldTarget = _anchor.TransformPoint(positions[i]);

            // Y: 물 파형 높이 + 위상차 흔들림
            float bobY   = Mathf.Sin(Time.time * bobbingSpeed + i * bobbingPhaseStep)
                           * bobbingAmplitude;
            float waterY = SampleWaterHeight(worldTarget);
            worldTarget.y = waterY + baseYOffset + waterSurfaceOffset + bobY;

            Quaternion shipRot = CalcShipFacingToCamera(worldTarget);
            active[i].SetFormationTarget(worldTarget, shipRot);
        }
    }

    // ─── 헬퍼 ─────────────────────────────────────────────────────────

    /// <summary>
    /// n개 배의 로컬 XZ 위치를 ∧ 패턴으로 생성합니다.
    /// - X: 좌우 spread (arcRadius × sin(angle))
    /// - Z: 앞방향 오프셋 (음수 = viewer 쪽)
    /// - 중심 ±centerGapDeg 범위는 항상 비워 둠
    /// </summary>
    private Vector3[] BuildFormationPositions(int n)
    {
        var pos = new Vector3[n];

        int nRight = (n + 1) / 2;   // 오른쪽 배 수
        int nLeft  = n / 2;         // 왼쪽 배 수

        int idx = 0;

        // 왼쪽: angle 범위 [-centerGapDeg, -halfSpreadDeg] (inner → outer)
        for (int i = 0; i < nLeft; i++)
        {
            float t       = nLeft == 1 ? 0f : (float)i / (nLeft - 1);
            float angle   = Mathf.Lerp(-centerGapDeg, -halfSpreadDeg, t);
            float forward = Mathf.Lerp(innerForward, outerForward, t);

            pos[idx++] = new Vector3(
                Mathf.Sin(angle * Mathf.Deg2Rad) * arcRadius,
                0f,
                -forward
            );
        }

        // 오른쪽: angle 범위 [+centerGapDeg, +halfSpreadDeg] (inner → outer)
        for (int i = 0; i < nRight; i++)
        {
            float t       = nRight == 1 ? 0f : (float)i / (nRight - 1);
            float angle   = Mathf.Lerp(centerGapDeg, halfSpreadDeg, t);
            float forward = Mathf.Lerp(innerForward, outerForward, t);

            pos[idx++] = new Vector3(
                Mathf.Sin(angle * Mathf.Deg2Rad) * arcRadius,
                0f,
                -forward
            );
        }

        return pos;
    }

    /// <summary>
    /// WaterQuest3.shader 파형 공식과 동일하게 계산 (싱크 보장).
    /// 머티리얼 없으면 앵커 Y로 폴백.
    /// </summary>
    private float SampleWaterHeight(Vector3 worldPos)
    {
        // Shader vertex displacement removed (small plane bobs as rigid body).
        // Water surface Y = anchor Y + waterSurfaceOffset (set by caller).
        return _anchor != null ? _anchor.position.y : 0f;
    }

    /// <summary>
    /// 모든 배가 카메라(플레이어)를 정면으로 바라보도록 방향을 계산합니다.
    /// 부채꼴 배치에서 일관된 함수 방향을 보장합니다.
    /// </summary>
    private Quaternion CalcShipFacingToCamera(Vector3 worldShipPos)
    {
        if (Camera.main == null) return Quaternion.identity;

        Vector3 toCamera = Camera.main.transform.position - worldShipPos;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.001f) return Quaternion.identity;

        return Quaternion.LookRotation(toCamera.normalized);
    }
}
