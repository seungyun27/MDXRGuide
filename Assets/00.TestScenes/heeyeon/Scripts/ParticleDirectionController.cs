using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class ParticleDirectionController : MonoBehaviour
{
    [SerializeField] private GameObject fireParticleObject;
    [SerializeField] private Transform shipTransform;  

    [SerializeField] private float time = 6.0f;
    [SerializeField] private bool changed = false;
    float currentTime = 0.0f;
    public List<GameObject> Characters;
    

    [Tooltip("배가 이 각도 범위 안에 있으면 불 활성화")]
    [SerializeField] private float angleThreshold = 45f;

    private bool isFiring = false;

    private void Start()
    {
        // 시작할 때 안전하게 한 번 더 꺼줍니다.
        if (fireParticleObject != null)
        {
            fireParticleObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (fireParticleObject == null || shipTransform == null) return;

        // 배의 정면 방향
        Vector3 shipForward = shipTransform.forward;

        shipForward.y = 0;
        shipForward.Normalize();

        // 기준이 되는 월드의 오른쪽 방향 (Vector3.right = (1, 0, 0))
        Vector3 worldRight = Vector3.right;

        // 배의 정면과 월드 오른쪽(동쪽) 사이의 각도 계산
        float angleToRight = Vector3.Angle(shipForward, worldRight);

        // 정해진 오차 범위 내에서 오른쪽을 바라보고 있는지 판정
        bool isLookingRight = angleToRight < angleThreshold;

        if (isLookingRight)
        {
            if (!fireParticleObject.activeSelf)
            {
                fireParticleObject.SetActive(true);
                Debug.Log("🔥 배가 오른쪽을 봄: 파티클 오브젝트 활성화(ON)");
            }
            if (!changed)
            {
                currentTime += Time.deltaTime;

                if (currentTime >= time)
                {
                    Characters[0].SetActive(false);
                    Characters[1].SetActive(true);
                    changed = true;
                }
            }
        }
        else
        {
            if (fireParticleObject.activeSelf)
            {
                fireParticleObject.SetActive(false);
            }
        }

        //Debug.Log($"오차 각도: {angleToRight:F1}°, 오른쪽판단: {isLookingRight}, 시스템작동여부(isFiring): {isFiring}");
    }
}