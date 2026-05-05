using UnityEngine;

public class FloatingGhost : MonoBehaviour
{
    [SerializeField] private float floatSpeed = 1.2f;      // 떠다니는 속도
    [SerializeField] private float floatHeight = 0.015f;   // 위아래 이동 범위
    [Header("유령 회전 설정")]
    [SerializeField] private float yRotationRange = 3f;  // 좌우 도리도리 흔들림 각도 (Y축)
    [SerializeField] private float yRotationSpeed = 1.5f; // 좌우 흔들림 속도
    [SerializeField] private float zRotationRange = 3f;   // 좌우 기우뚱 기우뚱 각도 (Z축)
    [SerializeField] private float zRotationSpeed = 2f;   // 기우뚱 속도

    private cshFollowPollack followPollack;


    private Vector3 _startPos;
    private Quaternion _startRot;

    private void Start()
    {
        _startPos = transform.position;
        _startRot = transform.localRotation; // 초기 회전값 저장
        followPollack = GetComponent<cshFollowPollack>();
    }

    private void Update()
    {

        if (followPollack!=null && followPollack.IsFollowing)
        {
            return;
        }
        // 사인파로 위아래 이동
        float newY = _startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(_startPos.x, newY, _startPos.z);

        // 2. [변경됨] 사인파로 자연스럽게 좌우 + 기우뚱 회전
        float sinY = Mathf.Sin(Time.time * yRotationSpeed) * yRotationRange;
        float sinZ = Mathf.Sin(Time.time * zRotationSpeed) * zRotationRange;

        // 원래 회전값에 실시간 오프셋(Y, Z)을 곱해 자연스러운 각도 생성
        transform.localRotation = _startRot * Quaternion.Euler(0, sinY, sinZ);
    }
}