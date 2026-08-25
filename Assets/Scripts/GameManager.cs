using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DefaultNamespace;

/// <summary>
/// 弹珠台总控：分数、生命数、UI 显示、奖品槽判定、游戏结束。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("基础设置")]
    [Tooltip("初始生命（球）数。")]
    public int startingBalls = 300;

    [Tooltip("分数倍率。")]
    public int scoreMultiplier = 1;

    [Header("奖品槽分值")]
    [Tooltip("各槽对应的分数。")]
    public int[] slotScores = new int[] { 20, 40, 100, 40, 20 };

    [Header("UI 引用")]
    public Text scoreText;
    public Text ballsText;
    public Text statusText;
    public GameObject gameOverPanel;

    [Header("球")]
    public Ball ball;

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

        if (ball == null) ball = FindObjectOfType<Ball>();

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        UpdateUI();
        if (statusText != null) statusText.text = "按空格发射弹珠";
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
        if (!isGameOver) return;
        if (Input.GetKeyDown(KeyCode.R))
        {
            Restart();
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
    /// 球进入奖品槽。
    /// </summary>
    public void OnBallEnterSlot(int slotIndex)
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

        AddScore(slotScore);

        ballsLeft--;
        UpdateUI();

        if (statusText != null)
        {
            statusText.text = $"落入槽 {slotIndex + 1}，获得 {slotScore} 分";
        }

        if (ballsLeft <= 0)
        {
            GameOver();
        }
        else
        {
            RespawnBall();
        }
    }

    private void RespawnBall()
    {
        if (ball == null) return;
        ball.ResetToSpawn();
        if (statusText != null)
        {
            statusText.text = "按空格发射弹珠";
        }
    }

    private void GameOver()
    {
        isGameOver = true;
        if (ball != null)
        {
            ball.gameObject.SetActive(false);
        }
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
