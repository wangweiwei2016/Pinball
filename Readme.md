# 弹珠台游戏（DanZhu）

基于 Unity 2D 的塔塔冒险队式可控结果弹珠台游戏。球看起来在自然弹跳，但实际落点确定可控——通过预录轨迹回放 + 实时路径引导两种策略实现"假物理"。

## 核心机制

1. **录制-回放范式**：每颗小球在发射前就确定了运动轨迹和目标槽位。先制作好弹珠台，运行多个小球记录下每个球的运动轨迹和目标槽位，服务端可提前准备好 15 颗小球的运动轨迹。
2. **假物理碰撞**：球看起来在挡板和机关之间反复碰撞，但落点确定可控。
3. **能量约束**：反弹力 ≤ 重力；反弹高度 ≤ 下落起点高度；发射区内速度不受约束。
4. **强引导封顶**：强引导阶段施加力 ≤ 最近一次反弹力，避免凭空加速。

## 按键说明

| 按键 | 功能 |
|---|---|
| `空格` | 发射弹珠（单次/连续） |
| `F6` | 切换录制模式（编辑器预录轨迹） |
| `F7` | 切换纯物理模式（完全走物理引擎，无路径引导） |

## 快速开始

1. 在 Unity 中打开项目（推荐 Unity 2021 LTS 或更高）。
2. 打开 `Pinball.unity` 场景（或新建空场景挂载 `PinballSetup.cs`）。
3. 运行场景——所有弹珠台元素（墙体、撞击器、奖品槽、发射器、GameManager、UI）由 `PinballSetup` 自动生成。
4. 按空格发射弹珠。

## 运行模式

`Launcher` 按以下优先级选择发射模式：

| 优先级 | 模式 | 触发条件 | 说明 |
|---|---|---|---|
| 1 | 编辑器预录 | `recorder.enableRecording`（F6 开启） | 录制真实物理路径为 `TrajectoryData` 资源 |
| 2 | 纯物理引擎 | `purePhysicsMode`（F7 开启） | 完全走物理引擎，不应用路径引导、不回放 |
| 3 | 强制路径控制 | `forcePathController = true` | 调试用，强制用 `BallPathController` 实时引导 |
| 4 | 轨迹回放 | 有匹配的预录轨迹 | 运行时回放预录轨迹，实现可控结果 |
| 5 | 实时引导 | 后备方案 | 无匹配轨迹时回退到实时路径引导 |

### 模式组合

| 录制 (F6) | 纯物理 (F7) | 行为 |
|---|---|---|
| ✓ | ✗ | 录制带引导的轨迹 |
| ✓ | ✓ | **录制纯物理轨迹**（关闭引导，仍记录轨迹数据） |
| ✗ | ✓ | 纯物理不录制 |
| ✗ | ✗ | 走模式 3/4/5（轨迹回放或实时引导） |

## 录制流程

1. 按 `F6` 开启录制模式。
2. 按空格发射弹珠——球会按真实物理运动，`TrajectoryRecorder` 逐帧记录位置和时间戳。
3. 球入槽时，`GameManager` 调用 `recorder.StopAndSave(slotId)` 保存为 `Assets/Resources/Trajectories/slot_X_<timestamp>.asset`。
4. 重复发射至每个槽位都有多条轨迹样本。
5. 按 `F6` 关闭录制模式，进入正常游戏。

> 提示：开启 `F7` 纯物理模式后录制，可得到未经引导干预的"自然轨迹"，适合做基准样本。

## 物理约束详解

为实现自然弹跳观感同时保证可控结果，引入三层能量约束：

### 约束 1：发射区豁免
球在右侧发射通道内（`x > launchZoneLeftX && y < launchZoneTopY`）时：
- 速度不受封顶约束
- 不被路径引导
- 不被路径控制器干预反弹

让发射冲力完整传递，不受约束破坏发球体验。

### 约束 2：反弹力 ≤ 重力
非发射区内的反弹速度大小封顶到重力加速度大小：
```
gravityMag = |Physics2D.gravity| × |rb.gravityScale|  // 默认 ≈ 15.7 m/s
if (speed > gravityMag) velocity = velocity.normalized × gravityMag
```
- 适用于墙体反弹和撞击器反弹
- 由 `Ball.CapVelocityToGravity()` 集中处理

### 约束 3：反弹高度 ≤ 下落起点高度
能量守恒式约束：反弹后能达到的最高点不得超过开始下落时的高度。
```
v²/(2g) ≤ yDescendStart - bouncePoint.y
→ v ≤ sqrt(2g × heightDiff)
```
- 在 `Ball.FixedUpdate` 检测 `velocity.y` 从 ≥0 变为 <0 的瞬间，记录 `yDescendStart`
- 在反弹时由 `Ball.CapBounceHeightByDescend()` 封顶速度
- 仅在球处于下落状态时生效

### 约束 4：强引导力 ≤ 反弹力
`BallPathController` 强引导阶段（stage 2）施加的目标水平速度上限：
```
maxSteerSpeed = min(strongSteerSpeed, lastBounceSpeed)
```
- `lastBounceSpeed` 缓存最近一次反弹速度大小（已经过约束 2 封顶）
- 避免引导阶段凭空加速，破坏自然弹跳观感

### 约束链总览
```
强引导力 ≤ 反弹力（封顶后）≤ 重力
            ↑
       反弹高度 ≤ 下落起点高度
```

## 脚本结构

### 核心脚本

| 脚本 | 职责 |
|---|---|
| [PinballSetup.cs](Assets/Scripts/PinballSetup.cs) | 场景自动生成：墙体、撞击器、奖品槽、发射器、UI |
| [GameManager.cs](Assets/Scripts/GameManager.cs) | 单例全局状态：录制开关、得分、球生命周期 |
| [Launcher.cs](Assets/Scripts/Launcher.cs) | 发球器：5 种发射模式、多球管理、按键切换 |
| [Ball.cs](Assets/Scripts/Ball.cs) | 弹珠：碰撞反弹、速度限制、能量约束（CapVelocityToGravity/CapBounceHeightByDescend） |
| [Bumper.cs](Assets/Scripts/Bumper.cs) | 撞击器：反弹、加分、视觉反馈 |
| [BallPathController.cs](Assets/Scripts/BallPathController.cs) | 路径引导：分阶段策略（自由/中段微调/强引导） |
| [Flipper.cs](Assets/Scripts/Flipper.cs) | 挡板（若启用） |

### 轨迹系统

| 脚本 | 职责 |
|---|---|
| [TrajectoryData.cs](Assets/Scripts/TrajectoryData.cs) | `ScriptableObject` 资源：极简帧结构（10 字节/帧） |
| [TrajectoryRecorder.cs](Assets/Scripts/TrajectoryRecorder.cs) | 编辑器录制：记录位置+时间戳，保存为 `.asset` |
| [TrajectoryPlayer.cs](Assets/Scripts/TrajectoryPlayer.cs) | 运行时回放：运动学插值 + 撞击器近距反馈 |
| [TrajectoryLibrary.cs](Assets/Scripts/TrajectoryLibrary.cs) | 轨迹库：按槽位索引、按起始位置/力度匹配 |

### MoveTable 子目录

| 脚本 | 职责 |
|---|---|
| [BarTrigger.cs](Assets/Scripts/MoveTable/BarTrigger.cs) | 杆触发器 |
| [BarTriggerManager.cs](Assets/Scripts/MoveTable/BarTriggerManager.cs) | 杆触发管理 |
| [BarrierComponent.cs](Assets/Scripts/MoveTable/BarrierComponent.cs) | 障碍组件 |
| [MoveRewardTrigger.cs](Assets/Scripts/MoveTable/MoveRewardTrigger.cs) | 移动奖励触发 |
| [TargetTrigger.cs](Assets/Scripts/MoveTable/TargetTrigger.cs) | 目标触发 |
| [TriggerCollisionForwarder.cs](Assets/Scripts/MoveTable/TriggerCollisionForwarder.cs) | 碰撞转发 |

### 工具类

| 脚本 | 职责 |
|---|---|
| [Const.cs](Assets/Scripts/Const.cs) | 常量：Tag、Layer 名 |
| [ObjectPool.cs](Assets/Scripts/ObjectPool.cs) | 通用对象池 |

## TrajectoryData 存储优化

为最小化存储体积，`TrajectoryFrame` 采用极简结构：

| 字段 | 类型 | 字节 | 说明 |
|---|---|---|---|
| position | `Vector2` | 8 | 2D 位置（移除 z 分量） |
| timestamp | `ushort` | 2 | 0.01s 精度，上限 ~655s |

**单帧 10 字节（原 44 字节，减 77%）**。1000 帧轨迹约 10 KB。

### `TrajectoryData` 资源字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `targetSlotId` | `int` | 目标槽位 ID |
| `startPosition` | `Vector2` | 起始位置 |
| `startSpeed` | `float` | 起始速度大小（用于匹配发射力度） |
| `frames` | `List<TrajectoryFrame>` | 轨迹帧数据 |
| `starHitCount` | `byte` | SpecialStar 撞击次数（0-255） |
| `shieldHitCount` | `byte` | SpecialShield 撞击次数（0-255） |
| `totalDuration` | `float`（派生） | 由末帧时间戳计算，不占存储 |

### 移除的冗余字段
- `id`（string）—— 资产文件名即唯一标识
- `startVelocity`（Vector3）—— 仅用 `.magnitude`，改为 `startSpeed`
- `TrajectoryFrame.velocity` —— 回放仅靠位置插值，速度不入档
- `TrajectoryFrame.rotation` —— 2D 圆形弹珠旋转不影响物理/视觉

## 场景布局

```
┌─────────────────────────┐
│         WallTop         │
│  ┌──────────┐   Bumper  │
│  │ Channel  │  ◯   ◯   │
│  │  ↕ 发射  │    ◯     │
│  │   通道   │  ◯   ◯   │
│  │    ↓     │  Special  │
│  └──┐  ┌────┘  ┌──┐    │
│     │  │导流板 │  │    │
│ ┌─┐ │  │ ←────┘  │    │
│ │0│ │  │  ←──────┘    │
│ │1│ │                │
│ │2│ │                │
│ │3│ │                │
│ │4│ │                │
│ └─┘ └────────────────┘
└─────────────────────────┘
- 右侧：垂直发射通道，球从底部向上发射，顶部导流板将球导入主区域
- 中部：环形撞击器 + 特殊撞击器（Star/Shield）
- 左侧底部：5 个奖品槽（凹形终点）+ 1 个中部特殊奖励槽
```

## 开发约定

### 全局状态集中管理
- 录制开关由 `GameManager.EnableRecording` 统一管理，不放在每颗球上
- 球间碰撞禁用（`Physics2D.IgnoreLayerCollision` for Ball layer）
- 最大 15 颗活跃球同时在场
- 初始状态包含 1 颗待命球在发射通道

### 工程约定
- 球组件（Rigidbody2D/Collider2D 等）由 `Launcher.CreateBallGameObject` 动态创建
- 轨迹数据文件名包含特殊撞击器命中标记（`_starN`、`_shieldN`）
- 轨迹回放按特殊撞击器命中要求过滤（`requireStar`、`requireShield`、`requireAnySpecial`）

### 教训
- `BallPathController` 中直接控制 velocity 会导致不自然轨迹，需用基于区域的角度偏转（≤25°）
- 2×2 像素纹理会导致挡板/凹槽视觉不可见，需用 100×100 + `pixelsPerUnit=100`

## 调试技巧

- **查看当前模式**：观察 Console 的 `[Launcher]` 日志，会打印模式编号与名称
- **强制使用路径控制**：在 Inspector 勾选 `Launcher.forcePathController`
- **检查轨迹匹配**：`TrajectoryLibrary` 提供 `PrintStats()` 调试方法
- **可视化球半径**：选中球 GameObject，Gizmos 会绘制青色边框圆

## 目录结构

```
DanZhu/
├── Assets/
│   ├── Resources/
│   │   └── Trajectories/         # 预录轨迹 .asset 文件
│   │       ├── slot_0_<timestamp>.asset
│   │       ├── slot_0_star2_<timestamp>.asset
│   │       └── ...
│   └── Scripts/
│       ├── MoveTable/             # 移动台组件
│       ├── Ball.cs
│       ├── BallPathController.cs
│       ├── Bumper.cs
│       ├── Const.cs
│       ├── Flipper.cs
│       ├── GameManager.cs
│       ├── Launcher.cs
│       ├── ObjectPool.cs
│       ├── PinballSetup.cs
│       ├── TrajectoryData.cs
│       ├── TrajectoryLibrary.cs
│       ├── TrajectoryPlayer.cs
│       └── TrajectoryRecorder.cs
└── Readme.md
```

BattleSetup.Build()
  ├─ GridSystem.Build()           → 5×8 格子
  ├─ BattleManager (Ready 状态)   → 放置初始角色到 PlayerRow
  └─ MonsterSpawner               → 等待开始

按 B 键
  └─ BattleManager.StartBattle()
       ├─ 初始角色已放置
       └─ MonsterSpawner.StartNextWave()
            ├─ 每 1.5s 从顶部随机列生成怪物
            ├─ 怪物沿列向下移动，遇角色阻挡停下攻击
            ├─ 到达底部 → OnMonsterReachBottom → Defeat
            └─ Wave 清空 → 等场上所有怪物死 → 下一波
                 └─ 所有 Wave 打完 → OnAllWavesCleared → Victory

使用方法
新建空 GameObject，挂 BattleSetup.cs
运行场景（自动构建战斗场景、生成默认角色和怪物）
按 B 键开始战斗
胜利/失败后按 B 重置

Battle/
├── IDamageable.cs          # 伤害接口 + Team 枚举
├── GridCell.cs             # 单个格子
├── GridSystem.cs           # 格子系统（查询/攻击范围判定）
├── EntityBase.cs           # 实体基类（HP/攻击/移动/死亡）
├── CharacterData.cs        # 角色配置 ScriptableObject
├── MonsterData.cs          # 怪物配置 ScriptableObject
├── Character.cs            # 玩家角色（普攻 + 自动技能 AOE）
├── Monster.cs              # 怪物（向下移动 + 遇阻挡停攻）
├── MonsterSpawner.cs       # Wave 调度 + 定时生成
├── BattleManager.cs        # 战斗流程（Ready/Fighting/Victory/Defeat）
└── BattleSetup.cs          # 一键生成场景