/*
 * 작성자: 김승윤
 * 작성일: 2026.04.29
 * 역할: 거북선 움직임 설정 스크립트
 */
using UnityEngine;

public class MoveController : MonoBehaviour
{
    // 상하 움직임
    public float deeper = 0.01f; // 진폭
    public float upDownSpeed = 1f; // 움직임 속도

    // 회전 흔들림 (좌우, 앞뒤)
    public float leftRightAngle = 2f; // 좌우 흔들림
    public float frontBackAngle = 2f; // 앞뒤 흔들림
    public float rotSpeed = 1.5f; // 흔들림 속도

    private Vector3 startPos; // 초기 위치값
    private Quaternion startRot; // 초기 회전값

    void Start()
    {
        startPos = transform.position;
        startRot = transform.rotation;
    }

    void Update()
    {
        float newY = startPos.y + Mathf.Sin(Time.time * upDownSpeed) * deeper; // 상하 위치 계산
        transform.position = new Vector3(startPos.x, newY, startPos.z);

        float newX = Mathf.Sin(Time.time * rotSpeed) * frontBackAngle; // 앞뒤 흔들림 계산
        float newZ = Mathf.Cos(Time.time * rotSpeed * 0.9f) * leftRightAngle; // 좌우 흔들림 계산
        transform.rotation = startRot * Quaternion.Euler(newX, 0, newZ);
    }
}
