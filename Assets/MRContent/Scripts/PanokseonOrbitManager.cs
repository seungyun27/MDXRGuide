using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 판옥선 전체의 궤도 각도를 중앙에서 관리합니다.
///
/// - 매 프레임 마스터 각도를 진행시키고
/// - Grabbed 및 WaitingToReturn 상태가 아닌 오비터에게만 등간격 목표 각도 할당
/// - 하나가 그랩되면 나머지가 천천히 균등 배분
/// - 대기 후 귀환 시작되면 나머지가 천천히 자리를 내어줌
/// </summary>
public class PanokseonOrbitManager : MonoBehaviour
{
    private readonly List<PanokseonOrbit> _all = new List<PanokseonOrbit>();
    private float _masterAngle = 0f;
    private float _speed;

    public void Init(float speed) => _speed = speed;
    public void Register(PanokseonOrbit o) => _all.Add(o);

    private void Update()
    {
        _masterAngle += _speed * Time.deltaTime;

        // Grabbed 중이거나 대기 중(WaitingToReturn)인 오비터는 제외
        var free = new List<PanokseonOrbit>();
        foreach (var o in _all)
            if (o != null && !o.IsGrabbed && !o.IsWaitingToReturn) free.Add(o);

        int n = free.Count;
        if (n == 0) return;

        float step = 360f / n;
        for (int i = 0; i < n; i++)
            free[i].SetTargetAngle(_masterAngle + i * step);
    }
}
