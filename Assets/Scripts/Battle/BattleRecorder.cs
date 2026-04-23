using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BattleRecorder : MonoBehaviour
{
    [System.Serializable]
    public class ObjectSnapshot
    {
        public int instanceId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public bool active;
        public Color color;
        public Sprite sprite;
    }

    [System.Serializable]
    public class Frame
    {
        public List<ObjectSnapshot> snapshots = new List<ObjectSnapshot>();
        public string logText; // Record the full log text
    }

    // Objects to track
    public List<Transform> targets = new List<Transform>();
    public TMPro.TextMeshProUGUI logTextUI; // Reference to the log UI
    
    // Recording data
    private List<Frame> recording = new List<Frame>();
    private bool isReplaying = false;
    private int replayFrame = 0;
    private int maxFrames = 3600; // ~60 seconds at 60fps

    // Cache for performance
    private Dictionary<int, Transform> transformCache = new Dictionary<int, Transform>();

    void Update()
    {
        HandleInput();

        if (isReplaying)
        {
            // Replay Mode: Do nothing (game is paused), input handles stepping
        }
        else
        {
            // Live Mode: Record
            if (Time.timeScale > 0)
            {
                RecordFrame();
            }
        }
    }

    private void HandleInput()
    {
        // Ctrl+F8: Pause / Resume (F8 단독은 BattleManager 이동 선택에 사용)
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F8))
        {
            TogglePause();
        }

        // F6: Rewind (only when paused)
        if (isReplaying && (Input.GetKey(KeyCode.F6) || Input.GetKeyDown(KeyCode.F6)))
        {
            Step(-1);
        }

        // F7: Forward (only when paused)
        if (isReplaying && (Input.GetKey(KeyCode.F7) || Input.GetKeyDown(KeyCode.F7)))
        {
            Step(1);
        }
    }

    public void TogglePause()
    {
        if (!isReplaying)
        {
            // Enter Replay Mode
            // Capture the current state before pausing so we don't lose the latest changes
            RecordFrame();
            
            isReplaying = true;
            Time.timeScale = 0;
            replayFrame = recording.Count - 1; // Start at the latest frame
            Debug.Log($"[BattleRecorder] Paused. Frames recorded: {recording.Count}");
        }
        else
        {
            // Exit Replay Mode
            isReplaying = false;
            Time.timeScale = 1;
            // Restore the very last frame to ensure we are in sync with "Live" state
            if (recording.Count > 0)
            {
                RestoreFrame(recording.Count - 1);
            }
            Debug.Log("[BattleRecorder] Resumed.");
        }
    }

    private void Step(int direction)
    {
        if (recording.Count == 0) return;

        // Speed multiplier for holding down key?
        // For now, 1 frame per Update is fast enough (60fps rewind)
        
        replayFrame += direction;
        replayFrame = Mathf.Clamp(replayFrame, 0, recording.Count - 1);
        
        RestoreFrame(replayFrame);
    }

    public void RegisterTarget(Transform t)
    {
        if (t == null) return;
        if (!targets.Contains(t))
        {
            targets.Add(t);
            transformCache[t.GetInstanceID()] = t;
        }
    }

    public void RegisterLogUI(TMPro.TextMeshProUGUI ui)
    {
        logTextUI = ui;
    }

    public void ClearTargets()
    {
        targets.Clear();
        transformCache.Clear();
    }

    private void RecordFrame()
    {
        Frame frame = new Frame();
        
        // Record Log Text
        if (logTextUI != null)
        {
            frame.logText = logTextUI.text;
        }
        
        // Clean up null targets
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            if (targets[i] == null)
            {
                targets.RemoveAt(i);
            }
        }

        foreach (var t in targets)
        {
            ObjectSnapshot snap = new ObjectSnapshot();
            snap.instanceId = t.GetInstanceID();
            snap.position = t.position;
            snap.rotation = t.rotation;
            snap.scale = t.localScale;
            snap.active = t.gameObject.activeSelf;
            
            // SpriteRenderer (2D World)
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                snap.color = sr.color;
                snap.sprite = sr.sprite;
            }
            
            // Image (UI)
            var img = t.GetComponent<Image>();
            if (img != null)
            {
                snap.color = img.color;
                snap.sprite = img.sprite;
            }

            frame.snapshots.Add(snap);
        }
        
        recording.Add(frame);
        
        // Circular buffer limit
        if (recording.Count > maxFrames)
        {
            recording.RemoveAt(0);
        }
    }

    private void RestoreFrame(int index)
    {
        if (index < 0 || index >= recording.Count) return;
        
        Frame frame = recording[index];
        
        // Restore Log Text
        if (logTextUI != null)
        {
            logTextUI.text = frame.logText;
            
            // Optional: Scroll to bottom if needed, but text content restoration is usually enough
            // var scroll = logTextUI.GetComponentInParent<ScrollRect>();
            // if (scroll != null) scroll.verticalNormalizedPosition = 0f;
        }
        
        foreach (var snap in frame.snapshots)
        {
            if (transformCache.TryGetValue(snap.instanceId, out Transform t))
            {
                if (t == null) continue;

                t.position = snap.position;
                t.rotation = snap.rotation;
                t.localScale = snap.scale;
                
                // Only change active state if it differs (optimization)
                if (t.gameObject.activeSelf != snap.active)
                {
                    t.gameObject.SetActive(snap.active);
                }
                
                var sr = t.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = snap.color;
                    sr.sprite = snap.sprite;
                }
                
                var img = t.GetComponent<Image>();
                if (img != null)
                {
                    img.color = snap.color;
                    img.sprite = snap.sprite;
                }
            }
        }
    }
}
