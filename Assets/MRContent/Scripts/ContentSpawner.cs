using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ContentType { None, Geobukseon, Bugeo }

/// <summary>
/// 탐지된 3D 월드 위치에 앵커를 부드럽게 따라가며 콘텐츠를 배치합니다.
/// 2단계 스무딩: 탐지 노이즈 EMA 필터 → 앵커 Lerp 이동.
/// </summary>
public class ContentSpawner : MonoBehaviour
{
    [Header("Geobukseon (거북선) Prefabs")]
    [Tooltip("거북선 본체 — 비어 있으면 Cube 대체")]
    [SerializeField] private GameObject geobukseonMainPrefab;
    [Tooltip("판옥선 — 비어 있으면 작은 Cube 대체")]
    [SerializeField] private GameObject panokseonPrefab;
    [SerializeField] private int   panokseonCount = 8;
    [SerializeField] private ParticleSystem seaParticle;

    [Header("Formation (학익진 ∧)")]
    [Tooltip("편대 원호 반지름 (m).")]
    [SerializeField] private float formationArcRadius     = 0.60f;
    [Tooltip("좌우 반각 (°). 전체 spread = 2×값. 기본 85 = ±85°")]
    [SerializeField] private float formationHalfSpreadDeg = 85f;
    [Tooltip("중심 빈 공간 각도 (°). 클수록 거북선 자리가 더 비어 보임.")]
    [SerializeField] private float formationCenterGapDeg  = 20f;

    [Header("Content Y Offset")]
    [Tooltip("물·판옥선·파티클 전체를 앵커 기준으로 올리거나 내립니다 (m).\n거북선 모델이 반쯤 파묻힐 때 음수 값으로 내리세요. 예: -0.05")]
    [SerializeField] private float contentBaseYOffset = 0f;

    [Header("Water")]
    [Tooltip("Bitgem StylisedWater 머티리얼을 여기에 드래그하세요.\n경로: Assets/Bitgem/StylisedWater/URP/Materials/example-water-01.mat\n설정 시 WaterQuest3 대신 이 머티리얼을 사용합니다.")]
    [SerializeField] private Material waterSurfaceMaterial;
    [Tooltip("WaterQuest3Mat 머티리얼이 적용된 WaterPlane 프리팹 (waterSurfaceMaterial이 없을 때 폴백).")]
    [SerializeField] private GameObject waterPlanePrefab;
    [Tooltip("물 표면 스케일 (1 = 1m×1m). 더 넓게 하려면 2, 3 등 조절.")]
    [SerializeField] private float waterScale     = 1f;
    [Tooltip("물 표면만 추가로 올리거나 내릴 Y 오프셋 (m). contentBaseYOffset에 더해집니다.")]
    [SerializeField] private float waterYOffset   = 0f;
    [Tooltip("물 Cube의 Y 두께 (m). waterSurfaceMaterial 사용 시 무시됨.")]
    [SerializeField] private float waterThickness = 0.04f;

    [Header("Bugeo (북어) Prefabs")]
    [Tooltip("북어 본체 — 비어 있으면 Sphere 대체")]
    [SerializeField] private GameObject bugeoMainPrefab;
    [Tooltip("액운 유령 — 비어 있으면 반투명 Sphere 대체")]
    [SerializeField] private GameObject ghostPrefab;
    [SerializeField] private int   ghostCount       = 5;
    [SerializeField] private float ghostSpawnRadius = 0.55f;
    [SerializeField] private float ghostAbsorbSpeed = 0.25f;

    [Header("Smooth Follow")]
    [Tooltip("1단계: 탐지 노이즈 필터 속도. 낮을수록 떨림 감소 (권장: 2~4)")]
    [SerializeField] private float targetFilterSpeed   = 3f;
    [Tooltip("2단계: 앵커 이동 속도. 높을수록 목표에 빠르게 도달 (권장: 5~10)")]
    [SerializeField] private float positionSmoothSpeed = 7f;
    [Tooltip("회전 보간 속도")]
    [SerializeField] private float rotationSmoothSpeed = 4f;

    [Header("Occlusion (실물 오클루더)")]
    [Tooltip("ON: 실물 객체 자리에 보이지 않는 깊이 메시를 배치 → 가상 객체가 실물 뒤로 가면 가려짐")]
    [SerializeField] private bool     useOccluder    = true;
    [Tooltip("Assets/MRContent/Shaders/DepthOccluderMat 을 여기에 드래그하세요.")]
    [SerializeField] private Material occluderMaterial;
    [Tooltip("오클루더 기본 크기(m). 탐지가 스케일을 못 줄 때 폴백으로 사용.")]
    [SerializeField] private Vector3  occluderSize    = new Vector3(0.07f, 0.06f, 0.07f);
    [Tooltip("탐지 스케일에 곱하는 여유 배율 (기본 1.1 = 10% 크게)")]
    [SerializeField] private float    occluderPadding = 1.1f;
    [Tooltip("판옥선용 머티리얼. 비워두면 URP/Lit Shader.Find 폴백.")]
    [SerializeField] private Material panokseonMaterial;

    // ─── 내부 상태 ──────────────────────────────────────────────────
    private GameObject       _anchorRoot;
    private Transform        _sceneAnchor;
    private ContentType      _activeContent    = ContentType.None;
    private List<GameObject> _spawnedObjects   = new List<GameObject>();
    private List<Coroutine>  _activeCoroutines = new List<Coroutine>();

    // 2단계 스무딩
    private Vector3    _rawTargetPos;                       // 탐지 원시 위치 (노이즈)
    private Quaternion _rawTargetRot  = Quaternion.identity;
    private Vector3    _targetPos;                          // EMA 필터링된 목표
    private Quaternion _targetRot     = Quaternion.identity;
    private bool       _isFollowing   = false;

    // 오클루더 참조 (런타임 리사이즈용)
    private Transform _occluderTransform;
    private Vector3   _detectedScale  = Vector3.zero;       // 탐지로부터 받은 스케일 (0 = 미수신)

    private Material _urpLitMat;
    private Material _occluderMat;

    public bool HasSceneAnchor => _sceneAnchor != null;
    public bool IsActive       => _anchorRoot  != null;

    // ─── Unity ───────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isFollowing || _anchorRoot == null) return;

        // 1단계: 탐지 노이즈 EMA 필터링
        _targetPos = Vector3.Lerp(_targetPos, _rawTargetPos, Time.deltaTime * targetFilterSpeed);
        float rawYaw      = _rawTargetRot.eulerAngles.y;
        float filteredYaw = Mathf.LerpAngle(_targetRot.eulerAngles.y, rawYaw,
                                             Time.deltaTime * targetFilterSpeed);
        _targetRot = Quaternion.Euler(0f, filteredYaw, 0f);

        // 2단계: 앵커를 필터링된 목표로 부드럽게 이동
        _anchorRoot.transform.position = Vector3.Lerp(
            _anchorRoot.transform.position, _targetPos,
            Time.deltaTime * positionSmoothSpeed);

        float currentYaw = _anchorRoot.transform.eulerAngles.y;
        float smoothYaw  = Mathf.LerpAngle(currentYaw, filteredYaw,
                                            Time.deltaTime * rotationSmoothSpeed);
        _anchorRoot.transform.eulerAngles = new Vector3(0f, smoothYaw, 0f);
    }

    // ─── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// 탐지 프레임마다 호출 — 원시 목표를 갱신. Update()에서 필터링 후 이동.
    /// </summary>
    public void SmoothUpdateTarget(Vector3 worldPos, Quaternion worldRot)
    {
        _rawTargetPos = worldPos;
        _rawTargetRot = worldRot;
        _isFollowing  = true;
    }

    /// <summary>
    /// 오클루더 크기를 탐지 스케일에 맞게 업데이트합니다.
    /// ObjectDetectionBridge가 TryProject 결과를 전달할 때 호출합니다.
    /// </summary>
    public void UpdateOccluderScale(Vector3 detectedWorldScale)
    {
        _detectedScale = detectedWorldScale * occluderPadding;
        _detectedScale.z = 0.01f;
        if (_occluderTransform != null)
            _occluderTransform.localScale = _detectedScale;
    }

    /// <summary>SceneAnchorManager가 TABLE 앵커를 찾으면 호출합니다.</summary>
    public void SetAnchor(Transform anchor)
    {
        _sceneAnchor = anchor;
        Debug.Log($"[ContentSpawner] 씬 앵커 설정: {anchor.position}");
    }

    /// <summary>씬 언더스탠딩 앵커 위치에 콘텐츠를 스폰합니다.</summary>
    public void Activate(ContentType type)
    {
        if (_sceneAnchor == null)
        {
            Debug.LogWarning("[ContentSpawner] 씬 앵커 없음");
            return;
        }
        if (_activeContent == type) return;

        ClearSpawnedOnly();

        _anchorRoot = new GameObject("ContentAnchorRoot");
        _anchorRoot.transform.SetParent(_sceneAnchor, worldPositionStays: false);
        _anchorRoot.transform.localPosition = Vector3.zero;
        _anchorRoot.transform.localRotation = Quaternion.identity;

        _isFollowing = false;
        SpawnContent(type);
        Debug.Log($"[ContentSpawner] 씬 앵커 기준 스폰: {_sceneAnchor.position}, type={type}");
    }

    /// <summary>직접 월드 좌표로 최초 1회 스폰합니다.</summary>
    public void PlaceAndActivate(Vector3 worldPos, Quaternion worldRot, ContentType type)
    {
        if (_activeContent == type && _anchorRoot != null)
        {
            SmoothUpdateTarget(worldPos, worldRot);
            return;
        }

        ClearSpawnedOnly();

        _anchorRoot = new GameObject("ContentAnchorRoot");
        _anchorRoot.transform.position = worldPos;
        _anchorRoot.transform.rotation = Quaternion.Euler(0f, worldRot.eulerAngles.y, 0f);

        // 초기값: 필터 타겟과 원시 타겟 모두 같은 위치로 초기화 (튐 방지)
        _rawTargetPos = worldPos;
        _rawTargetRot = worldRot;
        _targetPos    = worldPos;
        _targetRot    = Quaternion.Euler(0f, worldRot.eulerAngles.y, 0f);
        _isFollowing  = true;

        SpawnContent(type);
        Debug.Log($"[ContentSpawner] 최초 스폰: {worldPos}, type={type}");
    }

    /// <summary>콘텐츠만 제거. 씬 앵커는 유지됩니다.</summary>
    public void ClearContent() => ClearSpawnedOnly();

    /// <summary>콘텐츠 + 씬 앵커 모두 완전 초기화.</summary>
    public void ClearAll()
    {
        ClearSpawnedOnly();
        _sceneAnchor = null;
        _isFollowing = false;
    }

    // ─── 내부 스폰 ───────────────────────────────────────────────────

    private void SpawnContent(ContentType type)
    {
        _activeContent = type;
        if (type == ContentType.Geobukseon) SpawnGeobukseonSet();
        else if (type == ContentType.Bugeo)  SpawnBugeoSet();
    }

    private void ClearSpawnedOnly()
    {
        foreach (var co  in _activeCoroutines) { if (co  != null) StopCoroutine(co); }
        foreach (var obj in _spawnedObjects)   { if (obj != null) Destroy(obj); }
        _activeCoroutines.Clear();
        _spawnedObjects.Clear();
        if (seaParticle != null) seaParticle.Stop();
        if (_anchorRoot != null) { Destroy(_anchorRoot); _anchorRoot = null; }
        _occluderTransform = null;
        _activeContent     = ContentType.None;
    }

    // ─── 거북선 콘텐츠 ──────────────────────────────────────────────

    private void SpawnGeobukseonSet()
    {
        SpawnOccluder();

        // ── 바다 평면 (WaterQuest3 셰이더 적용 Plane) ───────────────
        SpawnWaterPlane();

        // ── 학익진 편대 매니저 ────────────────────────────────────────
        var formation = _anchorRoot.AddComponent<PanokseonFormation>();
        formation.arcRadius       = formationArcRadius;
        formation.halfSpreadDeg   = formationHalfSpreadDeg;
        formation.centerGapDeg    = formationCenterGapDeg;
        formation.baseYOffset     = contentBaseYOffset;
        // waterMaterial은 프리팹으로 관리하므로 formation에는 미전달 (앵커 Y 기반 fallback 사용)
        formation.Initialize(_anchorRoot.transform);

        // ── 판옥선 생성 ───────────────────────────────────────────────
        for (int i = 0; i < panokseonCount; i++)
        {
            // 초기 위치: 편대 각도에 맞춰 배치 (첫 프레임 튐 방지)
            float t          = panokseonCount == 1 ? 0.5f : (float)i / (panokseonCount - 1);
            float angleDeg   = Mathf.Lerp(-formationHalfSpreadDeg,
                                           formationHalfSpreadDeg, t);
            float rad        = angleDeg * Mathf.Deg2Rad;
            var   initOffset = new Vector3(
                Mathf.Sin(rad) * formationArcRadius, 0f,
                -formationArcRadius * 0.1f);

            var panok = MakePrimitive(panokseonPrefab, PrimitiveType.Cube,
                initOffset, Quaternion.identity, new Vector3(0.06f, 0.03f, 0.06f));
            panok.name = $"Panokseon_{i}";
            ApplyColor(panok, new Color(0.55f, 0.35f, 0.12f));

            var floater = panok.GetComponent<PanokseonFloater>();
            if (floater == null) floater = panok.AddComponent<PanokseonFloater>();
            formation.Register(floater);
        }

        SpawnLabel("거북선", new Vector3(0f, 0.2f, 0f));

        if (seaParticle != null)
        {
            seaParticle.transform.SetParent(_anchorRoot.transform);
            seaParticle.transform.localPosition = new Vector3(0f, contentBaseYOffset, 0f);
            seaParticle.Play();
        }

        Debug.Log("[ContentSpawner] 거북선 — 학익진 편대 활성화");
    }

    // ─── 물 평면 생성 ───────────────────────────────────────────────

    private void SpawnWaterPlane()
    {
        // ── Bitgem StylisedWater 머티리얼이 지정된 경우 ───────────────
        if (waterSurfaceMaterial != null)
        {
            SpawnBitgemWater();
            return;
        }

        // ── 폴백: WaterQuest3 Cube ────────────────────────────────────
        SpawnWaterQuest3Cube();
    }

    /// <summary>Bitgem StylisedWater 머티리얼을 Unity Plane에 적용합니다.</summary>
    private void SpawnBitgemWater()
    {
        // Plane 프리미티브 (10×10유닛 기본). waterScale=1 → 1m×1m
        var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
        surface.name = "WaterSurface";
        var col = surface.GetComponent<Collider>();
        if (col != null) Destroy(col);

        surface.transform.SetParent(_anchorRoot.transform);
        surface.transform.localPosition = new Vector3(0f, contentBaseYOffset + waterYOffset, 0f);
        surface.transform.localRotation = Quaternion.identity;
        // Unity Plane = 10유닛 → 1유닛=0.1m. waterScale=1 → 0.1 scale → 1m×1m
        float s = 0.1f * waterScale;
        surface.transform.localScale = new Vector3(s, 1f, s);

        // 원본 shared material을 건드리지 않도록 인스턴스 복사
        surface.GetComponent<MeshRenderer>().material = new Material(waterSurfaceMaterial);

        _spawnedObjects.Add(surface);
        Debug.Log($"[ContentSpawner] WaterSurface(Plane/Bitgem) 스폰 완료: " +
                  $"셰이더={waterSurfaceMaterial.shader.name}, size={waterScale}m");
    }

    /// <summary>WaterQuest3 셰이더를 Cube에 적용하는 기존 방식 (폴백).</summary>
    private void SpawnWaterQuest3Cube()
    {
        Material srcMat = Resources.Load<Material>("WaterQuest3Mat");
        if (srcMat == null && waterPlanePrefab != null)
        {
            var prefabMr = waterPlanePrefab.GetComponentInChildren<MeshRenderer>();
            if (prefabMr != null) srcMat = prefabMr.sharedMaterial;
        }
        if (srcMat == null)
        {
            var shader = Shader.Find("Custom/WaterQuest3");
            if (shader != null) srcMat = new Material(shader);
            else { Debug.LogError("[ContentSpawner] WaterQuest3 셰이더를 찾을 수 없습니다!"); return; }
        }

        Material mat = new Material(srcMat);
        mat.SetFloat("_FoamWidth",    0f);
        mat.SetColor("_ShallowColor", new Color(0.10f, 0.55f, 0.80f, 1f));
        mat.SetColor("_DeepColor",    new Color(0.02f, 0.15f, 0.40f, 1f));

        // Plane 프리미티브 사용 (10×10 쿼드 = 121 버텍스 → 버텍스 웨이브 표현 가능)
        var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
        surface.name = "WaterSurface";
        var col = surface.GetComponent<Collider>();
        if (col != null) Destroy(col);

        surface.transform.SetParent(_anchorRoot.transform);
        surface.transform.localPosition = new Vector3(0f, contentBaseYOffset + waterYOffset, 0f);
        surface.transform.localRotation = Quaternion.identity;
        // Unity Plane = 10유닛 기본. scale 0.1 × waterScale → 1m × waterScale
        float s = 0.1f * waterScale;
        surface.transform.localScale = new Vector3(s, 1f, s);
        surface.GetComponent<MeshRenderer>().material = mat;

        _spawnedObjects.Add(surface);
        Debug.Log($"[ContentSpawner] WaterSurface(Plane/WaterQuest3) 스폰: size={waterScale}m");
    }

    /// <summary>
    /// 현재 탐지 위치로 앵커를 즉시 스냅합니다 (콘텐츠 재생성 없음).
    /// ObjectDetectionBridge.ReSnapToLatestDetection()이 호출합니다.
    /// </summary>
    public void SnapAnchor(Vector3 worldPos, Quaternion worldRot)
    {
        if (_anchorRoot == null) return;

        _anchorRoot.transform.position = worldPos;
        float yaw = worldRot.eulerAngles.y;
        _anchorRoot.transform.eulerAngles = new Vector3(0f, yaw, 0f);

        // 필터 상태도 동기화 (다음 프레임 튐 방지)
        _rawTargetPos = worldPos;
        _rawTargetRot = worldRot;
        _targetPos    = worldPos;
        _targetRot    = Quaternion.Euler(0f, yaw, 0f);
        _isFollowing  = false;

        Debug.Log($"[ContentSpawner] 앵커 재스냅: {worldPos}");
    }

    // ─── 북어 콘텐츠 ────────────────────────────────────────────────

    private void SpawnBugeoSet()
    {
        var main = MakePrimitive(bugeoMainPrefab, PrimitiveType.Sphere,
            Vector3.up * 0.16f, Quaternion.identity,
            new Vector3(0.07f, 0.055f, 0.14f));
        ApplyColor(main, new Color(0.84f, 0.68f, 0.48f));

        for (int i = 0; i < ghostCount; i++)
        {
            var rnd = Random.insideUnitSphere * ghostSpawnRadius;
            rnd.y   = Mathf.Max(rnd.y, 0.05f);

            var ghost = MakePrimitive(ghostPrefab, PrimitiveType.Sphere,
                rnd, Quaternion.identity, Vector3.one * 0.038f);
            ApplyGhostMaterial(ghost);
            _activeCoroutines.Add(StartCoroutine(AbsorbLoop(ghost, main.transform, i * 1.4f)));
        }

        Debug.Log("[ContentSpawner] 북어 콘텐츠 활성화");
    }

    // ─── 코루틴 ─────────────────────────────────────────────────────

    private IEnumerator AbsorbLoop(GameObject ghost, Transform target, float delay)
    {
        yield return new WaitForSeconds(delay);
        while (true)
        {
            if (ghost == null || target == null || _anchorRoot == null) yield break;

            ghost.transform.position = Vector3.MoveTowards(
                ghost.transform.position, target.position, ghostAbsorbSpeed * Time.deltaTime);

            if (Vector3.Distance(ghost.transform.position, target.position) < 0.012f)
            {
                ghost.SetActive(false);
                yield return new WaitForSeconds(Random.Range(1.5f, 3.2f));
                if (ghost != null && _anchorRoot != null)
                {
                    var rnd = Random.insideUnitSphere * ghostSpawnRadius;
                    rnd.y   = Mathf.Max(rnd.y, 0.05f);
                    ghost.transform.position = _anchorRoot.transform.position + rnd;
                    ghost.SetActive(true);
                }
            }
            yield return null;
        }
    }

    // ─── 헬퍼 ───────────────────────────────────────────────────────

    private GameObject MakePrimitive(GameObject prefab, PrimitiveType fallback,
        Vector3 localPos, Quaternion localRot, Vector3 scale)
    {
        GameObject obj = prefab != null
            ? Instantiate(prefab, _anchorRoot.transform)
            : GameObject.CreatePrimitive(fallback);

        obj.transform.SetParent(_anchorRoot.transform);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = localRot;
        obj.transform.localScale    = scale;
        _spawnedObjects.Add(obj);
        return obj;
    }

    private void SpawnOccluder()
    {
        if (!useOccluder) return;

        var mat = occluderMaterial != null ? occluderMaterial : GetOccluderMat();
        if (mat == null) return;

        // 탐지 스케일을 받은 경우 그 크기 사용, 없으면 인스펙터 기본값
        var finalScale = (_detectedScale != Vector3.zero) ? _detectedScale : occluderSize;

        var occluder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var col = occluder.GetComponent<Collider>();
        if (col != null) Destroy(col);

        occluder.transform.SetParent(_anchorRoot.transform);
        occluder.transform.localPosition = Vector3.zero;
        occluder.transform.localRotation = Quaternion.identity;
        occluder.transform.localScale    = finalScale;

        occluder.GetComponent<Renderer>().material = mat;
        occluder.name = "OccluderMesh";

        _occluderTransform = occluder.transform; // 나중에 리사이즈 가능하도록 저장
        _spawnedObjects.Add(occluder);
    }

    private Material GetOccluderMat()
    {
        if (_occluderMat != null) return _occluderMat;
        var shader = Shader.Find("Custom/DepthOccluder");
        if (shader == null)
        {
            Debug.LogWarning("[ContentSpawner] DepthOccluder 셰이더를 찾을 수 없습니다.");
            return null;
        }
        _occluderMat = new Material(shader);
        return _occluderMat;
    }

    private Material GetUrpMat()
    {
        if (_urpLitMat != null) return _urpLitMat;

        if (panokseonMaterial != null)
        {
            _urpLitMat = panokseonMaterial;
            return _urpLitMat;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        _urpLitMat = new Material(shader);
        return _urpLitMat;
    }

    private void ApplyColor(GameObject obj, Color color)
    {
        var r = obj.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(GetUrpMat());
        mat.SetColor("_BaseColor", color);
        r.material = mat;
    }

    private void ApplyGhostMaterial(GameObject obj)
    {
        var r = obj.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(GetUrpMat());
        mat.SetColor("_BaseColor", new Color(0.55f, 0.78f, 1f, 0.45f));
        r.material = mat;
    }

    private void SpawnLabel(string text, Vector3 localOffset)
    {
        var labelObj = new GameObject("ContentLabel");
        labelObj.transform.SetParent(_anchorRoot.transform);
        labelObj.transform.localPosition = localOffset;
        labelObj.transform.localRotation = Quaternion.identity;

        var tm = labelObj.AddComponent<TextMesh>();
        tm.text          = text;
        tm.fontSize      = 48;
        tm.characterSize = 0.004f;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.alignment     = TextAlignment.Center;
        tm.color         = Color.white;

        labelObj.AddComponent<BillboardToCamera>();
        _spawnedObjects.Add(labelObj);
    }
}
