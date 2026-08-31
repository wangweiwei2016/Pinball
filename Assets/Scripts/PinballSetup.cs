using UnityEngine;
using UnityEngine.UI;
using DefaultNamespace;

/// <summary>
/// 垂直弹珠台：一键搭建场景。
/// 布局：
///   - 右侧：发射台（垂直通道，球从底部向上发射，顶部有导流板将球导入主区域）
///   - 中部：环形撞击器 + 特殊撞击器
///   - 左侧底部：多个奖品槽（凹槽）
///   - 支持可控假物理：可指定球落入哪个奖品槽
///
/// 用法：新建空物体挂本脚本，运行即自动生成。
/// </summary>
public class PinballSetup : MonoBehaviour
{
    [Header("桌面尺寸")] public float tableWidth = 8f;
    public float tableHeight = 14f;
    public float wallThickness = 0.4f;

    [Header("物理")] public float gravityScale = 1.6f;

    [Header("发射通道")] public float channelWidth = 1.0f;
    public float channelTopY = 4.5f;

    [Header("奖品槽")] public int slotCount = 5;
    public float slotHeight = 1.5f;

    [Header("撞击器")] public float bumperRingRadius = 3.0f;
    public int bumperRingCount = 10;

    private void Start()
    {
        Build();
        Destroy(gameObject);
    }

    private void Build()
    {
        var root = new GameObject("PinballTable");
        var rootTransform = root.transform;

        float halfW = tableWidth / 2f;
        float halfH = tableHeight / 2f;
        float outerW = tableWidth + wallThickness * 2f;

        // ---------- 外围封闭墙体 ----------
        Color wallColor = new Color(0.35f, 0.32f, 0.45f);
        CreateWallColored("WallLeft", new Vector2(-halfW, 0f), new Vector2(wallThickness, tableHeight), rootTransform, wallColor);
        CreateWallColored("WallRight", new Vector2(halfW, 0f), new Vector2(wallThickness, tableHeight), rootTransform, wallColor);
        CreateWallColored("WallTop", new Vector2(0f, halfH), new Vector2(outerW, wallThickness), rootTransform, wallColor);
        CreateWallColored("WallBottom", new Vector2(0f, -halfH), new Vector2(outerW, wallThickness), rootTransform, wallColor);

        // ---------- 右侧发射通道 ----------
        float channelLeftX = halfW - channelWidth;
        float channelWallH = halfH + channelTopY;
        CreateWallColored("ChannelLeftWall",
            new Vector2(channelLeftX, -halfH + channelWallH * 0.5f),
            new Vector2(wallThickness, channelWallH), rootTransform, new Color(0.45f, 0.35f, 0.2f));

        float slotBottomY = -halfH + wallThickness;

        // 主游戏区背景
        float playAreaLeft = -halfW + wallThickness * 0.5f;
        float playAreaRight = channelLeftX - wallThickness * 0.5f;
        float playAreaTop = halfH - wallThickness * 0.5f;
        float playAreaBottom = slotBottomY + slotHeight + 0.5f;
        var playBg = new GameObject("PlayAreaBg");
        playBg.transform.SetParent(rootTransform, false);
        playBg.transform.position = new Vector2((playAreaLeft + playAreaRight) * 0.5f, (playAreaTop + playAreaBottom) * 0.5f);
        playBg.transform.localScale = new Vector3(playAreaRight - playAreaLeft, playAreaTop - playAreaBottom, 1f);
        var playBgSr = playBg.AddComponent<SpriteRenderer>();
        playBgSr.sprite = MakeRectSprite(new Color(0.12f, 0.1f, 0.22f));
        playBgSr.sortingOrder = -1;

        // 通道顶部导流板（将向上的球转为向左进入主区域）
        float deflectorRightX = channelLeftX + channelWidth - 0.1f;
        float deflectorRightY = channelTopY + 0.8f;
        float deflectorLeftX = channelLeftX - 2.0f;
        float deflectorLeftY = channelTopY + 3f;
        float deflectorCenterX = (deflectorRightX + deflectorLeftX) * 0.5f;
        float deflectorCenterY = (deflectorRightY + deflectorLeftY) * 0.5f;
        float deflectorLen = Mathf.Sqrt((deflectorRightX - deflectorLeftX) * (deflectorRightX - deflectorLeftX)
                                        + (deflectorRightY - deflectorLeftY) * (deflectorRightY - deflectorLeftY));
        float deflectorAngle = Mathf.Atan2(deflectorLeftY - deflectorRightY, deflectorLeftX - deflectorRightX) * Mathf.Rad2Deg;
        var deflector = CreateWallColored("ChannelDeflector",
            new Vector2(deflectorCenterX, deflectorCenterY),
            new Vector2(deflectorLen, 0.4f), rootTransform, new Color(0.9f, 0.75f, 0.25f));
        deflector.transform.rotation = Quaternion.Euler(0f, 0f, deflectorAngle);

        // ---------- 左侧奖品槽（凹槽） ----------
        float slotAreaLeft = -halfW + wallThickness * 0.5f;
        float slotAreaRight = channelLeftX - wallThickness * 0.5f;
        float slotAreaWidth = slotAreaRight - slotAreaLeft;
        float slotWidth = slotAreaWidth / slotCount;

        float slotAreaCenterY = slotBottomY + slotHeight * 0.5f;

        // 槽区域背景板
        var slotBg = new GameObject("SlotAreaBg");
        slotBg.transform.SetParent(rootTransform, false);
        slotBg.transform.position = new Vector2((slotAreaLeft + slotAreaRight) * 0.5f, slotAreaCenterY);
        slotBg.transform.localScale = new Vector3(slotAreaWidth, slotHeight + 0.4f, 1f);
        var slotBgSr = slotBg.AddComponent<SpriteRenderer>();
        slotBgSr.sprite = MakeRectSprite(new Color(0.25f, 0.18f, 0.12f));
        slotBgSr.sortingOrder = -1;

        Color slotWallColor = new Color(0.55f, 0.4f, 0.25f);
        Color slotBaseColor = new Color(0.35f, 0.25f, 0.15f);
        Color barrierColor = new Color(0.75f, 0.6f, 0.3f);
        int[] slotScores = { 20, 40, 100, 40, 20 };

        for (int i = 0; i < slotCount; i++)
        {
            float slotCenterX = slotAreaLeft + slotWidth * 0.5f + i * slotWidth;

            CreateWallColored("SlotBase" + i,
                new Vector2(slotCenterX, slotBottomY + 0.05f),
                new Vector2(slotWidth - 0.05f, 0.1f), rootTransform, slotBaseColor);

            float wallH = slotHeight * 0.7f;
            CreateWallColored("SlotWallL" + i,
                new Vector2(slotCenterX - slotWidth * 0.35f, slotBottomY + wallH * 0.5f),
                new Vector2(wallThickness * 0.5f, wallH), rootTransform, slotWallColor);
            CreateWallColored("SlotWallR" + i,
                new Vector2(slotCenterX + slotWidth * 0.35f, slotBottomY + wallH * 0.5f),
                new Vector2(wallThickness * 0.5f, wallH), rootTransform, slotWallColor);

            var slotTrigger = new GameObject("SlotTrigger" + i);
            slotTrigger.transform.SetParent(rootTransform, false);
            slotTrigger.transform.position = new Vector2(slotCenterX, slotBottomY + slotHeight * 0.35f);
            var sCol = slotTrigger.AddComponent<BoxCollider2D>();
            sCol.isTrigger = true;
            sCol.size = new Vector2(slotWidth * 0.7f, slotHeight * 0.7f);
            sCol.tag = Const.TAG_SLOT;

            CreateSlotLabel("SlotLabel" + i, slotCenterX, slotBottomY + slotHeight * 0.95f, rootTransform, slotScores[i]);
        }

        //不需要挡板
        // for (int i = 0; i < slotCount; i++)
        // {
        //     float slotCenterX = slotAreaLeft + slotWidth * 0.5f + i * slotWidth;
        //     CreateWallColored("SlotTopBarrier" + i,
        //         new Vector2(slotCenterX, slotBottomY + slotHeight + 0.15f),
        //         new Vector2(slotWidth * 0.7f, 0.15f), rootTransform, barrierColor);
        // }

        // ---------- 发球器（位于通道底部） ----------
        float launcherX = halfW - channelWidth * 0.5f;
        float launcherY = -halfH + wallThickness + 0.6f;
        var launcher = new GameObject("Launcher");
        launcher.transform.SetParent(rootTransform, false);
        launcher.transform.position = new Vector2(launcherX, launcherY);
        launcher.transform.localScale = new Vector2(channelWidth * 0.7f, 0.8f);
        var launcherCol = launcher.AddComponent<BoxCollider2D>();
        launcherCol.size = Vector2.one;
        launcherCol.isTrigger = true;
        var launcherSprite = launcher.AddComponent<SpriteRenderer>();
        launcherSprite.sprite = MakeRectSprite(new Color(0.8f, 0.5f, 0.95f));
        launcherSprite.sortingOrder = 2;
        var launcherComp = launcher.AddComponent<Launcher>();
        //launcherComp.launchSpeed = 28f;
        launcherComp.targetSlotIndex = -1;
        launcherComp.spawnPosition = new Vector2(launcherX, launcherY + 0.2f);
        launcherComp.gravityScale = gravityScale;

        // 让 Ball 层自碰撞禁用（球与球之间不碰撞）
        int ballLayer = LayerMask.NameToLayer("Ball");
        if (ballLayer >= 0)
        {
            Physics2D.IgnoreLayerCollision(ballLayer, ballLayer, true);
        }

        // ---------- 撞击器（避开右侧通道） ----------
        // 图片风格：自然分散布局，非正五边形环形
        // 主游戏区 x 范围：(-halfW + wallThickness) ~ (channelLeftX - wallThickness)
        float bumperAreaRight = channelLeftX - wallThickness * 0.5f;
        float bumperAreaLeft = -halfW + wallThickness * 0.5f;
        float bumperAreaTop = channelTopY + 0.5f;
        float bumperAreaBottom = slotBottomY + slotHeight + 0.5f;

        // 分散撞击器位置（按图片中小圆点分布手工排布）
        Vector2[] bumperPositions = new Vector2[]
        {
            // 顶部弧形
            new Vector2(-0.5f, 4.0f),

            new Vector2(-1.5f, 3.1f),
            new Vector2(0.5f, 3.1f),

            new Vector2(-2.5f, 2.3f),
            new Vector2(-0.5f, 2.3f),
            new Vector2(1.5f, 2.3f),

            new Vector2(-1.3f, 1.8f),
            new Vector2(0.3f, 1.8f),

            new Vector2(-0.5f, 1.1f),
            // 中部
            new Vector2(-1f, 0f),
            new Vector2(0f, 0f),

            new Vector2(-2.8f, -0.6f),
            new Vector2(1.8f, -0.6f),
            // 下
            new Vector2(-1.5f, -1.8f),
            new Vector2(-0.5f, -1.8f),
            new Vector2(0.5f, -1.8f),
            // 底部
            new Vector2(-2.2f, -2.8f),
            new Vector2(1.2f, -2.8f),

            new Vector2(-2.7f, -3.6f),
            new Vector2(1.8f, -3.6f),

            new Vector2(-2.0f, -4.2f),
            new Vector2(1.2f, -4.2f),
        };

        for (int i = 0; i < bumperPositions.Length; i++)
        {
            Vector2 pos = bumperPositions[i];
            // 钳制到主游戏区内，避开通道
            //pos.x = Mathf.Clamp(pos.x, bumperAreaLeft + 0.4f, bumperAreaRight - 0.4f);
            //pos.y = Mathf.Clamp(pos.y, bumperAreaBottom + 0.4f, bumperAreaTop - 0.4f);
            CreateBumper("Bumper" + i, pos, 0.2f, rootTransform, true);
        }

        // 中央大撞击器
        //CreateBumper("BumperCenter", new Vector2(-0.5f, 3.2f), 0.7f, rootTransform, true);

        // ---------- 中部偏下：特殊奖励槽 + 两侧弧形撞击器 ----------
        float specialSlotX = -0.5f;
        float specialSlotY = -3.6f;
        float specialSlotW = 0.8f;
        float specialSlotH = 0.5f;
        Color specialSlotColor = new Color(0.2f, 0.15f, 0.05f);
        Color specialSlotRimColor = new Color(1f, 0.82f, 0.2f);

        // 特殊槽底板
        CreateWallColored("SpecialSlotBase",
            new Vector2(specialSlotX, specialSlotY - specialSlotH * 0.5f),
            new Vector2(specialSlotW, 0.12f), rootTransform, specialSlotRimColor);

        // 特殊槽侧壁
        CreateWallColored("SpecialSlotWallL",
            new Vector2(specialSlotX - specialSlotW * 0.5f, specialSlotY),
            new Vector2(0.12f, specialSlotH), rootTransform, specialSlotRimColor);
        CreateWallColored("SpecialSlotWallR",
            new Vector2(specialSlotX + specialSlotW * 0.5f, specialSlotY),
            new Vector2(0.12f, specialSlotH), rootTransform, specialSlotRimColor);

        // 特殊槽背景
        var specialSlotBg = new GameObject("SpecialSlotBg");
        specialSlotBg.transform.SetParent(rootTransform, false);
        specialSlotBg.transform.position = new Vector2(specialSlotX, specialSlotY);
        specialSlotBg.transform.localScale = new Vector3(specialSlotW - 0.24f, specialSlotH, 1f);
        var specialSlotBgSr = specialSlotBg.AddComponent<SpriteRenderer>();
        specialSlotBgSr.sprite = MakeRectSprite(specialSlotColor);
        specialSlotBgSr.sortingOrder = 0;

        // 特殊槽触发器（标签 Slot，索引 5，超出常规槽范围 → GameManager 给予特殊奖励分）
        var specialTrigger = new GameObject("SlotTrigger5");
        specialTrigger.transform.SetParent(rootTransform, false);
        specialTrigger.transform.position = new Vector2(specialSlotX, specialSlotY);
        var specialCol = specialTrigger.AddComponent<BoxCollider2D>();
        specialCol.isTrigger = true;
        specialCol.size = new Vector2(specialSlotW * 0.8f, specialSlotH * 0.8f);
        specialCol.tag = Const.TAG_SLOT;

        CreateSlotLabel("SpecialSlotLabel", specialSlotX, specialSlotY + specialSlotH * 0.55f, rootTransform, 500);

        // 两侧弧形撞击器（围绕特殊槽，形成漏斗形引导）
        float arcCenterY = specialSlotY;
        float arcRadius = 1.3f;
        int arcBumperCount = 5;
        float arcBumperRadius = 0.2f;

        // // 左弧（圆心在特殊槽左侧，弧面朝右，形成 "(" 形状）
        // float leftArcCenterX = -1.8f;
        // for (int i = 0; i < arcBumperCount; i++)
        // {
        //     // 角度从 -50° 到 50°，弧面朝向特殊槽（右侧）
        //     float angle = Mathf.Lerp(-50f, 50f, (float)i / (arcBumperCount - 1));
        //     float rad = angle * Mathf.Deg2Rad;
        //     float bx = leftArcCenterX + Mathf.Cos(rad) * arcRadius;
        //     float by = arcCenterY + Mathf.Sin(rad) * arcRadius;
        //     CreateBumper("LeftArcBumper" + i, new Vector2(bx, by), arcBumperRadius, rootTransform, true);
        // }
        //
        // // 右弧（圆心在特殊槽右侧，弧面朝左，形成 ")" 形状）
        // float rightArcCenterX = 1.8f;
        // for (int i = 0; i < arcBumperCount; i++)
        // {
        //     // 角度从 130° 到 230°，弧面朝向特殊槽（左侧）
        //     float angle = Mathf.Lerp(130f, 230f, (float)i / (arcBumperCount - 1));
        //     float rad = angle * Mathf.Deg2Rad;
        //     float bx = rightArcCenterX + Mathf.Cos(rad) * arcRadius;
        //     float by = arcCenterY + Mathf.Sin(rad) * arcRadius;
        //     CreateBumper("RightArcBumper" + i, new Vector2(bx, by), arcBumperRadius, rootTransform, true);
        // }

        // 特殊撞击器
        CreateSpecialBumper("SpecialStar", new Vector2(-2.5f, 1.2f), 0.5f, rootTransform, new Color(1f, 0.85f, 0.2f));
        CreateSpecialBumper("SpecialShield", new Vector2(1.5f, 1.2f), 0.5f, rootTransform, new Color(0.4f, 0.7f, 1f));

        // ---------- 球生成参数（注入 Launcher，按空格动态创建球） ----------
        // 包含底部奖品槽（0~slotCount-1）+ 中部特殊奖励槽（索引 slotCount）
        float[] slotCenterXs = new float[slotCount + 1];
        float[] slotCenterYs = new float[slotCount + 1];
        for (int i = 0; i < slotCount; i++)
        {
            slotCenterXs[i] = slotAreaLeft + slotWidth * 0.5f + i * slotWidth;
            slotCenterYs[i] = slotAreaCenterY;
        }
        slotCenterXs[slotCount] = specialSlotX; // 特殊奖励槽 X 坐标
        slotCenterYs[slotCount] = specialSlotY; // 特殊奖励槽 Y 坐标

        launcherComp.slotCenterXs = slotCenterXs;
        launcherComp.slotCenterYs = slotCenterYs;
        // 引导区域：分阶段覆盖整个下落过程
        launcherComp.freePlayTopY = specialSlotY + 3f;     // 上半部分自由弹跳
        launcherComp.midGuideTopY = specialSlotY + 0.8f;    // 中段轻微引导
        launcherComp.strongGuideY = slotBottomY + 1.5f;     // 接近槽位强引导

        // 发射区边界：球在此区域内时速度不受约束、不被引导
        launcherComp.launchZoneLeftX = channelLeftX;
        launcherComp.launchZoneTopY = channelTopY + 0.8f;  // 导流板高度

        // ---------- 摄像机 ----------
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("MainCamera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }

        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.orthographic = true;
        cam.orthographicSize = tableHeight / 2f + 1f;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);

        // ---------- GameManager ----------
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        gm.launcher = launcherComp;

        // ---------- 轨迹库（单例，独立 GameObject + DontDestroyOnLoad，
        //           运行时从 Resources/Trajectories 加载预录轨迹） ----------
        var libGo = new GameObject("TrajectoryLibrary");
        libGo.AddComponent<TrajectoryLibrary>();

        // ---------- UI ----------
        BuildUI(gm);
    }

    private GameObject CreateWall(string name, Vector2 pos, Vector2 size, Transform parent)
    {
        return CreateWallColored(name, pos, size, parent, new Color(0.2f, 0.2f, 0.25f));
    }

    private GameObject CreateWallColored(string name, Vector2 pos, Vector2 size, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = size;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeRectSprite(color);
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        col.sharedMaterial = CreateBouncyMaterial();
        return go;
    }

    private void CreateBumper(string name, Vector2 pos, float radius, Transform parent, bool controllable)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(new Color(0.85f, 0.3f, 0.3f));
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;
        col.sharedMaterial = CreateBouncyMaterial();
        var bumper = go.AddComponent<Bumper>();
        bumper.baseScore = 0;
        bumper.isControllable = controllable;
    }

    private void CreateSpecialBumper(string name, Vector2 pos, float radius, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(color);
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;
        col.sharedMaterial = CreateBouncyMaterial();
        col.tag = Const.TAG_SPBUMPER;
        var bumper = go.AddComponent<Bumper>();
        bumper.baseScore = 50;
        bumper.isControllable = true;
        bumper.bounceMultiplier = 2.5f;
    }

    private GameObject CreateSlotLabel(string name, float x, float y, Transform parent, int score)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0f);
        go.transform.localScale = new Vector3(0.8f, 0.35f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeRectSprite(new Color(0.85f, 0.65f, 0.2f));

        var labelBg = new GameObject(name + "_Bg");
        labelBg.transform.SetParent(parent, false);
        labelBg.transform.position = new Vector3(x, y, 0f);
        labelBg.transform.localScale = new Vector3(0.9f, 0.4f, 1f);
        var bgSr = labelBg.AddComponent<SpriteRenderer>();
        bgSr.sprite = MakeRectSprite(new Color(0.6f, 0.4f, 0.1f));
        bgSr.sortingOrder = 1;

        var textCanvasGo = new GameObject(name + "_Canvas");
        textCanvasGo.transform.SetParent(parent, false);
        textCanvasGo.transform.position = new Vector3(x, y, 0f);
        textCanvasGo.transform.localScale = new Vector3(0.01f, 0.01f, 1f);
        var canvas = textCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 2;
        var canvasRt = canvas.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(120, 60);
        textCanvasGo.AddComponent<GraphicRaycaster>();

        var textGo = new GameObject("ScoreText");
        textGo.transform.SetParent(textCanvasGo.transform, false);
        var labelText = textGo.AddComponent<Text>();
        labelText.text = score.ToString();
        labelText.fontSize = 48;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.fontStyle = FontStyle.Bold;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); //设置字体
        var textRt = labelText.rectTransform;
        textRt.anchorMin = textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.sizeDelta = new Vector2(120, 60);

        return go;
    }

    private Text CreateWorldText(string name, Transform parent, Vector2 worldPos, string content, int fontSize, TextAnchor anchor)
    {
        var textGo = new GameObject(name);
        textGo.transform.SetParent(parent, false);
        textGo.transform.position = worldPos;
        var text = textGo.AddComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = text.rectTransform;
        rt.sizeDelta = new Vector2(200, 100);
        rt.anchoredPosition = Vector2.zero;
        return text;
    }

    private Canvas CreateWorldSpaceCanvas(string name, Vector3 position, Vector2 size)
    {
        var canvasGo = new GameObject(name);
        canvasGo.transform.position = position;
        canvasGo.transform.localScale = new Vector3(0.01f, 0.01f, 1f);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private PhysicsMaterial2D CreateBouncyMaterial()
    {
        var mat = new PhysicsMaterial2D("BouncyMat");
        mat.bounciness = 0.85f;
        mat.friction = 0.02f;
        return mat;
    }

    private Sprite MakeRectSprite(Color color)
    {
        int size = 100;
        var tex = new Texture2D(size, size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, color);
            }
        }

        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return sprite;
    }

    private Sprite MakeCircleSprite(Color color)
    {
        int size = 100;
        var tex = new Texture2D(size, size);
        var center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - dist);
                Color c = color;
                c.a = alpha > 0f ? 1f : 0f;
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void BuildUI(GameManager gm)
    {
        var canvasGo = new GameObject("UICanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        var scoreText = CreateText("ScoreText", canvasGo.transform, new Vector2(20, -20), TextAnchor.UpperLeft, 28);
        var ballsText = CreateText("BallsText", canvasGo.transform, new Vector2(-20, -20), TextAnchor.UpperRight, 28);
        var statusText = CreateText("StatusText", canvasGo.transform, new Vector2(0, -60), TextAnchor.UpperCenter, 22);

        gm.scoreText = scoreText;
        gm.ballsText = ballsText;
        gm.statusText = statusText;
    }

    private Text CreateText(string name, Transform parent, Vector2 anchoredPos, TextAnchor anchor, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.alignment = anchor;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
        var rt = text.rectTransform;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(400, 50);
        rt.anchorMin = rt.anchorMax = GetAnchor(anchor);
        return text;
    }

    private Vector2 GetAnchor(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return new Vector2(0, 1);
            case TextAnchor.UpperRight: return new Vector2(1, 1);
            case TextAnchor.UpperCenter: return new Vector2(0.5f, 1);
            default: return new Vector2(0, 1);
        }
    }
}