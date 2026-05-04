using UnityEngine;
using TMPro;

public class NarrationManager : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip geobukseonClip01;  // 거북선 첫번째 MP3
    [SerializeField] private AudioClip geobukseonClip02;  // 거북선 두번째 MP3
    [SerializeField] private AudioClip bugeoClip;         // 북어 MP3

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI narrationText;

    [Header("텍스트")]
    [SerializeField] private string geobukseonMessage = "거북선의 꼬리를 잡아 방향을 움직여 보세요!";
    [SerializeField] private string bugeoMessage = "북어의 원형 받침대를 움직여서 입이 액운을 향하게 해보세요!";

    [Header("Settings")]
    [SerializeField] private float secondClipDelay = 10f;  // 텍스트 표시 후 두번째 MP3까지 대기 시간

    private bool _hasPlayed = false;
    private bool _waitingForGeobukseonEnd = false;
    private bool _waitingForSecondClip = false;
    private float _secondClipTimer = 0f;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (narrationText != null)
            narrationText.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 거북선 첫번째 MP3 끝나면 텍스트 표시
        if (_waitingForGeobukseonEnd && !audioSource.isPlaying)
        {
            _waitingForGeobukseonEnd = false;
            _waitingForSecondClip = true;
            _secondClipTimer = 0f;
            ShowText(geobukseonMessage);
            Debug.Log("[NarrationManager] 텍스트 표시 — 두번째 MP3 대기 시작");
        }

        // 텍스트 표시 후 10초 뒤 두번째 MP3 재생
        if (_waitingForSecondClip)
        {
            _secondClipTimer += Time.deltaTime;
            if (_secondClipTimer >= secondClipDelay)
            {
                _waitingForSecondClip = false;
                PlaySecondClip();
            }
        }
    }

    public void PlayNarration(ContentType type)
    {
        if (_hasPlayed) return;
        _hasPlayed = true;

        if (narrationText != null)
            narrationText.gameObject.SetActive(false);

        switch (type)
        {
            case ContentType.Geobukseon:
                if (geobukseonClip01 != null)
                {
                    audioSource.clip = geobukseonClip01;
                    audioSource.Play();
                    _waitingForGeobukseonEnd = true;
                }
                break;

            case ContentType.Bugeo:
                if (bugeoClip != null)
                {
                    audioSource.clip = bugeoClip;
                    audioSource.Play();
                }
                ShowText(bugeoMessage);
                break;
        }

        Debug.Log($"[NarrationManager] 나레이션 재생: {type}");
    }

    private void PlaySecondClip()
    {
        if (geobukseonClip02 != null)
        {
            audioSource.clip = geobukseonClip02;
            audioSource.Play();
            Debug.Log("[NarrationManager] 두번째 MP3 재생");
        }
        else
        {
            Debug.LogWarning("[NarrationManager] 두번째 MP3가 없습니다.");
        }
    }

    private void ShowText(string message)
    {
        if (narrationText == null) return;
        narrationText.text = message;
        narrationText.gameObject.SetActive(true);
        Debug.Log($"[NarrationManager] 텍스트 표시: {message}");
    }

    public void Reset()
    {
        _hasPlayed = false;
        _waitingForGeobukseonEnd = false;
        _waitingForSecondClip = false;
        _secondClipTimer = 0f;
        audioSource?.Stop();

        if (narrationText != null)
            narrationText.gameObject.SetActive(false);
    }
}