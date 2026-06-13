# Cities: Skylines II Mod 项目知识迁移文档

> **源项目**: Transit Scope (Vaelric) — 一个交通流量查看模组，支持道路/铁路/建筑的统一选择和流量饼图分析。
>
> **目标读者**: 其他 AI 或开发者，需要基于官方 CS2 Mod 体系开发新模组。
>
> **文档版本**: 基于 Transit Scope v1.5 源码提取

---

## 1. 项目目标与可迁移经验概览

### 从当前项目提取到的经验

Transit Scope 是一个"信息查看"型 Mod，核心功能链路为：

1. 用户在 UI 上点击按钮 → 激活自定义选择工具
2. 鼠标悬停在道路/建筑上 → 自定义 raycast 解析 Entity → 高亮
3. 左键确认选择 → 触发 ECS Entity 遍历管线 → 统计该路段/建筑的流量构成
4. 统计结果 JSON 序列化 → 通过 Colossal UI Binding 推送到 React 前端
5. 前端解析 JSON → 渲染饼图和分类明细

**可迁移的核心模式**：

| 模式 | 迁移价值 | 复杂度 |
|------|---------|--------|
| `IMod` + `UpdateAt<>` 系统注册 | 所有 Mod 必须 | 低 |
| `ToolBaseSystem` 自定义工具 | 需要场景交互的 Mod | 中 |
| `UISystemBase` + `ValueBinding`/`TriggerBinding` | 需要 UI 的 Mod | 中 |
| `GameSystemBase` 纯逻辑系统 | 通用后处理/分析 Mod | 低 |
| `OverlayRenderSystem` 世界空间绘制 | 需要 3D 可视化的 Mod | 中 |
| `EntityQuery` + `EntityManager` 遍历 | 需要查询游戏实体的 Mod | 高 |
| React + `cs2/*` 模块 UI 注入 | 需要自定义 UI 的 Mod | 中 |

### 通用化后的开发指导

**新建一个 Mod 的推荐最小架构**：

```
C# 后端 (至少 2 个文件):
├── Mod.cs              ← IMod 入口
├── Logger.cs           ← 日志封装
└── [可选] XxxSystem.cs ← 游戏逻辑

UI 前端 (至少 3 个文件):
├── src/index.tsx        ← ModRegistrar 入口
├── src/bindings.ts      ← C#-UI 绑定
└── src/[Component].tsx  ← UI 组件
```

---

## 2. 官方模组入口与生命周期

### 从当前项目提取到的经验

**入口文件**: `code/Core/Mod.cs`

```csharp
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace Transit_Scope.code
{
    public class Mod : IMod
    {
        public void OnLoad(UpdateSystem updateSystem)
        {
            Logger.Info("Transit Scope 启动");

            // 工具类系统注册到 ToolUpdate 阶段
            updateSystem.UpdateAt<SelectionToolSystem>(SystemUpdatePhase.ToolUpdate);

            // UI 绑定类系统注册到 UIUpdate 阶段
            updateSystem.UpdateAt<UIBridgeSystem>(SystemUpdatePhase.UIUpdate);

            // 通用逻辑系统注册到 UIUpdate 阶段
            updateSystem.UpdateAt<SelectionSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<OverlaySystem>(SystemUpdatePhase.UIUpdate);

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Logger.Info($"Current mod asset at {asset.path}");
                Logger.Info("Transit Scope 完成注册");
            }
        }

        public void OnDispose()
        {
            Logger.Info("Transit Scope 关闭");
        }
    }
}
```

**【CS2 特有】关键要点**：

- `IMod` 接口来自 `Game.Modding` 命名空间，是 Mod 的唯一入口
- `UpdateSystem` 参数由游戏传入，系统通过 `updateSystem.UpdateAt<T>(SystemUpdatePhase.Xxx)` 注册
- `SystemUpdatePhase` 枚举决定系统在哪个阶段运行：
  - `ToolUpdate` — 工具类系统（`ToolBaseSystem` 子类），处理鼠标输入和 raycast
  - `UIUpdate` — UI 绑定和通用逻辑系统
  - `GameSimulation` — 需要访问模拟数据的系统
  - `PrefabUpdate` — 预制件相关
- `GameManager.instance.modManager.TryGetExecutableAsset()` 【CS2 特有】用于获取当前 Mod 的程序集信息
- `OnDispose()` 在 Mod 被禁用或游戏退出时调用，用于清理资源

**Logger 模式**: `code/Core/Logger.cs`

```csharp
using Colossal.Logging;

namespace Transit_Scope
{
    public static class Logger
    {
        private static readonly ILog _log = LogManager.GetLogger(nameof(Transit_Scope));

        public static void Info(string message)
        {
            SafeLog(log => log.Info(message));
        }

        private static void SafeLog(Action<ILog> write)
        {
            if (write == null || _log == null) return;
            try { write(_log); }
            catch
            {
                // Colossal.Logging 在 Unity log handler 内部可能抛出异常
                // 日志绝不能中断模拟更新循环
            }
        }
    }
}
```

**【CS2 特有】** `Colossal.Logging.LogManager.GetLogger()` 获取日志实例，日志输出到游戏日志文件 `Player.log`。

**【可迁移模板】** Logger 的 `SafeLog` 包装模式值得在所有 Mod 中复用——Colossal.Logging 在 Unity 内部可能抛出异常，如果不用 try-catch 包裹，日志调用可能崩溃整个更新循环。

### 通用化后的开发指导

**新建 Mod 入口的标准流程**：

1. 创建 `Mod.cs` 实现 `IMod`
2. 在 `OnLoad` 中按阶段注册所有系统：
   - 先注册工具类系统（`ToolUpdate`）
   - 再注册 UI 系统（`UIUpdate`）
   - 最后注册逻辑系统（`UIUpdate` 或 `GameSimulation`）
3. 创建 `Logger.cs` 封装 `Colossal.Logging`，使用异常安全包装
4. 在 `OnDispose` 中清理资源

---

## 3. 系统注册与 UpdateSystem 使用方式

### 从当前项目提取到的经验

Transit Scope 使用了三种系统基类：

| 系统 | 基类 | 注册阶段 | 职责 |
|------|------|---------|------|
| `SelectionToolSystem` | `ToolBaseSystem` | `ToolUpdate` | 自定义工具：raycast、选择状态管理、鼠标处理 |
| `UIBridgeSystem` | `UISystemBase` | `UIUpdate` | C#-React 双向绑定（ValueBinding / TriggerBinding） |
| `SelectionSystem` | `GameSystemBase` | `UIUpdate` | 协调选择变化 → 刷新统计 → 推送到 UI |
| `OverlaySystem` | `GameSystemBase` | `UIUpdate` | 世界空间道路悬停高亮绘制 |
| `TrafficFlowSystem` | `GameSystemBase` | 未注册到 UpdateSystem | 纯被 SelectionSystem 驱动，内部管线 |

**系统间依赖获取模式**：

所有系统都在 `OnCreate()` 中通过 `World.GetOrCreateSystemManaged<T>()` 获取对其他系统的引用：

```csharp
protected override void OnCreate()
{
    base.OnCreate();
    m_ToolSystem = World.GetOrCreateSystemManaged<SelectionToolSystem>();
    m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
}
```

**【CS2 特有】** `World.GetOrCreateSystemManaged<T>()` 是 ECS 世界中获取系统引用的标准方式。`World.DefaultGameObjectInjectionWorld` 是默认的 ECS World。

**TrafficFlowSystem 的 `[UpdateInGroup]` 属性**：

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class TrafficFlowSystem : GameSystemBase
```

**【Unity / ECS 相关】** 即使不在 `Mod.OnLoad` 中通过 `UpdateAt` 注册，使用 `[UpdateInGroup]` 属性也能让 Unity ECS 框架自动调度该系统。

### 通用化后的开发指导

**系统注册决策树**：

```
你的系统需要处理鼠标/键盘输入吗？
├── 是 → 继承 ToolBaseSystem → 注册到 ToolUpdate
└── 否 → 需要和 UI 前端通信吗？
    ├── 是 → 继承 UISystemBase → 注册到 UIUpdate
    └── 否 → 继承 GameSystemBase → 注册到合适的阶段
```

**系统间通信的推荐方式**：

1. 系统 A 持有系统 B 的引用（通过 `World.GetOrCreateSystemManaged<B>()`）
2. 系统 A 暴露 public 属性/方法供系统 B 调用
3. 不要用静态变量或单例传递系统间状态

---

## 4. Tool Mode / 自定义工具实现方式

### 从当前项目提取到的经验

**核心文件**: `code/Selection/SelectionToolSystem.cs`

这是 Transit Scope 最核心的代码，展示了自定义 Tool 的完整实现模式。

**4.1 工具类声明**：

```csharp
public partial class SelectionToolSystem : ToolBaseSystem
{
    public override string toolID => "ScopeTool";  // 【CS2 特有】全局唯一工具标识符
}
```

**【CS2 特有】** `toolID` 必须在游戏所有 Mod 中唯一。建议使用 Mod 名前缀，如 `"MyMod_MyTool"`。

**4.2 工具生命周期**：

```
OnCreate()          → 获取依赖系统引用，设置 Enabled = false（默认不激活）
  ↓
OnStartRunning()    → 工具被激活时调用，初始化状态
  ↓
OnUpdate()          → 每帧调用，处理输入和逻辑
  ↓
OnStopRunning()     → 工具被停用时调用，清理状态
```

```csharp
protected override void OnCreate()
{
    base.OnCreate();
    m_GameToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
    m_TrafficRoutesSystem = World.GetOrCreateSystemManaged<TrafficRoutesSystem>();
    Enabled = false; // 【CS2 特有】初始不激活，等待 UI 触发
}

protected override void OnStartRunning()
{
    base.OnStartRunning();
    m_State = State.Selecting;
    ResetState();
}

protected override void OnStopRunning()
{
    base.OnStopRunning();
    m_State = State.Default;
    ClearVanillaHoverHighlight();
    ResetState();
}
```

**4.3 激活/停用工具**：

```csharp
public void EnableSelectionMode()
{
    // 【CS2 特有】通过 ToolSystem 切换当前激活的工具
    if (m_GameToolSystem.activeTool != this)
    {
        m_GameToolSystem.selected = Entity.Null;
        m_GameToolSystem.activeTool = this;
    }
}

public void DisableSelectionMode()
{
    if (m_GameToolSystem.activeTool == this)
    {
        m_GameToolSystem.selected = Entity.Null;
        m_GameToolSystem.activeTool = m_DefaultToolSystem; // 回退到默认工具
    }
}
```

**【CS2 特有】关键点**：
- `ToolSystem.activeTool` 控制当前激活的工具
- `m_DefaultToolSystem` 是 `ToolBaseSystem` 内置的引用，指向游戏的默认选择工具
- 切换工具时必须同时清理 `ToolSystem.selected`

**4.4 Raycast 配置**：

```csharp
public override void InitializeRaycast()
{
    base.InitializeRaycast();

    // 【CS2 特有】碰撞掩码：地面、地上、地下
    m_ToolRaycastSystem.collisionMask =
        CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;

    // 【CS2 特有】类型掩码：网络（道路/轨道）和静态对象（建筑）
    m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.StaticObjects;

    // 【CS2 特有】射线标志：子元素、货物、乘客、编辑器容器
    m_ToolRaycastSystem.raycastFlags =
        RaycastFlags.SubElements |
        RaycastFlags.Cargo |
        RaycastFlags.Passenger |
        RaycastFlags.EditorContainers;

    // 【CS2 特有】网络层掩码：限制可命中的道路/轨道类型
    m_ToolRaycastSystem.netLayerMask =
        Layer.Road |
        Layer.PublicTransportRoad |
        Layer.Pathway |
        Layer.TrainTrack |
        Layer.SubwayTrack |
        Layer.TramTrack;

    m_ToolRaycastSystem.iconLayerMask = IconLayerMask.None;
    m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
}
```

**【CS2 特有】** `ToolRaycastSystem` 的配置决定了工具能"看到"游戏世界中的哪些东西。这是 CS2 Mod 中最关键的配置之一，每个需要场景交互的 Mod 都必须正确设置。

**4.5 OnUpdate 主循环**：

```csharp
protected override JobHandle OnUpdate(JobHandle inputDeps)
{
    if (m_FocusChanged) return inputDeps; // 焦点变化时跳过

    if (m_GameToolSystem == null || m_GameToolSystem.activeTool != this)
        return inputDeps;

    HasNewSelection = false;
    UpdateHoveredTarget();          // 解析 raycast 命中
    SyncVanillaHoverHighlight();    // 同步原版高亮组件

    // 检查选中实体是否被删除
    if (SelectedEntity != Entity.Null && !EntityManager.Exists(SelectedEntity))
        ClearSelectionInternal();

    // 鼠标输入处理
    if (CanAcceptSceneClick() && Mouse.current.leftButton.wasPressedThisFrame)
        ConfirmHoveredTarget();

    if (CanAcceptSceneClick() && Mouse.current.rightButton.wasPressedThisFrame)
        ClearSelectionInternal();

    return inputDeps;
}
```

**【CS2 特有】** 使用 `Mouse.current.leftButton.wasPressedThisFrame` (Unity Input System) 而非 `Input.GetMouseButtonDown`。

**4.6 命中解析到 Entity**：

```csharp
private void UpdateHoveredTarget()
{
    HoveredEntity = Entity.Null;
    HoveredKind = SelectionKind.None;
    HoveredSourceEntity = Entity.Null;

    // 【CS2 特有】获取 raycast 结果
    if (!GetRaycastResult(out Entity hitEntity, out _))
        return;

    HoveredSourceEntity = hitEntity;

    // 尝试解析为道路
    Entity roadEntity = EntityResolver.ResolveRoadEdge(EntityManager, hitEntity);
    if (EntityResolver.IsRoad(EntityManager, roadEntity))
    {
        HoveredEntity = roadEntity;
        HoveredKind = SelectionKind.Road;
        return;
    }

    // 回退到建筑
    Entity buildingEntity = EntityResolver.ResolveBuildingEntity(EntityManager, hitEntity);
    if (EntityResolver.IsBuilding(EntityManager, buildingEntity))
    {
        HoveredEntity = buildingEntity;
        HoveredKind = SelectionKind.Building;
    }
}
```

**【CS2 特有】** `GetRaycastResult(out Entity, out RaycastHit)` 是 `ToolBaseSystem` 提供的方法，直接返回命中的 ECS Entity。

**4.7 与原版高亮交互**：

```csharp
private void SyncVanillaHoverHighlight()
{
    // 收集需要高亮的 Entity 列表
    List<Entity> targetEntities = ...;

    // 【CS2 特有】添加/移除 Highlighted 组件控制原版高亮
    for (int i = 0; i < targetEntities.Count; i++)
    {
        if (!EntityManager.HasComponent<Highlighted>(entity))
        {
            EntityManager.AddComponent<Highlighted>(entity);
            MarkEntityUpdated(entity);
        }
    }
}

private void MarkEntityUpdated(Entity entity)
{
    if (!EntityManager.HasComponent<Updated>(entity))
        EntityManager.AddComponent<Updated>(entity);
}
```

**【CS2 特有】** `Highlighted` 组件来自 `Game.Common`（或 `Game.Notifications`），添加后原版渲染器会绘制高亮轮廓。`Updated` 组件是信号组件，告知下游系统该实体发生了变化。

**4.8 状态管理模式**：

```csharp
public enum State { Default, Selecting }
public enum SelectionKind { None = 0, Road = 1, Building = 2 }

// 公开只读属性供其他系统消费
public Entity HoveredEntity { get; private set; }
public SelectionKind HoveredKind { get; private set; }
public Entity SelectedEntity { get; private set; }
public bool HasNewSelection { get; private set; }
public bool IsSelecting => m_State == State.Selecting;

// 公开方法供 UI 系统调用
public void EnableSelectionMode() { ... }
public void DisableSelectionMode() { ... }
public void ConfirmHoveredTarget() { ... }
```

**与 TrafficRoutesSystem 的交互**：

```csharp
// 进入选择模式时自动开启原版交通路线显示
private void EnsureVanillaTrafficRoutesVisible()
{
    bool wasVisible = m_TrafficRoutesSystem.routesVisible;
    m_OpenedVanillaTrafficRoutes = !wasVisible;
    if (!wasVisible)
        m_TrafficRoutesSystem.routesVisible = true;
}

// 退出选择模式时恢复原状
private void RestoreVanillaTrafficRoutesVisibility()
{
    if (m_OpenedVanillaTrafficRoutes)
        m_TrafficRoutesSystem.routesVisible = false;
}
```

**【CS2 特有】** `TrafficRoutesSystem` 是原版交通路线可视化系统，通过修改 `routesVisible` 属性控制可见性。这是利用原版系统增强 Mod 功能的典型示例。

### 通用化后的开发指导

**创建自定义工具的标准流程**：

1. 创建类继承 `ToolBaseSystem`
2. 设置 `toolID` 为全局唯一字符串
3. 在 `OnCreate` 中：
   - 调用 `base.OnCreate()`
   - 获取 `ToolSystem`、`DefaultToolSystem` 及其他依赖系统引用
   - 设置 `Enabled = false`
4. 覆盖 `InitializeRaycast()` 配置碰撞/类型/层掩码
5. 在 `OnStartRunning()` 中初始化工具状态
6. 在 `OnUpdate()` 中：
   - 检查焦点和激活状态
   - 调用 `GetRaycastResult()` 获取命中
   - 处理鼠标/键盘输入
   - 更新状态属性
7. 在 `OnStopRunning()` 中清理状态
8. 暴露 public 属性/方法供 UI 系统调用
9. 通过 `ToolSystem.activeTool = this` 激活工具

**工具状态可被其他系统查询的模式**：

```csharp
// Tool 系统暴露属性
public Entity SelectedEntity { get; private set; }
public bool HasNewSelection { get; private set; }

// 其他系统消费
Entity selected = m_ToolSystem.SelectedEntity;
bool isNew = m_ToolSystem.HasNewSelection;
```

---

## 5. UI 注入、面板与交互机制

### 从当前项目提取到的经验

Transit Scope 使用 **React + TypeScript + cs2 官方 UI 模块** 实现前端 UI。

**5.1 UI 入口**: `UI/src/index.tsx`

```tsx
import { ModRegistrar } from "cs2/modding";
import { SelectionButton } from "./SelectionButton";

const register: ModRegistrar = (moduleRegistry) => {
    // 【CS2 特有】向游戏 UI 的指定插槽注入组件
    moduleRegistry.append("GameTopLeft", SelectionButton);
    console.log("Transit Scope UI mounted at GameTopLeft.");
};

export default register;
```

**【CS2 特有】关键概念**：

- `ModRegistrar` — Mod UI 注册函数类型（来自 `cs2/modding`）
- `moduleRegistry.append(slot, component)` — 向游戏 UI 插槽追加 React 组件
- 可用插槽包括：`"GameTopLeft"`、`"GameTopCenter"`、`"GameBottomRight"`、`"MainToolbar"`、`"ToolOptions"` 等

**5.2 C#-UI 绑定**: `UI/src/bindings.ts`

```tsx
import { bindValue, trigger } from "cs2/api";

// 【CS2 特有】ValueBinding：C# 推送到 UI 的数据流
export const isActiveBinding = bindValue<boolean>("transitScope", "isActive");
export const hasStatsBinding = bindValue<boolean>("transitScope", "hasStats");
export const statsJsonBinding = bindValue<string>("transitScope", "statsJson");

// 【CS2 特有】TriggerBinding：UI 触发 C# 方法的回调
export const toggleTransitScope = (active: boolean) => {
    trigger("transitScope", "toggle", active);
};

export const confirmTransitScope = () => {
    trigger("transitScope", "confirm");
};
```

**【CS2 特有】** `bindValue<T>(group, name)` 和 `trigger(group, name, args?)` 的 group 和 name 必须与 C# 端 `ValueBinding` / `TriggerBinding` 构造函数的参数完全匹配。

**5.3 UI 组件消费绑定值**:

```tsx
import { useValue } from "cs2/api";

export const SelectionButton = () => {
    // 【CS2 特有】useValue Hook 订阅 C# 推送的值变更
    const isActive = useValue(isActiveBinding);
    const hasStats = useValue(hasStatsBinding);

    const handleToggle = () => {
        toggleTransitScope(!isActive);  // 触发 C# 端回调
    };

    return (
        <Button
            variant="floating"     // 【CS2 特有】浮动按钮样式
            selected={isActive}
            onSelect={handleToggle}
        >
            <SelectionIcon active={isActive} />
        </Button>
    );
};
```

**【CS2 特有】** `cs2/ui` 导出的组件（`Button`、`Portal` 等）是游戏内置的 UI 组件库，应优先使用以保持视觉风格一致。

**5.4 面板组件与 Portal**:

```tsx
import { Portal } from "cs2/ui";

export const StatsPanel = ({ anchor }: Props) => {
    // ... 读取 bindings 值 ...

    return (
        <Portal>
            {/* 【CS2 特有】Portal 渲染到游戏 UI 层，不受父组件 CSS 限制 */}
            <div style={{ position: "absolute", top: anchor.y, left: anchor.x }}>
                {/* 面板内容 */}
            </div>
        </Portal>
    );
};
```

**【CS2 特有】** `Portal` 将组件渲染到 Cohtml UI 树的高层，避免被父容器的 overflow/clip 裁剪。所有浮动面板都应使用 Portal。

**5.5 本地化 Hook**: `UI/src/localization.ts`

```tsx
import { useLocalization } from "cs2/l10n";

export const useTranslate = () => {
    const { translate: gameTranslate } = useLocalization();

    return useCallback((key: string | undefined, fallback: string, arg?: string): string => {
        if (!key) return fallback;
        const translated = gameTranslate(key, fallback);
        if (!translated || translated === key) return fallback;
        if (arg !== undefined) return translated.replace("{0}", arg);
        return translated;
    }, [gameTranslate]);
};
```

**【CS2 特有】** `cs2/l10n` 的 `useLocalization()` Hook 返回游戏的本地化翻译函数，会自动根据当前游戏语言查找对应的翻译键值。

### 通用化后的开发指导

**UI 注入的标准流程**：

1. 在 `UI/src/index.tsx` 中导出 `ModRegistrar` 函数
2. 使用 `moduleRegistry.append(slot, component)` 向游戏插槽注入组件
3. 在 `UI/src/bindings.ts` 中定义所有 `bindValue` 和 `trigger` 绑定
4. 在组件中使用 `useValue(binding)` 读取 C# 端推送的值
5. 使用 `trigger()` 将用户交互发送到 C# 端
6. 浮动面板使用 `<Portal>` 包装
7. 按钮使用 `cs2/ui` 的 `Button` 组件和 `floating` variant

**UI 插槽选择建议**：

| 插槽 | 场景 |
|------|------|
| `GameTopLeft` | 侧边栏按钮、浮动面板触发按钮 |
| `GameTopCenter` | 顶部工具栏扩展 |
| `GameBottomRight` | 信息面板 |
| `MainToolbar` | 主工具栏（需要图标资源） |

---

## 6. C# 后端与 UI 前端通信方式

### 从当前项目提取到的经验

**核心文件**: `code/Presentation/UIBridgeSystem.cs`

```csharp
public partial class UIBridgeSystem : UISystemBase
{
    private ValueBinding<bool> m_ActiveBinding;
    private ValueBinding<bool> m_HasStatsBinding;
    private ValueBinding<string> m_StatsJsonBinding;

    protected override void OnCreate()
    {
        base.OnCreate();

        // 【CS2 特有】创建 ValueBinding（C# → UI 数据流）
        AddBinding(m_ActiveBinding = new ValueBinding<bool>("transitScope", "isActive", false));
        AddBinding(m_HasStatsBinding = new ValueBinding<bool>("transitScope", "hasStats", false));
        AddBinding(m_StatsJsonBinding = new ValueBinding<string>("transitScope", "statsJson", string.Empty));

        // 【CS2 特有】创建 TriggerBinding（UI → C# 事件流）
        AddBinding(new TriggerBinding<bool>("transitScope", "toggle", OnToggleMode));
        AddBinding(new TriggerBinding("transitScope", "confirm", OnConfirmSelection));
    }

    protected override void OnUpdate()
    {
        base.OnUpdate();
        SyncBindings(); // 每帧同步状态
    }

    private void OnToggleMode(bool active)
    {
        if (m_ActiveBinding.value == active) return;
        m_ActiveBinding.Update(active);

        if (active)
            m_SelectionToolSystem.EnableSelectionMode();
        else
        {
            m_SelectionToolSystem.DisableSelectionMode();
            ClearStats();
        }
    }

    private void OnConfirmSelection()
    {
        if (!m_ActiveBinding.value) return;
        m_SelectionToolSystem.ConfirmHoveredTarget();
    }

    // 【CS2 特有】从其他系统调用的数据推送方法
    internal void PresentStats(RouteStatisticsPanelPayload stats)
    {
        m_HasStatsBinding.Update(true);
        string json = stats.ToJson();
        m_StatsJsonBinding.Update(json);
    }

    internal void ClearStats()
    {
        m_HasStatsBinding.Update(false);
        m_StatsJsonBinding.Update(string.Empty);
    }
}
```

**【CS2 特有】核心机制**：

- `ValueBinding<T>(group, name, defaultValue)` — C# 持有值，UI 订阅变更
- `TriggerBinding(group, name, callback)` / `TriggerBinding<T>(group, name, callback)` — UI 调用 trigger 时 C# 执行回调
- `AddBinding()` 必须在 `OnCreate()` 中调用
- `m_Binding.Update(newValue)` 更新值后自动推送到 UI
- group 和 name 组合必须在同一个 Mod 内唯一

**【当前项目特例】** Transit Scope 在 `UIBridgeSystem` 中直接集中管理所有绑定。对于绑定数量较多的大型 Mod，建议将绑定分散到多个 UISystemBase 子类中。

**JSON 序列化方式**：

本项目使用 `DataContractJsonSerializer` 而非 Newtonsoft.Json 或 System.Text.Json，因为后者在 CS2 的 .NET Framework 4.8 + Unity 环境下可能存在兼容性问题。

```csharp
[DataContract]
internal sealed class RouteStatisticsPanelPayload
{
    [DataMember(Name = "selectedEntity")]
    public int SelectedEntityIndex { get; set; }

    [DataMember(Name = "buckets")]
    public List<RouteStatisticsBucket> Buckets { get; } = new();

    public string ToJson()
    {
        DataContractJsonSerializer serializer = new(typeof(RouteStatisticsPanelPayload));
        using MemoryStream stream = new();
        serializer.WriteObject(stream, this);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
```

**【当前项目特例】** 使用 `DataContract` / `DataMember` 属性标记序列化字段，`Name` 参数的值必须与 TypeScript 接口的字段名完全匹配。

### 通用化后的开发指导

**C#-UI 通信的标准架构**：

```
┌──────────────────────────────────────────┐
│ C# UIBridgeSystem (继承 UISystemBase)     │
│                                           │
│ ValueBinding<T>  ───推送──→  UI useValue() │
│ TriggerBinding   ←──触发──  UI trigger()  │
│                                           │
│ ↑ 被其他 GameSystemBase 调用               │
│ ↑ PresentXxx() / ClearXxx()               │
└──────────────────────────────────────────┘
```

**新增绑定的步骤**：

1. 在 C# `UIBridgeSystem.OnCreate()` 中用 `AddBinding()` 注册
2. 在 `UI/src/bindings.ts` 中用 `bindValue<T>(group, name)` 或 `trigger()` 声明对应绑定
3. 在 React 组件中用 `useValue(binding)` 订阅值
4. 在需要更新时调用 C# 端的 `binding.Update(value)`

**通信方向总结**：

| 方向 | C# 端 | UI 端 |
|------|-------|-------|
| C# → UI (数据) | `ValueBinding<T>.Update(value)` | `useValue(binding)` |
| UI → C# (事件) | `TriggerBinding` 回调方法 | `trigger(group, name, args?)` |

---

## 7. ECS / Entity / Component / Query 相关经验

### 从当前项目提取到的经验

**7.1 EntityResolver — Entity 关系链遍历**

`code/Shared/EntityResolver.cs` 是 Transit Scope 最值得研究的 ECS 相关代码。

```csharp
// 【CS2 特有】判断 Entity 是否为道路
public static bool IsRoad(EntityManager entityManager, Entity entity)
{
    return entity != Entity.Null &&
           entityManager.Exists(entity) &&
           entityManager.HasComponent<Game.Net.Edge>(entity); // 有 Edge 组件 = 道路/轨道
}

// 【CS2 特有】判断 Entity 是否为建筑
public static bool IsBuilding(EntityManager entityManager, Entity entity)
{
    if (entity == Entity.Null || !entityManager.Exists(entity)) return false;
    if (entityManager.HasComponent<Game.Net.Edge>(entity)) return false; // 排除道路

    return entityManager.HasComponent<Game.Prefabs.PrefabRef>(entity) &&
           (entityManager.HasComponent<Game.Buildings.Building>(entity) ||
            entityManager.HasComponent<Game.Objects.Transform>(entity));
}
```

**【CS2 特有】Owner 链遍历**：

```csharp
// Entity 通过 Owner 组件形成父子链，需要向上遍历找到根
private static Entity ResolveOwnerRoot(EntityManager entityManager, Entity entity)
{
    Entity current = entity;
    for (int depth = 0; depth < MaxOwnerTraversalDepth; depth++)
    {
        if (current == Entity.Null || !entityManager.Exists(current) ||
            !entityManager.HasComponent<Game.Common.Owner>(current))
            break;

        Game.Common.Owner owner = entityManager.GetComponentData<Game.Common.Owner>(current);
        if (owner.m_Owner == Entity.Null || !entityManager.Exists(owner.m_Owner))
            break;

        current = owner.m_Owner; // 向上遍历
    }
    return current;
}
```

**【CS2 特有】关键的 Entity 关系组件**：

| 组件 | 命名空间 | 用途 |
|------|---------|------|
| `Owner` | `Game.Common` | 父子关系，`m_Owner` 指向父实体 |
| `Target` | `Game.Common` | 目标引用，`m_Target` 指向目标实体 |
| `Attached` | `Game.Objects` | 附着关系，`m_Parent` 指向父对象 |
| `Attachment` | `Game.Objects` | 附着集合，`m_Attached` 指向附着子对象 |
| `Temp` | `Game.Tools` | 临时实体标记，`m_Original` 指向原始实体 |
| `Edge` | `Game.Net` | 网络边（道路/轨道段）标记组件 |
| `Building` | `Game.Buildings` | 建筑标记组件 |
| `PrefabRef` | `Game.Prefabs` | 预制件引用（几乎所有"真实"实体都有） |
| `SubObject` | `Game.Objects` | 子对象 Buffer，`m_SubObject` |
| `SubNet` | `Game.Net` | 子网络 Buffer |
| `SubLane` | `Game.Net` | 子车道 Buffer |
| `SubArea` | `Game.Areas` | 子区域 Buffer |
| `InstalledUpgrade` | `Game.Buildings` | 建筑升级组件 Buffer |
| `Highlighted` | `Game.Common` | 添加此组件后原版渲染高亮轮廓 |
| `Updated` | `Game.Common` | 信号组件，告知下游系统该实体已变更 |

**【CS2 特有】建筑高亮实体收集**：

```csharp
private static void AddBuildingHighlightEntityRecursive(
    EntityManager entityManager, Entity entity,
    HashSet<Entity> visited, List<Entity> result)
{
    if (!visited.Add(entity)) return; // 防循环

    // 收集有 PrefabRef 但不是道路的实体（即建筑相关实体）
    if (entityManager.HasComponent<Game.Prefabs.PrefabRef>(entity) &&
        !entityManager.HasComponent<Game.Net.Edge>(entity))
        result.Add(entity);

    // 递归收集子对象
    if (entityManager.HasBuffer<Game.Objects.SubObject>(entity))
    {
        DynamicBuffer<Game.Objects.SubObject> subObjects =
            entityManager.GetBuffer<Game.Objects.SubObject>(entity);
        for (int i = 0; i < subObjects.Length; i++)
            AddBuildingHighlightEntityRecursive(entityManager, subObjects[i].m_SubObject, visited, result);
    }

    // 递归收集升级组件
    if (entityManager.HasBuffer<Game.Buildings.InstalledUpgrade>(entity))
    {
        DynamicBuffer<Game.Buildings.InstalledUpgrade> upgrades =
            entityManager.GetBuffer<Game.Buildings.InstalledUpgrade>(entity);
        for (int i = 0; i < upgrades.Length; i++)
            AddBuildingHighlightEntityRecursive(entityManager, upgrades[i].m_Upgrade, visited, result);
    }

    // 递归收集附着对象
    if (entityManager.HasComponent<Game.Objects.Attachment>(entity))
    {
        Entity attached = entityManager.GetComponentData<Game.Objects.Attachment>(entity).m_Attached;
        AddBuildingHighlightEntityRecursive(entityManager, attached, visited, result);
    }
}
```

**【CS2 特有】关键技巧**：
- 使用 `HashSet<Entity>` 防止 Entity 关系图中的循环引用导致无限递归
- 使用 `DynamicBuffer<T>` 访问 ECS Buffer 组件（类似数组但属于 ECS 原生结构）
- `MAX_OWNER_TRAVERSAL_DEPTH = 8` 是经验值，防止异常深度的链导致性能问题

**7.2 EntityQuery 构建**：

```csharp
// 在 RouteStatisticsPipeline 构造函数中
m_PathSourceQuery = m_EntityManager.CreateEntityQuery(new EntityQueryDesc
{
    All = new[] { ComponentType.ReadOnly<UpdateFrame>() },
    Any = new[]
    {
        ComponentType.ReadOnly<PathOwner>(),
        ComponentType.ReadOnly<TrainCurrentLane>()
    },
    None = new[]
    {
        ComponentType.ReadOnly<Deleted>(),
        ComponentType.ReadOnly<Game.Tools.Temp>()
    }
});

// 使用查询
using NativeArray<Entity> entities = m_PathSourceQuery.ToEntityArray(Allocator.Temp);
```

**【Unity / ECS 相关】** `EntityQueryDesc` 使用 `All`(必须全部有)、`Any`(至少有一个)、`None`(必须都没有) 来过滤 Entity。

**7.3 EntityManager 常用 API**：

```csharp
// 存在性检查
EntityManager.Exists(entity)

// 组件检查
EntityManager.HasComponent<T>(entity)

// 获取组件数据 (struct/component data)
T data = EntityManager.GetComponentData<T>(entity)

// 获取 Buffer 组件
DynamicBuffer<T> buffer = EntityManager.GetBuffer<T>(entity)

// 添加/移除组件
EntityManager.AddComponent<T>(entity)
EntityManager.RemoveComponent<T>(entity)
```

**【Unity / ECS 相关】** `GetComponentData<T>()` 只能用于实现 `IComponentData` 的结构体，`GetBuffer<T>()` 用于实现 `IBufferElementData` 的结构体。

**7.4 OverlayRenderSystem 使用**：

```csharp
// code/Presentation/OverlaySystem.cs
protected override void OnUpdate()
{
    // 【CS2 特有】获取 Overlay 渲染 Buffer
    OverlayRenderSystem.Buffer overlayBuffer =
        m_OverlayRenderSystem.GetBuffer(out JobHandle bufferHandle);
    bufferHandle.Complete(); // 等待前序 Job 完成

    DrawHoveredOverlay(overlayBuffer);
}

private void DrawRoadHover(OverlayRenderSystem.Buffer overlayBuffer, Entity edgeEntity)
{
    // 读取 Entity 的 Curve 数据
    Curve curveData = EntityManager.GetComponentData<Curve>(edgeEntity);

    // 绘制道路曲线
    OverlayHelpers.DrawCurve(
        overlayBuffer,
        curveData.m_Bezier,
        Color.clear,           // 边框色
        OverlayColors.MainFill, // 填充色
        0.01f,                 // 边框宽度
        roadWidth + 4.5f);     // 曲线宽度
}
```

**【CS2 特有】** `OverlayRenderSystem` 是原版的世界空间渲染系统，通过 `GetBuffer()` 获取绘制命令缓冲区。所有世界空间 UI / 高亮 / 指示器都通过这个系统绘制。

**7.5 ECS 组件分类体系（RouteStatisticsPipeline 中的车辆分类）**：

```csharp
// 【CS2 特有】通过检查 Entity 及其 Controller/Vehicle 上的特定组件来分类
private RouteVisualizationKind ClassifyCarLikeEntity(Entity sourceEntity)
{
    // 公共服务车辆
    if (HasAnyComponentOnClassificationEntities<
        Game.Vehicles.Ambulance, Game.Vehicles.FireEngine, Game.Vehicles.GarbageTruck,
        Game.Vehicles.Hearse, Game.Vehicles.MaintenanceVehicle, ...>(sourceEntity))
        return RouteVisualizationKind.PublicService;

    // 公共交通
    if (HasAnyComponentOnClassificationEntities<
        Game.Vehicles.PublicTransport, PassengerTransport, Game.Vehicles.Taxi>(sourceEntity))
        return RouteVisualizationKind.PublicTransport;

    // 货运
    if (HasAnyComponentOnClassificationEntities<
        Game.Vehicles.CargoTransport, Game.Vehicles.DeliveryTruck, GoodsDeliveryVehicle>(sourceEntity))
        return RouteVisualizationKind.CargoFreight;

    // ... 回退到私家车
}
```

**【CS2 特有】** 这是一种通过 ECS 组件标签来分类 Entity 的标准方法——检查 Entity 及其关联 Entity（Controller、CurrentVehicle）上的特定组件。

### 通用化后的开发指导

**查询游戏实体的标准流程**：

1. 通过 `EntityManager.CreateEntityQuery(EntityQueryDesc)` 创建查询
2. 使用 `query.ToEntityArray(Allocator.Temp)` 或 `query.ToComponentDataArray<T>()` 获取结果
3. 对结果进行遍历和处理
4. 处理完成后 `NativeArray.Dispose()`（如果使用 Temp allocator，会在帧结束时自动释放）

**处理 Entity 关系的安全模式**：

```
1. 始终先调用 EntityManager.Exists(entity) 检查有效性
2. 使用深度限制防止无限循环（Owner 链、Target 链等）
3. 使用 HashSet<Entity> 防止重复和循环引用
4. 检查 Temp 组件的 m_Original 获取临时实体的原始引用
5. 通过 PrefabRef 区分"真实实体"和"运行时临时对象"
```

---

## 8. 设置、配置与本地化

### 从当前项目提取到的经验

Transit Scope 采用了一种**轻量化、解耦**的本地化方案：后端只传输原始数据/类型枚举，前端负责将其映射为本地化键值，并依赖第三方 Mod `I18n EveryWhere` 进行资源加载。

**8.1 本地化文件组织**：

`lang/` 目录下按语言代码（ISO 标准）命名的 JSON 文件。这种结构是 `I18n EveryWhere` 识别的标准路径。

```
lang/
├── en-US.json      ← 英语（必须存在，作为兜底）
├── zh-HANS.json    ← 简体中文
└── de-DE.json      ← 德语
```

文件格式为平铺的 key-value JSON：

```json
{
  "stats.title.main": "Transit Statistics",
  "stats.total": "Total",
  "stats.item.private_cars": "Private Cars",
  "stats.item.no_traffic": "No Traffic"
}
```

**8.2 C#-UI 数据传输模式**：

后端不直接发送翻译后的字符串，而是发送枚举名或稳定的 Key。

```csharp
// code/Analysis/RouteStatisticsModels.cs
[DataMember(Name = "kind")]
public RouteVisualizationKind Kind { get; set; } // 发送枚举值
```

**8.3 前端本地化适配器 (UI/src/localization.ts)**：

封装 `cs2/l10n` 的 `useLocalization` Hook，提供统一的占位符处理和错误回退逻辑。

```tsx
import { useLocalization } from "cs2/l10n";

export const useTranslate = () => {
    const { translate: gameTranslate } = useLocalization();

    return useCallback((key: string | undefined, fallback: string, arg?: string): string => {
        if (!key) return fallback;
        const translated = gameTranslate(key, fallback);

        // 如果翻译结果与 key 相同，说明找不到对应的本地化条目
        if (!translated || translated === key) return fallback;

        // 处理 {0} 占位符替换
        if (arg !== undefined) return translated.replace("{0}", arg);
        return translated;
    }, [gameTranslate]);
};
```

**8.4 键值映射模式 (UI/src/StatsPanel.tsx)**：

在组件层定义一个 Map，将后端枚举映射到 `lang/*.json` 中的键值。

```tsx
// 映射表：将后端枚举转换为本地化键
const kindLabelKeyMap: Record<RouteVisualizationKind, string> = {
    CargoFreight: "stats.item.cargo",
    PrivateCar: "stats.item.private_cars",
    // ...
};

// 使用
const translate = useTranslate();
const label = translate(kindLabelKeyMap[bucket.kind], bucket.kind);
```

**8.5 构建与部署集成**：

在 `.csproj` 中确保 `lang/` 文件夹及其内容被复制到构建输出目录。

```xml
<ItemGroup>
    <None Include="lang\**\*.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
</ItemGroup>
```

**【当前项目特例】** 构建时将所有 `lang/**/*.json` 文件复制到输出目录，配合 `I18n EveryWhere` 的约定路径加载。

**8.6 声明依赖**：

在 `Properties/PublishConfiguration.xml` 中声明对 `I18n EveryWhere` 的依赖，确保用户在安装此 Mod 时自动安装翻译支持。

```xml
<Dependency Id="75426" DisplayName="I18n EveryWhere" />
```

**【CS2 特有】** `PublishConfiguration.xml` 中声明的 `Dependency` 在发布到 PDX Mods 时生效，游戏启动时会确保依赖 Mod 先加载。

### 通用化后的开发指导

**最佳实践总结**：

1. **后端哑化**：后端系统不应感知语言，只输出 DataContract 定义的原始数据。
2. **前端映射**：在 React 端进行 UI 呈现时的翻译映射，便于调试和热重载。
3. **适配器封装**：始终封装原生的 `translate` 函数，处理 `null`、`undefined` 和 `fallback` 逻辑。
4. **利用成熟生态**：优先使用 `I18n EveryWhere` 方案。它支持自动合并、多 Mod 冲突处理，且允许玩家通过修改 JSON 文件来修正翻译，无需重新编译。

**本地化方案选择矩阵**：

| 维度 | 方案 A: I18n EveryWhere (推荐) | 方案 B: C# 手动加载 |
|------|--------------------------------|-------------------|
| 复杂度 | 极低，仅需 JSON 文件 | 高，需编写文件读取和注册逻辑 |
| 维护性 | 玩家可自由修改 JSON | 需要重新编译 DLL |
| 性能 | 游戏启动时统一加载，无运行时开销 | 视实现而定 |
| 适用场景 | 绝大多数 Mod | 极致隐私或不希望外部修改翻译 |

**推荐本地化文件规范**：

- 使用 `en-US.json` 作为默认回退语言
- key 命名使用点号分隔：`modname.section.element`
- 每个 key 在所有语言文件中都应存在（至少 en-US.json 必须包含所有 key）
- 支持 `{0}` 占位符用于参数替换

---

## 9. 项目结构与资源组织建议

### 从当前项目提取到的经验

**9.1 完整目录结构**：

```
Transit Scope/
├── Transit Scope.sln
├── Transit Scope.csproj
├── README.md
├── .gitignore
│
├── code/                          # C# 后端源代码
│   ├── Core/
│   │   ├── Mod.cs                 # IMod 入口
│   │   └── Logger.cs             # 日志封装
│   ├── Selection/
│   │   ├── SelectionToolSystem.cs # 自定义工具 (ToolBaseSystem)
│   │   └── SelectionSystem.cs     # 选择协调逻辑 (GameSystemBase)
│   ├── Presentation/
│   │   ├── UIBridgeSystem.cs      # C#-UI 通信 (UISystemBase)
│   │   └── OverlaySystem.cs       # 世界空间渲染 (GameSystemBase)
│   ├── Analysis/
│   │   ├── TrafficFlowSystem.cs   # 统计外观类 (GameSystemBase)
│   │   ├── RouteStatisticsPipeline.cs # 核心统计管线
│   │   └── RouteStatisticsModels.cs   # 数据 DTO
│   └── Shared/
│       ├── EntityResolver.cs      # Entity 关系解析
│       ├── OverlayColors.cs       # 渲染颜色常量
│       └── OverlayHelpers.cs      # 渲染辅助函数
│
├── UI/                            # TypeScript/React 前端
│   ├── mod.json                   # UI Mod 元数据
│   ├── package.json               # npm 依赖
│   ├── tsconfig.json              # TypeScript 配置
│   ├── webpack.config.js          # Webpack 构建配置
│   ├── src/
│   │   ├── index.tsx              # ModRegistrar 入口
│   │   ├── bindings.ts            # C#-UI 绑定声明
│   │   ├── localization.ts        # 本地化 Hook
│   │   ├── routeStatsContracts.ts # TS 接口 (匹配 C# DTO)
│   │   ├── SelectionButton.tsx    # 工具栏按钮组件
│   │   ├── SelectionIcon.tsx      # SVG 图标组件
│   │   └── StatsPanel.tsx         # 统计面板组件
│   ├── tools/
│   │   └── css-presence.js        # Webpack 插件
│   └── types/                     # *.d.ts 类型声明
│       ├── api.d.ts
│       ├── modding.d.ts
│       ├── ui.d.ts
│       ├── l10n.d.ts
│       └── ...
│
├── lang/                          # 本地化 JSON
│   ├── en-US.json
│   ├── zh-HANS.json
│   └── de-DE.json
│
├── Properties/                    # 发布资源
│   ├── PublishConfiguration.xml   # PDX Mods 发布配置
│   ├── Thumbnail.jpg
│   ├── 1.jpg ～ 4.jpg             # 截图
│   └── PublishProfiles/           # 发布配置文件
│
├── Knowledge Base/                # 开发笔记 (当前项目特例)
│
└── Library/                       # Rider IDE 缓存
```

**9.2 后端代码组织模式**：

按**功能职责**而非技术层分目录：

- `Core/` — 入口和基础设施
- `Selection/` — 选择交互
- `Presentation/` — UI 桥接和视觉
- `Analysis/` — 数据分析逻辑
- `Shared/` — 跨模块共享的工具类

**9.3 前端代码组织模式**：

- `src/index.tsx` — 唯一入口
- `src/bindings.ts` — 所有绑定集中声明
- `src/` 下每个组件一个文件
- `src/routeStatsContracts.ts` — TypeScript 接口匹配 C# DTO
- `types/` — 游戏 API 的类型声明

**9.4 .csproj 关键配置**：

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <PublishConfigurationPath>Properties\PublishConfiguration.xml</PublishConfigurationPath>
        <!-- CSII 工具链路径：优先用户环境变量，回退到默认缓存路径 -->
        <CsiiToolPath>$([System.Environment]::GetEnvironmentVariable('CSII_TOOLPATH', 'EnvironmentVariableTarget.User'))</CsiiToolPath>
    </PropertyGroup>

    <!-- 导入 CSII Mod 构建标准 -->
    <Import Project="$(CsiiToolPath)\Mod.props"/>
    <Import Project="$(CsiiToolPath)\Mod.targets"/>

    <!-- 引用游戏程序集，全部设为 Private=false（不复制到输出） -->
    <ItemGroup>
        <Reference Include="Game"><Private>false</Private></Reference>
        <Reference Include="Colossal.Core"><Private>false</Private></Reference>
        <Reference Include="Colossal.UI.Binding"><Private>false</Private></Reference>
        <Reference Include="Unity.Entities"><Private>false</Private></Reference>
        <!-- ... -->
    </ItemGroup>

    <!-- 构建后自动运行 npm build -->
    <Target Name="BuildUI" AfterTargets="AfterBuild">
        <Exec Command="npm run build" WorkingDirectory="$(ProjectDir)/UI" />
    </Target>
</Project>
```

**【CS2 特有】关键点**：
- 引用游戏程序集时必须设 `Private=false`，因为游戏运行时已加载这些程序集
- `Import Mod.props/Mod.targets` 提供自动部署到游戏 Mod 目录的功能
- `AfterBuild` target 将 npm 构建嵌入 MSBuild 流程

**9.5 webpack 配置关键点**：

```javascript
// 输出到游戏 Mods 目录（由 CSII_USERDATAPATH 环境变量决定）
const OUTPUT_DIR = `${CSII_USERDATAPATH}\\Mods\\${MOD.id}`;

module.exports = {
    mode: "production",
    entry: { [MOD.id]: "./src/index.tsx" },
    externalsType: "window",
    externals: {
        react: "React",               // 游戏已提供 React
        "react-dom": "ReactDOM",      // 游戏已提供 ReactDOM
        "cs2/modding": "cs2/modding",  // 游戏已提供 cs2/* 模块
        "cs2/api": "cs2/api",
        "cs2/l10n": "cs2/l10n",
        "cs2/ui": "cs2/ui",
    },
    output: {
        library: { type: "module" },  // ES 模块输出
        publicPath: "coui://ui-mods/",
    },
};
```

**【CS2 特有】** `externals` 配置将 React、cs2/*、cohtml 等标记为外部依赖，因为这些库在游戏运行时已由 Cohtml UI 引擎提供，不需要打包进 mod。

### 通用化后的开发指导

**推荐新项目目录结构**：

```
MyMod/
├── MyMod.sln
├── MyMod.csproj
├── .gitignore
│
├── code/
│   ├── MyMod/
│   │   ├── Mod.cs
│   │   ├── Logger.cs
│   │   ├── [Tool]System.cs
│   │   ├── [UI]BridgeSystem.cs
│   │   └── [Logic]System.cs
│
├── UI/
│   ├── mod.json
│   ├── package.json
│   ├── tsconfig.json
│   ├── webpack.config.js
│   ├── src/
│   │   ├── index.tsx
│   │   ├── bindings.ts
│   │   └── [Components].tsx
│   └── types/
│
├── lang/
│   └── en-US.json
│
└── Properties/
    ├── PublishConfiguration.xml
    └── Thumbnail.jpg
```

**命名空间建议**：使用 `YourModName` 或 `YourModName.Module` 模式，避免与其它 Mod 冲突。

---

## 10. 构建、加载、调试与发布

### 从当前项目提取到的经验

**10.1 构建流程**：

```
1. MSBuild 编译 C# → bin/ 目录
2. CSII Mod.targets 自动部署 C# DLL 到游戏 Mods 目录
3. AfterBuild target 触发 npm run build → webpack 打包 UI
4. Webpack 输出 .mjs + .css 到 %CSII_USERDATAPATH%/Mods/Transit Scope/
```

**关键环境变量**（【CS2 特有】）：

| 变量 | 用途 | 示例值 |
|------|------|--------|
| `CSII_TOOLPATH` | Mod 工具链路径 | `%USERPROFILE%/AppData/LocalLow/Colossal Order/Cities Skylines II/.cache/Modding` |
| `CSII_USERDATAPATH` | 游戏用户数据路径 | `%USERPROFILE%/AppData/LocalLow/Colossal Order/Cities Skylines II` |
| `CSII_INSTALLATIONPATH` | 游戏安装路径 | `C:/Program Files (x86)/Steam/steamapps/common/Cities Skylines II` |
| `CSII_MANAGEDPATH` | 托管程序集路径 | `{InstallPath}/Cities2_Data/Managed` |

**10.2 Mod 元数据**：

```json
// UI/mod.json — 运行时元数据
{
  "id": "Transit Scope",
  "author": "Vaelric",
  "version": "1.0.0",
  "dependencies": []
}
```

```xml
<!-- Properties/PublishConfiguration.xml — 发布元数据 -->
<Publish>
    <ModId Value="139959" />
    <ModVersion Value="1.5" />
    <GameVersion Value="1.5.*" />
    <Dependency Id="75426" DisplayName="I18n EveryWhere" />
</Publish>
```

**【CS2 特有】** `mod.json` 的 `id` 必须与游戏 Mods 目录下的文件夹名精确匹配。`PublishConfiguration.xml` 的 `ModId` 是 PDX Mods 平台分配的唯一数字 ID。

**10.3 日志查看**：

日志输出到游戏的 `Player.log`：
```
%CSII_USERDATAPATH%/Player.log
```

或通过 PDX Mods 平台的 Mod 详情页查看用户提交的日志。

**10.4 发布配置**：

```xml
<Publish>
    <ModId Value="139959" />
    <DisplayName Value="Transit Scope" />
    <ShortDescription Value="..." />
    <LongDescription>...</LongDescription>
    <Thumbnail Value="Properties/Thumbnail.jpg" />
    <Screenshot Value="Properties/1.jpg" />
    <ModVersion Value="1.5" />
    <GameVersion Value="1.5.*" />
    <Dependency Id="75426" DisplayName="I18n EveryWhere" />
    <ForumLink Value="https://forum.paradoxplaza.com/forum/threads/..." />
    <ExternalLink Type="github" Url="https://github.com/Dnikolas991/Traffic-Scope" />
    <AccessLevel Value="Public" />
    <ChangeLog Value="V1.4 Vanilla-like building hover effects, bug fixes" />
</Publish>
```

### 通用化后的开发指导

**开发调试流程**：

1. 确保安装 CSII Modding Toolchain（游戏启动器 → Mods → Modding Toolchain）
2. 确保环境变量 `CSII_TOOLPATH`、`CSII_USERDATAPATH` 正确设置
3. Visual Studio / Rider 中 Ctrl+Shift+B 构建
4. C# DLL 自动部署到 Mods 目录
5. npm build 打包 UI 到同一目录
6. 启动游戏，在 Mod 列表中找到并启用
7. 查看 `Player.log` 确认无报错

**常见加载失败原因**：

| 问题 | 可能原因 |
|------|---------|
| Mod 不在列表中 | `mod.json` id 与目录名不匹配 |
| Mod 标记为"不兼容" | `GameVersion` 不匹配当前游戏版本 |
| Mod 崩溃 | DLL 中引用了不存在的程序集或 API |
| UI 不显示 | webpack 输出路径错误或 npm build 失败 |
| 本地化不工作 | I18n EveryWhere 未安装或 JSON 格式错误 |

---

## 11. 可复用开发模板

### 【可迁移模板】Mod 入口类

```csharp
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace YourMod
{
    public class Mod : IMod
    {
        public void OnLoad(UpdateSystem updateSystem)
        {
            // 1. 初始化日志
            Logger.Info("YourMod 启动");

            // 2. 注册系统（按阶段顺序）
            updateSystem.UpdateAt<YourToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<YourUIBridgeSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<YourLogicSystem>(SystemUpdatePhase.UIUpdate);

            // 3. 确认加载成功
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Logger.Info($"Mod 已加载到: {asset.path}");
            }
        }

        public void OnDispose()
        {
            Logger.Info("YourMod 关闭");
        }
    }
}
```

### 【可迁移模板】Logger 封装

```csharp
using System;
using Colossal.Logging;

namespace YourMod
{
    public static class Logger
    {
        private static readonly ILog _log = LogManager.GetLogger(nameof(YourMod));

        public static void Info(string message)  => SafeLog(log => log.Info(message));
        public static void Warn(string message)  => SafeLog(log => log.Warn(message));
        public static void Error(string message) => SafeLog(log => log.Error(message));

        private static void SafeLog(Action<ILog> write)
        {
            if (write == null || _log == null) return;
            try { write(_log); }
            catch { /* 日志绝不能中断模拟更新循环 */ }
        }
    }
}
```

### 【可迁移模板】自定义工具系统骨架

```csharp
using Game;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;

namespace YourMod
{
    public partial class YourToolSystem : ToolBaseSystem
    {
        // 1. Tool ID（全局唯一）
        public override string toolID => "YourMod_YourTool";

        // 2. 依赖引用
        private ToolSystem m_GameToolSystem;

        // 3. 暴露给其他系统的状态属性
        public Entity SelectedEntity { get; private set; } = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_GameToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            Enabled = false; // 默认不激活
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            // 初始化工具状态
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            // 清理工具状态
        }

        // 4. Raycast 配置
        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
            m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.StaticObjects;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.SubElements;
            m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.TrainTrack;
        }

        // 5. 每帧更新
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_FocusChanged) return inputDeps;
            if (m_GameToolSystem == null || m_GameToolSystem.activeTool != this)
                return inputDeps;

            // 处理 raycast + 输入
            if (GetRaycastResult(out Entity hitEntity, out _))
            {
                // 处理命中逻辑
            }

            return inputDeps;
        }

        // 6. 激活/停用方法（供 UI 调用）
        public void Enable()
        {
            if (m_GameToolSystem.activeTool != this)
            {
                m_GameToolSystem.selected = Entity.Null;
                m_GameToolSystem.activeTool = this;
            }
        }

        public void Disable()
        {
            if (m_GameToolSystem.activeTool == this)
            {
                m_GameToolSystem.selected = Entity.Null;
                m_GameToolSystem.activeTool = m_DefaultToolSystem;
            }
        }

        // 7. 必须覆盖的抽象方法（本项目不涉及 Prefab 选择，返回 false/null）
        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;
    }
}
```

### 【可迁移模板】UIBridgeSystem 骨架

```csharp
using Colossal.UI.Binding;
using Game.UI;

namespace YourMod
{
    public partial class YourUIBridgeSystem : UISystemBase
    {
        private ValueBinding<bool> m_ActiveBinding;
        private YourToolSystem m_ToolSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<YourToolSystem>();

            // ValueBinding：C# → UI
            AddBinding(m_ActiveBinding = new ValueBinding<bool>("yourMod", "isActive", false));

            // TriggerBinding：UI → C#
            AddBinding(new TriggerBinding<bool>("yourMod", "toggle", OnToggle));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            m_ActiveBinding.Update(m_ToolSystem.IsSelecting); // 同步状态
        }

        private void OnToggle(bool active)
        {
            if (active) m_ToolSystem.Enable();
            else m_ToolSystem.Disable();
        }
    }
}
```

### 【可迁移模板】UI 入口

```tsx
// UI/src/index.tsx
import { ModRegistrar } from "cs2/modding";
import { YourButton } from "./YourButton";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append("GameTopLeft", YourButton);
};

export default register;
```

### 【可迁移模板】UI 绑定声明

```tsx
// UI/src/bindings.ts
import { bindValue, trigger } from "cs2/api";

export const isActiveBinding = bindValue<boolean>("yourMod", "isActive");
export const toggleYourMod = (active: boolean) => trigger("yourMod", "toggle", active);
```

### 【可迁移模板】webpack.config.js

```javascript
const path = require("path");
const MOD = require("./mod.json");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
const TerserPlugin = require("terser-webpack-plugin");

const CSII_USERDATAPATH = process.env.CSII_USERDATAPATH;
if (!CSII_USERDATAPATH) {
  throw "CSII_USERDATAPATH environment variable is not set";
}

const OUTPUT_DIR = `${CSII_USERDATAPATH}\\Mods\\${MOD.id}`;

module.exports = {
  mode: "production",
  entry: { [MOD.id]: "./src/index.tsx" },
  externalsType: "window",
  externals: {
    react: "React",
    "react-dom": "ReactDOM",
    "cs2/modding": "cs2/modding",
    "cs2/api": "cs2/api",
    "cs2/bindings": "cs2/bindings",
    "cs2/l10n": "cs2/l10n",
    "cs2/ui": "cs2/ui",
    "cs2/input": "cs2/input",
  },
  module: {
    rules: [
      { test: /\.tsx?$/, use: "ts-loader", exclude: /node_modules/ },
      {
        test: /\.s?css$/,
        use: [MiniCssExtractPlugin.loader, "css-loader", "sass-loader"],
      },
    ],
  },
  resolve: { extensions: [".tsx", ".ts", ".js"] },
  output: {
    path: path.resolve(__dirname, OUTPUT_DIR),
    library: { type: "module" },
    publicPath: "coui://ui-mods/",
  },
  experiments: { outputModule: true },
  plugins: [new MiniCssExtractPlugin()],
};
```

---

## 12. 给其他 AI 的实现指导

### 从零创建 Cities: Skylines II Mod 的标准流程

**步骤 1：创建项目骨架**

1. 使用 Visual Studio 或 Rider 新建 .NET Framework 4.8 类库项目
2. 安装 CSII Modding Toolchain
3. 设置 `CSII_TOOLPATH`、`CSII_USERDATAPATH` 环境变量
4. 修改 `.csproj`：Import Mod.props/Mod.targets，引用 Game/Colossal.*/Unity.* 程序集
5. 在项目根目录创建 `UI/` 文件夹，使用 `npm init` + `npm install react cs2/*` 初始化前端

**步骤 2：创建 Mod 入口类**

- 实现 `IMod` 接口
- 在 `OnLoad` 中注册系统
- 创建 Logger 封装 `Colossal.Logging`

**步骤 3：确定是否需要自定义工具**

- 如果需要场景交互（鼠标点击/拖拽选择游戏对象）→ 继承 `ToolBaseSystem`
- 如果只需 UI 面板 → 跳过此步骤

**步骤 4：创建 UIBridge**

- 继承 `UISystemBase`
- 在 `OnCreate` 中用 `AddBinding()` 注册 ValueBinding 和 TriggerBinding
- 在 `OnUpdate` 中同步状态

**步骤 5：创建 UI 前端**

- 创建 `index.tsx` 导出 `ModRegistrar`
- 创建 `bindings.ts` 声明绑定
- 创建 React 组件，使用 `useValue()` 和 `trigger()`
- 配置 webpack 构建

**步骤 6：实现业务逻辑**

- 继承 `GameSystemBase` 创建逻辑系统
- 使用 `World.GetOrCreateSystemManaged<T>()` 获取其他系统引用
- 使用 `EntityManager` + `EntityQuery` 查询和修改游戏实体

**步骤 7：添加本地化**

- 创建 `lang/en-US.json`
- 在发布配置中声明 `I18n EveryWhere` 依赖
- 或在 C# 端自行加载翻译文件

**步骤 8：配置发布**

- 编辑 `Properties/PublishConfiguration.xml`
- 设置 ModId、版本号、游戏版本、依赖、描述
- 添加截图和缩略图

**步骤 9：测试**

- 构建项目 → C# DLL + UI bundle 自动部署到 Mods 目录
- 启动游戏，在 Mod 列表启用
- 检查 `Player.log` 确认无错误

---

## 13. 注意事项与容易踩坑的地方

### 【CS2 特有】常见陷阱

1. **toolID 冲突**：多个 Mod 的 `toolID` 如果相同，后加载的会覆盖先加载的。务必使用 Mod 名前缀。

2. **程序集引用必须设 Private=false**：所有游戏程序集引用（Game、Colossal.*、Unity.*）必须设置为不复制到输出目录，否则会与游戏自带的程序集冲突。

3. **mod.json id 与目录名不一致**：游戏的 Mod 加载器使用 `mod.json` 的 `id` 字段匹配 Mods 目录下的文件夹名，不一致会导致 Mod 无法加载。

4. **webpack externals 配置**：必须将 `react`、`react-dom`、`cs2/*`、`cohtml/*` 标记为外部依赖——这些由游戏 Cohtml UI 引擎在运行时提供。

5. **数据序列化问题**：System.Text.Json 和 Newtonsoft.Json 在 CS2 的 .NET Framework + Unity 环境下可能存在兼容性问题。推荐使用 `DataContractJsonSerializer`（System.Runtime.Serialization.Json）。

6. **World.GetOrCreateSystemManaged 时机**：必须在 `OnCreate()` 中调用，不能在构造函数或 `OnLoad` 之前调用。

7. **ECS 组件修改后需要添加 Updated**：修改 Entity 的组件数据后，如果不添加 `Updated` 组件，下游系统无法感知变更。

8. **Owner/Attached 链深度**：Entity 关系图可能很深（子建筑 → 父建筑 → 区域 → ...），遍历时必须设置最大深度限制。

9. **Temp 实体处理**：射线命中可能是 `Temp` 组件标记的临时实体，需要通过 `Temp.m_Original` 获取真正的游戏实体。

10. **Overlay 渲染需要 Complete JobHandle**：调用 `OverlayRenderSystem.GetBuffer()` 返回的 `JobHandle` 必须 `Complete()` 后才能写入绘制命令。

### 【当前项目特例】已知局限

- 本项目未实现设置页面，所有表现行为是硬编码的（颜色、尺寸、刷新间隔等）
- 建筑高亮效果"不完全"——当前实现使用 `Highlighted` 组件，不支持完整的原版建筑 hover 效果
- 选择大型多部件建筑时可能显示"无流量"——这是 Entity 解析链覆盖不全导致的已知问题
- 依赖 `I18n EveryWhere` 外部 Mod 提供本地化，未内建本地化系统

### 【C# 通用】开发建议

- 日志包装必须使用 try-catch，因为 Colossal.Logging 内部可能抛出异常
- 避免使用 `var` 关键字的过度省略——在 ECS 代码中明确类型有助于理解数据流
- 使用 `internal` 而非 `public` 修饰不对外暴露的类
- 系统之间的通信优先通过 public 属性/方法，避免静态状态

### 【UI 前端相关】开发建议

- 优先使用 `cs2/ui` 导出的组件（Button、Portal 等），保持视觉风格一致
- 浮动面板必须使用 Portal 组件，否则会被父容器裁剪
- 不要在 UI 端持有业务状态——UI 只显示和触发，真状态在后端
- TypeScript 接口定义应与 C# DTO 的 `[DataMember(Name = "...")]` 名称完全对齐
