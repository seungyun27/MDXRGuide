using UnityEngine;

public class DebugLogger : MonoBehaviour
{
    string _log = "";

    void OnEnable() => Application.logMessageReceived += OnLog;
    void OnDisable() => Application.logMessageReceived -= OnLog;

    void OnLog(string msg, string stack, LogType type)
    {
        // 최신 로그가 위에 오도록
        _log = msg + "\n" + _log;

        // 너무 길어지면 자르기
        if (_log.Length > 3000)
            _log = _log.Substring(0, 3000);
    }

    void OnGUI()
    {
        // 배경
        GUI.Box(new Rect(10, 10, 900, 500), "");
        // 스크롤 없이 텍스트 출력
        GUI.Label(new Rect(20, 20, 880, 480), _log);
    }
}