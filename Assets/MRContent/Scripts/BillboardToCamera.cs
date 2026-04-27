using UnityEngine;

/// <summary>
/// 항상 메인 카메라를 바라보는 빌보드 컴포넌트.
/// </summary>
public class BillboardToCamera : MonoBehaviour
{
    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // 카메라 방향을 바라보되 Y축 틸트 없이 수평 유지
        Vector3 dir = transform.position - cam.transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir);
    }
}
