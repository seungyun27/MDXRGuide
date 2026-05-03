using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class cshFollowPollack : MonoBehaviour
{
    public Transform Pollack;
    public Transform mouth;
    public float facingThreshold = 0.95f;
    public float detectionDistance = 20f;
    public float followSpeed = 5f;
    public float acceleration = 2f;
    public float arrivalDistance = 0.1f;

    public AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    public float minScaleRatio = 0f;

    private bool isFollow = false;
    private float currentSpeed;

    // 크기 축소용 상태값
    private Vector3 initialScale;
    private float initialDistance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        initialScale = transform.localScale;
    }

    // Update is called once per frame
    void Update()
    {
        if (Pollack == null) return;

        if (!isFollow)
            CheckFacingPollack();
        else
            MoveTowardPollack();
    }

    void CheckFacingPollack()
    {
        Vector3 toPollack = transform.position - Pollack.position;

        // 거리 체크
        if (toPollack.magnitude > detectionDistance) return;

        // 북어의 forward가 액운을 향하는지 즉, 북어를 기준으로 마주보고 있는지 여부를 내적으로 판정
        float dot = Vector3.Dot(Pollack.transform.forward, toPollack.normalized);
        if (dot < facingThreshold) return;

        currentSpeed = followSpeed;

        // 흡입 시작 시점의 거리를 기억해서 축소 비율 계산에 사용
        initialDistance = Vector3.Distance(transform.position, mouth.position);
        if (initialDistance < 0.0001f) initialDistance = 0.0001f;

        isFollow = true;
    }

    void MoveTowardPollack()
    {
        if (mouth == null) { isFollow = false; return; }

        float distance = Vector3.Distance(transform.position, mouth.position);
        if (distance < arrivalDistance)
        {
            // 최종 스케일로 스냅
            transform.localScale = initialScale * minScaleRatio;
            OnArrive();
            return;
        }

        // 점점 빨리 빨려들어가도록 가속
        currentSpeed += acceleration * Time.deltaTime;

        transform.position = Vector3.MoveTowards(
            transform.position,
            mouth.position,
            currentSpeed * Time.deltaTime
        );


        // 액운의 크기를 조절하려면 여기에 코드 추가
        // 진행도(0 = 시작, 1 = 도착)에 따른 스케일 적용
        float progress = 1f - Mathf.Clamp01(distance / initialDistance); // 0~1사이의 값으로 조절
        float curveValue = scaleCurve.Evaluate(progress); // 1 → 0 으로 자연스럽게
        float scaleRatio = Mathf.Lerp(minScaleRatio, 1f, curveValue);
        transform.localScale = initialScale * scaleRatio;
    }

    void OnArrive()
    {
        isFollow = false;
        // 도착 시 동작(충돌처리로 구현하는 가능)
        Destroy(gameObject);
    }
}
