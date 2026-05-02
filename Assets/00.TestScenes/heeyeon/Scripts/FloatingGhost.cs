using UnityEngine;

public class FloatingGhost : MonoBehaviour
{
    [SerializeField] private float floatSpeed = 2f;      // 떠다니는 속도
    [SerializeField] private float floatHeight = 0.5f;   // 위아래 이동 범위
    [SerializeField] private float rotationSpeed = 20f;  // 회전 속도 (선택)

    private Vector3 _startPos;

    private void Start()
    {
        _startPos = transform.position;
    }

    private void Update()
    {
        // 사인파로 위아래 이동
        float newY = _startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(_startPos.x, newY, _startPos.z);

        // 선택: 천천히 회전
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
    }
}