using UnityEngine;
using Meta.XR.EnvironmentDepth;

/// <summary>
/// EnvironmentDepthManager 상태를 ADB logcat으로 출력하는 진단 스크립트.
/// 씬의 아무 오브젝트에 붙여 빌드 후 로그를 확인하세요.
/// 확인 후 이 컴포넌트는 제거해도 됩니다.
/// </summary>
public class DepthOcclusionDebugger : MonoBehaviour
{
    private EnvironmentDepthManager _mgr;
    private float _timer;
    private bool _firstLog = true;

    private void Start()
    {
        _mgr = FindObjectOfType<EnvironmentDepthManager>();

        if (_mgr == null)
        {
            Debug.LogError("[DepthDebug] EnvironmentDepthManager를 씬에서 찾을 수 없습니다.");
            return;
        }

        Debug.Log($"[DepthDebug] ====== 초기 상태 ======\n" +
                  $"IsSupported      = {EnvironmentDepthManager.IsSupported}\n" +
                  $"IsDepthAvailable = {_mgr.IsDepthAvailable}\n" +
                  $"OcclusionMode    = {_mgr.OcclusionShadersMode}\n" +
                  $"enabled          = {_mgr.enabled}");

        if (!EnvironmentDepthManager.IsSupported)
            Debug.LogError("[DepthDebug] ★ IsSupported=false → 이 기기/펌웨어에서 Depth API 미지원. 오클루전 불가.");

        if (_mgr.OcclusionShadersMode == OcclusionShadersMode.None)
            Debug.LogWarning("[DepthDebug] ★ OcclusionShadersMode=None → 오클루전 꺼진 상태입니다. HardOcclusion으로 변경 필요.");
    }

    private void Update()
    {
        if (_mgr == null) return;

        _timer += Time.deltaTime;

        // 최초 3초 후 + 이후 5초마다
        if ((_firstLog && _timer > 3f) || (!_firstLog && _timer > 5f))
        {
            _timer    = 0f;
            _firstLog = false;

            Debug.Log($"[DepthDebug] IsSupported={EnvironmentDepthManager.IsSupported} | " +
                      $"IsDepthAvailable={_mgr.IsDepthAvailable} | " +
                      $"OcclusionMode={_mgr.OcclusionShadersMode}");

            if (!_mgr.IsDepthAvailable)
                Debug.LogWarning("[DepthDebug] ★ IsDepthAvailable=false → 깊이 데이터가 아직 없음. " +
                                 "USE_SCENE 권한 허가 여부 또는 기기 지원 여부를 확인하세요.");
        }
    }
}
