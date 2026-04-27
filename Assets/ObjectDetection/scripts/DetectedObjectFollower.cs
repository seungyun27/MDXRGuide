using System.Collections.Generic;
using UnityEngine;
using Meta.XR.BuildingBlocks.AIBlocks;
using TMPro;

public class DetectedObjectFollower : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string targetLabel = "special_object";
    [SerializeField] private int minStableFrames = 1;

    [Header("Spawn")]
    [SerializeField] private GameObject spawnedPrefab;
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private bool spawnOnlyOnce = false;

    [Header("Debug")]
    [SerializeField] private TMP_Text debugText;

    private ObjectDetectionAgent _agent;
    private ObjectDetectionVisualizer _visualizer;

    private GameObject _spawnedObject;
    private int _stableCount = 0;
    private bool _isLocked = false;

    private void Awake()
    {
        _agent = GetComponent<ObjectDetectionAgent>();
        _visualizer = GetComponent<ObjectDetectionVisualizer>();
    }

    private void OnEnable()
    {
        if (_agent != null)
        {
            _agent.OnBoxesUpdated += HandleBoxesUpdated;
            SetDebug("Agent connected");
        }
        else
        {
            SetDebug("ERROR: Agent missing");
        }
    }

    private void OnDisable()
    {
        if (_agent != null)
        {
            _agent.OnBoxesUpdated -= HandleBoxesUpdated;
        }
    }

    private void HandleBoxesUpdated(List<BoxData> batch)
    {
        if (_isLocked && spawnOnlyOnce)
        {
            SetDebug("Locked (spawn once)");
            return;
        }

        if (_visualizer == null)
        {
            SetDebug("ERROR: Visualizer missing");
            return;
        }

        if (batch == null || batch.Count == 0)
        {
            SetDebug("No detections");
            _stableCount = 0;
            return;
        }

        bool found = false;
        string labels = "";

        foreach (var b in batch)
        {
            string detectedLabelOnly = ExtractLabelName(b.label);
            labels += $"[{b.label} -> {detectedLabelOnly}] ";

            if (!string.Equals(detectedLabelOnly, targetLabel, System.StringComparison.OrdinalIgnoreCase))
                continue;

            found = true;

            float xmin = b.position.x;
            float ymin = b.position.y;
            float xmax = b.scale.x;
            float ymax = b.scale.y;

            if (_visualizer.TryProject(xmin, ymin, xmax, ymax, out var pos, out var rot, out var scl))
            {
                _stableCount++;

                SetDebug(
                    "Target FOUND\n" +
                    $"Detected: {labels}\n" +
                    $"Wanted: {targetLabel}\n" +
                    $"Stable: {_stableCount}\n" +
                    $"Pos: {pos}"
                );

                if (_stableCount >= minStableFrames)
                {
                    SpawnOrMove(pos, rot);

                    if (spawnOnlyOnce)
                        _isLocked = true;
                }
            }
            else
            {
                SetDebug(
                    "Projection FAILED\n" +
                    $"Detected: {labels}\n" +
                    $"Wanted: {targetLabel}"
                );
            }

            break;
        }

        if (!found)
        {
            _stableCount = 0;
            SetDebug(
                "Target NOT found\n" +
                $"Detected: {labels}\n" +
                $"Wanted: {targetLabel}"
            );
        }
    }

    private string ExtractLabelName(string rawLabel)
    {
        if (string.IsNullOrEmpty(rawLabel))
            return "";

        string[] parts = rawLabel.Split(' ');

        if (parts.Length > 0)
            return parts[0].Trim();

        return rawLabel.Trim();
    }

    private void SpawnOrMove(Vector3 pos, Quaternion rot)
    {
        if (spawnedPrefab == null)
        {
            SetDebug("ERROR: prefab is NULL");
            return;
        }

        Vector3 finalPos = pos + rot * localOffset;

        if (_spawnedObject == null)
        {
            _spawnedObject = Instantiate(spawnedPrefab, finalPos, rot);
            _spawnedObject.transform.localScale = Vector3.one * 0.2f;

            SetDebug("Spawned object\n" + finalPos);
        }
        else
        {
            _spawnedObject.transform.position = finalPos;
            _spawnedObject.transform.rotation = rot;

            SetDebug("Updated position\n" + finalPos);
        }
    }

    private void SetDebug(string msg)
    {
        if (debugText != null)
            debugText.text = msg;
    }
}