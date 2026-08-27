using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DefaultNamespace;

/// <summary>
/// 弹珠台总控：分数、生命数、UI 显示、奖品槽判定、游戏结束、录制模式开关。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("基础设置")]
    [Tooltip("初始生命（球）数。")]
    public int startingBalls = 300;

    [Tooltip("分数倍率。")]
    public int scoreMultiplier = 1;

    [Tooltip("无限球数（调试用）。开启后球数不减少，方便反复测试。")]
    public bool infiniteBalls = false;

    [Header("奖品槽分值")]
    [Tooltip("各槽对应的分数。")]
    public int[] slotScores = new int[] { 20, 40, 100, 40, 20 };

    [Header("UI 引用")]
    public Text scoreText;
    public Text ballsText;
    public Text statusText;
    public GameObject gameOverPanel;

    [Header("球")]
    public Launcher launcher;

    [Header("录制模式")]
    [Tooltip("全局录制模式开关。开启后球会记录轨迹并保存为 .asset 文件。")]
    public bool enableRecording = false;

    [Tooltip("切换录制模式的按键（编辑器 Play 模式生效）。")]
    public KeyCode recordingToggleKey = KeyCode.F6;

    /// <summary>当前是否处于录制模式（只读，供 Ball/Launcher/TrajectoryRecorder 查询）。</summary>
    public bool EnableRecording => enableRecording;

    private int score;
    private int ballsLeft;
    private bool isGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        ResolveUIReferences();

        score = 0;
        ballsLeft = startingBalls;
        isGameOver = false;

        if (launcher == null) launcher = FindObjectOfType<Launcher>();

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        UpdateUI();
        if (statusText != null) statusText.text = "按空格发射弹珠";

        // 生成初始球（在发射通道待命）
        if (launcher != null)
        {
            launcher.SpawnInitialBall();
        }
    }

    private void ResolveUIReferences()
    {
        if (scoreText == null)
        {
            var go = GameObject.Find("ScoreText");
            if (go != null) scoreText = go.GetComponent<Text>();
        }
        if (ballsText == null)
        {
            var go = GameObject.Find("BallsText");
            if (go != null) ballsText = go.GetComponent<Text>();
        }
        if (statusText == null)
        {
            var go = GameObject.Find("StatusText");
            if (go != null) statusText = go.GetComponent<Text>();
        }
        if (gameOverPanel == null)
        {
            var go = GameObject.Find("GameOverPanel");
            if (go != null) gameOverPanel = go;
        }
    }

    private void Update()
    {
        // F6 切换录制模式
        if (Input.GetKeyDown(recordingToggleKey))
        {
            enableRecording = !enableRecording;
            Debug.Log($"[GameManager] 录制模式：{(enableRecording ? "开启" : "关闭")}");
        }

        if (isGameOver)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                Restart();
            }
        }
    }

    public void AddScore(int amount)
    {
        if (isGameOver) return;
        score += amount * scoreMultiplier;
        UpdateUI();
    }

    public void OnBallLaunched()
    {
        if (statusText != null) statusText.text = "";
    }

    /// <summary>
    /// 球进入奖品槽。由 Ball.OnTriggerEnter2D 传入自己。
    /// </summary>
    public void OnBallEnterSlot(int slotIndex, Ball ball)
    {
        if (isGameOver) return;

        // 停止回放/录制：回放到此即视为球已落入槽；录制则把本条轨迹以实际槽位保存
        if (ball != null)
        {
            var recorder = ball.GetComponent<TrajectoryRecorder>();
            if (recorder != null && recorder.IsRecording)
            {
                recorder.StopAndSave(slotIndex);
            }
            var tp = ball.GetComponent<TrajectoryPlayer>();
            if (tp != null && tp.IsPlaying)
            {
                tp.Stop();
            }
        }

        int slotScore = 50;
        if (slotIndex >= 0 && slotIndex < slotScores.Length)
        {
            slotScore = slotScores[slotIndex];
        }
        else if (slotIndex == 5)
        {
            // 中部偏下特殊奖励槽
            slotScore = 500;
        }

        AddScore(slotScore);

        if (!infiniteBalls)
        {
            ballsLeft--;
            UpdateUI();
        }

        if (statusText != null)
        {
            if (slotIndex == 5)
                statusText.text = $"命中特殊奖励槽！获得 {slotScore} 分";
            else
                statusText.text = $"落入槽 {slotIndex + 1}，获得 {slotScore} 分";
        }

        // 销毁入槽的球，并通知 Launcher 减1
        // if (ball != null)
        // {
        //     Destroy(ball.gameObject);
        // }
        if (launcher != null)
        {
            launcher.Recycle(ball);
            launcher.NotifyBallRemoved();
        }

        if (ballsLeft <= 0)
        {
            GameOver();
        }
    }

    private void GameOver()
    {
        isGameOver = true;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (statusText != null) statusText.text = "游戏结束  按 R 重开";
    }

    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = "分数 " + score;
        if (ballsText != null) ballsText.text = "球 " + ballsLeft;
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
