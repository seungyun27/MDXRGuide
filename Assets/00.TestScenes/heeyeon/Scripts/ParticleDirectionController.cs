using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class ParticleDirectionController : MonoBehaviour
{
    [SerializeField] private ParticleSystem fireParticleSystem;
    [SerializeField] private Transform shipTransform;  
    [SerializeField] private Transform leftDirection;  
    [SerializeField] private Transform rightDirection;
    [SerializeField] private float time = 6.0f;
    [SerializeField] private bool changed = false;
    float currentTime = 0.0f;
    public List<GameObject> Characters;
    

    [Tooltip("배가 이 각도 범위 안에 있으면 불 활성화")]
    [SerializeField] private float angleThreshold = 90f;

    private void Update()
    {
        if (fireParticleSystem == null || shipTransform == null) return;

        // 배의 정면 방향
        Vector3 shipForward = shipTransform.forward;

        // 왼쪽/오른쪽 방향 벡터
        Vector3 leftDir = (leftDirection != null) ? leftDirection.position - shipTransform.position : -shipTransform.right;
        Vector3 rightDir = (rightDirection != null) ? rightDirection.position - shipTransform.position : shipTransform.right;

        leftDir.Normalize();
        rightDir.Normalize();

        // 배 정면과 왼쪽/오른쪽 방향의 각도 계산
        float angleToLeft = Vector3.Angle(shipForward, leftDir);
        float angleToRight = Vector3.Angle(shipForward, rightDir);

        // 왼쪽을 바라보면(왼쪽 각도 < 임계값) 불 활성화
        // 오른쪽을 바라보면 불 활성화
        bool isLookingLeft = angleToLeft < angleThreshold;
        bool isLookingRight = angleToRight < angleThreshold;

        if (isLookingLeft)
        {
            if (!fireParticleSystem.isPlaying)
            {
                fireParticleSystem.Play();
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
            if (fireParticleSystem.isPlaying)
            {
                fireParticleSystem.Stop();
            }
        }

        Debug.Log($"Left: {angleToLeft:F1}°, Right: {angleToRight:F1}°, Playing: {fireParticleSystem.isPlaying}");
    }
}