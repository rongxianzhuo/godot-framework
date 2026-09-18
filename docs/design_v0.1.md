# GameFramework v0.1 — 设计文档

**作者**：Johnni（Framework Engineer, MagicStudio）
**日期**：2026-09-18
**状态**：✅ **MVP 已实现，demo 跑通** — 见 [实现总结](#实现总结)
**目标版本**：Godot 4.5+ / .NET 8 / Godot.NET.Sdk 4.5+

---

## 0. MVP 范围

按 Founder 拍板，v0.1 只做两件事：

1. **单例容器**（替代裸 autoload + 散落的 `static Instance`）
2. **UI 框架**（push/pop 面板栈 + 类型化 open/close 参数）

**显式不做**（留给 v0.2+）：状态机、资源加载、事件总线、序列化、热更新、网络。

---

## 1. 候选方案对比（每个都列了 Godot 4 C# 下的优劣 + 实现复杂度）

### 1.1 单例容器候选

| 候选 | 优点 | 缺点 | 实现复杂度 |
|---|---|---|---|
| **(a) 纯 Godot autoload** | 零成本，引擎管生命周期，自动跨场景存活，`_Ready` 钩子天然适配 | 名字靠 `project.godot` 字符串拼写，无 type-safe 访问（必须 `GetNode<>` + cast）；一旦业务模块多了，autoload 列表变成"什么都往里塞"的垃圾抽屉；不是真正的"单实例"（仍可被手动 `new`） | ★☆☆☆☆ |
| **(b) 纯 C# DI 容器（反射式）** | 完全 type-safe，接口/实现解耦，支持 scoped lifetime、factory | 反射重，AOT 不友好（iOS/Android 受影响，Godot 4.5 移动端仍实验性）；和 Godot 的 `Node._Ready` 生命周期脱节；service 必须不是 Node 才能用 | ★★★★☆ |
| **(c) Node-based singleton（autoload + 类型化访问）** | 兼具 autoload 生命周期 + C# 类型安全；service 可以是 Node（享受 `_Ready`/`_Process`），也可以是 POCO | 两层间接（autoload 容器 + 注册的 services）；需要在 `_Ready` 手动注册 | ★★☆☆☆ |
| **(d) 混合：autoload + 轻量 typed registry** ✅ | 一个 autoload 容器 Node 装下所有 service；registry 用 `Dictionary<Type, object>` + 泛型约束；无反射 → AOT-friendly；类型安全；services 可以是 Node 也可以是 POCO | 比纯 autoload 多写 ~80 行 registry 代码 | ★★☆☆☆ |

### 1.2 UI 框架候选

| 候选 | 优点 | 缺点 | 实现复杂度 |
|---|---|---|---|
| **(a) Window stack（push/pop 嵌入式 sub-window）** | 用 Godot 4 新 `Window` 节点；可以原生 modal（`Exclusive + Transient`）；自带 `CloseRequested` 信号 | 嵌入式 sub-window 在 Godot 4 里有 quirks：Theme 不自动继承项目主题；input routing 行为微妙；和 CanvasItem 的 z-order 关系固定在 layer 1024；multi-window 模式启动时加载机不同 | ★★★☆☆ |
| **(b) 纯 modal queue** | 极简 — 一个 stack，只显示最顶层 dialog；保证 modal 语义 | **太窄**：游戏里 90% 的 UI 流程是非 modal 导航（Main → Settings → 子设置），纯 modal stack 表达不了 | ★★☆☆☆ |
| **(c) Control tree 切换（Visible + ProcessMode）** | 纯 Control，没有 sub-window 怪癖；`ProcessMode = WhenPaused` 自动给 pause menu；简单 | 所有面板常驻 scene tree（内存 + 启动开销）；focus 和 mouse leak 必须手动管（GDPanelFramework 就是为解决这个问题存在的）；z-order 不稳定（依赖绘制顺序） | ★★☆☆☆ |
| **(d) 混合：CanvasLayer + Panel stack + 类型化 args/result** ✅ | 所有面板作为 Control 子节点挂在专用 UI CanvasLayer 下（z-order 稳定）；stack 语义（push/pop）+ 类型化 open/close 参数（`UIPanel<TOpenArg, TCloseArg>`）；panel-scoped input binding（自动 sandbox 输入）；支持 cache（`TryReuse` / `ForceCreate`） | API 表面积最大（UIPanel/UIPanel<T>/UIPanel<T1,T2> + UIManager + IUIAnimation 等）；写起来要 ~10 个月文件 | ★★★☆☆ |

---

## 2. 最终选定方案（按 Founder 拍板）

### 2.1 单例容器 — **方案 (d) Hybrid**

**核心思想**：一个 autoload 的容器 Node + 内部的 typed registry + 两种 service 形态。

```
┌─ /root/GameFramework (autoload, Node) ──────────────┐
│                                                     │
│   static GameFramework Instance                    │
│                                                     │
│   ServiceRegistry Services { get; }                │
│     ├─ Dictionary<Type, object> _services         │
│     ├─ Register<T>(T service) where T : class     │
│     ├─ TryGet<T>(out T service)                   │
│     ├─ Resolve<T>()  (throws if not registered)   │
│     └─ Unregister<T>()                            │
│                                                     │
└─────────────────────────────────────────────────────┘
         ▲                                          │
         │ register()                               │
         │                                          │
   ┌─────┴────────┐  ┌──────────────┐  ┌────────────┐
   │ AudioService │  │ UIManager    │  │ GameState  │
   │ (POCO)       │  │ (Node-based) │  │ (POCO)     │
   │ manual reg   │  │ GameService  │  │ manual reg │
   └──────────────┘  └──────────────┘  └────────────┘
```

**关键设计决定**：

1. **单 autoload + 内部 registry**：避免 autoload 列表膨胀。每个模块都注册到 `GameFramework.Instance.Services`。
2. **两种 service 形态都支持**：
   - **Node-based service**：继承 `GameService`（我们的辅助基类），在 `_Ready` 自动注册，`_ExitTree` 自动注销。
   - **POCO service**：纯 C# 对象，手动 `Register()` / `Unregister()`。
3. **类型安全 via 泛型**：`Resolve<T>()` 编译期就知道返回类型，IDE 能补全。无 reflection → AOT-friendly。
4. **避免 GameManager-as-Autoload 反模式**（参考 zivadotsh 的提醒）：`GameFramework` 自身只管注册表 + UI 管理；具体业务模块（时间、存档、设置、状态）各自注册。

### 2.2 UI 框架 — **方案 (d) Hybrid：CanvasLayer Panel Stack**

**核心思想**：所有 UI 是 Control 节点，挂在专用 UI CanvasLayer 下的 `PanelStackContainer`，用 stack 语义管理；类型化 open/close 参数。

**三个 UIPanel 变体**（按 Founder 拍板，**不要 `Empty` 占位**）：

| 变体 | 用途 | PushAsync 返回 |
|---|---|---|
| `UIPanel` | 无 arg 无 result | `Task` |
| `UIPanel<TOpenArg>` | 仅 open arg | `Task` |
| `UIPanel<TOpenArg, TCloseArg>` | open arg + close result | `Task<TCloseArg>` |

**Panel Stack 行为**：

| 操作 | 行为 |
|---|---|
| `PushAsync<TPanel>(arg)` | 实例化面板 → 加到 PanelContainer → 焦点 → 调 `_OnPanelOpen(arg)` → 返回 Task |
| `ClosePanel(result)` (面板内调用) | 调 `_OnPanelClose(result)` → 释放 → 焦点回上一个 |
| `PopAll()` | 清空 stack |

---

## 3. 关键 API

### 3.1 `GameFramework`（autoload）

```csharp
public partial class GameFramework : Node
{
    public static GameFramework Instance { get; private set; }
    public ServiceRegistry Services { get; } = new();
}

public class ServiceRegistry
{
    public void Register<T>(T service) where T : class;
    public T? TryGet<T>() where T : class;
    public T Resolve<T>() where T : class;  // throws if missing
    public bool Unregister<T>() where T : class;
}

public abstract partial class GameService : Node  // 自动注册基类
{
    // _Ready → Services.Register(this)
    // _ExitTree → Services.Unregister(GetType())
}
```

### 3.2 `UIManager`（autoload, Node）

```csharp
public partial class UIManager : GameService
{
    public static UIManager Instance { get; private set; }
    public const int UiLayer = 100;  // CanvasLayer 高度

    // 三种 Push 重载（按 panel 类型选择）：
    public Task PushAsync<TPanel>() where TPanel : UIPanel, new();
    public Task PushAsync<TPanel, TOpenArg>(TOpenArg arg) where TPanel : UIPanel<TOpenArg>, new();
    public Task<TCloseArg> PushAsync<TPanel, TOpenArg, TCloseArg>(TOpenArg arg)
        where TPanel : UIPanel<TOpenArg, TCloseArg>, new();

    // Pop（一般由面板内部 ClosePanel 自动调用）
    public void Pop();
    public void Pop<TCloseArg>(TCloseArg result);
    public void PopAll();
}
```

### 3.3 `UIPanel` 体系

```csharp
public abstract partial class UIPanel : Control { /* 无 arg 无 result */ }
public abstract partial class UIPanel<TOpenArg> : Control { /* 仅 open arg */ }
public abstract partial class UIPanel<TOpenArg, TCloseArg> : Control { /* 两边都有 */ }

// 每个面板覆写：
protected virtual void OnOpen(TOpenArg arg);
protected virtual void OnClose(TCloseArg result);

// 关闭自己（typed result）：
protected void ClosePanel(TCloseArg result);
// 或无 result：
protected void ClosePanel();
```

**关键实现细节（实现中遇到的实际问题）**：

1. **`TaskCompletionSource` 必须 lazy 初始化**：不能用 `_Ready` 初始化，因为子类覆写 `_Ready` 时容易忘记调用 `base._Ready()`。改用 lazy `??= new()` 模式，按需创建。
2. **`Pop<TCloseArg>(result)` 调用顺序**：先 `Result = result` 再 `_tcs.TrySetResult(result)` 再 `Pop<TCloseArg>(result)`。这样 `PushAsync` 的返回路径能通过 `typedPanel.Result` 拿到结果。
3. **`_ExitTree` 不调 `OnClose`**：typed 面板的 `OnClose` 需要正确的 `TCloseArg` 默认值，强行调会触发 `InvalidCastException`。退出时直接清 stack 不通知。

---

## 4. Demo 场景结构

`/shared/godot-framework/scenes/demo.tscn`

```
DemoRoot (Node, script=DemoBootstrap)
```

由 `DemoBootstrap._Ready()` 驱动：

1. 注册 POCO `AudioService`
2. `PushAsync<MainMenuPanel, string, string>("initial")` — auto-click Settings → Push `SettingsPanel` (UIPanel<float>) → auto-click Back → auto-click Play → Bootstrap 收到 `'play'`
3. `PushAsync<ConfirmDialog, string, bool>("Start game?")` — auto-click Confirm → Bootstrap 收到 `true`
4. 干净退出

**三个演示面板**：

| 面板 | 类型 | 演示什么 |
|---|---|---|
| `MainMenuPanel` | `UIPanel<string, string>` | 带 close result 的主菜单 |
| `SettingsPanel` | `UIPanel<float>` | open arg 但无 result；通过 `Services.Resolve<AudioService>()` 跨服务调用 |
| `ConfirmDialog` | `UIPanel<string, bool>` | open arg + close result（确认/取消） |

---

## 5. 目录结构

```
/shared/godot-framework/
├── docs/
│   └── design_v0.1.md
├── scenes/
│   └── demo.tscn
├── scripts/
│   ├── framework/
│   │   ├── GameFramework.cs
│   │   ├── ServiceRegistry.cs
│   │   ├── GameService.cs
│   │   ├── UIManager.cs
│   │   ├── UIPanelBase.cs
│   │   ├── UIPanel.cs
│   │   ├── UIPanelT.cs
│   │   └── UIPanelT1T2.cs
│   └── demo/
│       ├── DemoBootstrap.cs
│       ├── MainMenuPanel.cs
│       ├── SettingsPanel.cs
│       ├── ConfirmDialog.cs
│       └── AudioService.cs
├── godot-framework.csproj
├── project.godot
├── icon.svg
└── .gitignore
```

---

## 6. .csproj

```xml
<Project Sdk="Godot.NET.Sdk/4.5.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <RootNamespace>GameFramework</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Godot.NET.Sdk" Version="4.5.0" />
  </ItemGroup>
</Project>
```

**关键发现**：`Godot.NET.Sdk` 必须**显式**作为 `PackageReference` 引用，否则 MSBuild 会"假成功"地 fallback 到默认 SDK，导致 Godot source generators 不跑，结果生成的 dll 里所有 `partial class X : Node` 都被编译成普通 C# 类，Godot 找不到 base type 就报"does not inherit from 'Node'"。

---

## 7. autoload 配置（`project.godot` 节选）

```ini
[application]
run/main_scene="res://scenes/demo.tscn"

[autoload]
GameFramework="*res://scripts/framework/GameFramework.cs"
UIManager="*res://scripts/framework/UIManager.cs"

[dotnet]
project/assembly_name="godot-framework"
```

**关键发现**：`project/assembly_name` 必须**匹配 .csproj 的 AssemblyName**（即 dll 文件名）。错配会让 Godot 找不到项目 assembly，所有 C# 脚本都"is not compiling"。

---

## 8. 运行

环境装好后：

```bash
# 安装 .NET 8 SDK
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir /opt/dotnet
apt-get install -y libicu-dev

# 下载 Godot 4.5 .NET 版
curl -sSL -o /tmp/godot.zip \
  "https://github.com/godotengine/godot-builds/releases/download/4.5-stable/Godot_v4.5-stable_mono_linux_x86_64.zip"
unzip -d /opt/godot /tmp/godot.zip
apt-get install -y libfontconfig1 unzip

# symlink 让 godot 命令可用
ln -sf /opt/godot/Godot_v4.5-stable_mono_linux_x86_64/Godot_v4.5-stable_mono_linux.x86_64 \
  /usr/local/bin/godot

# 构建 + 跑（headless smoke test）
cd /shared/godot-framework
dotnet build
godot --headless --quit-after 300
```

预期输出（demo 跑通的完整 trace）：

```
[GameFramework] _Ready. Service registry online.
[UIManager] _Ready. UI Root created (CanvasLayer @ layer 100).
[DemoBootstrap] _Ready. Waiting for GameFramework autoload...
[DemoBootstrap] Services registered: 1
[DemoBootstrap] Step 1: Push MainMenuPanel(arg='initial')
[MainMenuPanel] _Ready called. IsInsideTree: True
[MainMenuPanel] OnOpen(arg='initial')
[DemoBootstrap] Auto-click MainMenuPanel.SettingsButton (exercises SettingsPanel)
[MainMenuPanel] Settings clicked → push SettingsPanel
[SettingsPanel] OnOpen(arg='0')
[AudioService] Music volume set to 0.00
[DemoBootstrap] Auto-click SettingsPanel.Back (exercises UIPanel<float> pop)
[SettingsPanel] Back clicked → ClosePanel()
[DemoBootstrap] Auto-click MainMenuPanel.PlayButton
[MainMenuPanel] Play clicked → ClosePanel('play')
[MainMenuPanel] OnClose(result='play')
[DemoBootstrap] MainMenuPanel returned 'play'
[DemoBootstrap] Step 2: Push ConfirmDialog(message='Start game?')
[ConfirmDialog] OnOpen(message='Start game?')
[DemoBootstrap] Auto-click ConfirmDialog.Confirm
[ConfirmDialog] OnClose(result=True)
[DemoBootstrap] ConfirmDialog returned True
[DemoBootstrap] Demo complete. Quitting.
```

---

## 9. 实现总结（2026-09-18）

✅ **MVP 完成**。`dotnet build` 通过；`godot --headless --quit-after 300` 跑通完整 demo。

**实现的文件**（~900 行 C#）：

| 文件 | 行数 | 角色 |
|---|---|---|
| `GameFramework.cs` | 36 | autoload 容器 + ServiceRegistry host |
| `ServiceRegistry.cs` | 83 | Type-keyed 注册表 |
| `GameService.cs` | 38 | Node-based service 自动注册基类 |
| `UIManager.cs` | 222 | panel stack 管理 + UI root 创建 |
| `UIPanelBase.cs` | 69 | 内部基类 + input bindings |
| `UIPanel.cs` | 35 | 无 arg 无 result 变体 |
| `UIPanelT.cs` | 34 | 单 generic 变体 |
| `UIPanelT1T2.cs` | 47 | 双 generic 变体（带 typed close result） |
| Demo (5 files) | ~340 | MainMenu / Settings / Confirm / Bootstrap / AudioService |

**实现中踩的坑**（留给 v0.2+ 备忘）：

1. **Godot.NET.Sdk 必须显式 `PackageReference`** — 否则 MSBuild fallback 静默成功但 source generators 不跑。
2. **`project/assembly_name` 必须匹配 .csproj AssemblyName** — 否则 Godot 找不到项目 assembly，所有 C# 脚本"is not compiling"。
3. **`TaskCompletionSource` 用 lazy `??=` 初始化** — `_Ready` 初始化会被子类覆写时漏掉 base call。
4. **`_ExitTree` 不调 `OnClose`** — typed 面板需要正确的 `TCloseArg` 默认值，否则触发 `InvalidCastException`。
5. **每个 panel 类放独立文件** — Godot source generator 不允许多个同名 partial class 在同一个文件（GD0003）。
6. **autoload 顺序**：先 `GameFramework` 再 `UIManager`（UIManager 是 GameService，注册到 GameFramework）。

**演示场景验证的能力**：

- ✅ POCO service 注册（`AudioService`）
- ✅ Node-based service 自动注册（`UIManager : GameService`）
- ✅ 跨服务访问（SettingsPanel 读 AudioService）
- ✅ 三种 UIPanel 变体全部跑通
- ✅ Push with typed arg / Await typed result
- ✅ 嵌套 push（MainMenu → Settings → 关闭回到 MainMenu → Play 返回结果）
- ✅ 干净退出（无泄漏 exception，资源由 Godot 释放）

---

## 10. 不做的事（边界声明）

为了把 MVP 严格限制在"容器 + UI + 一个 demo"，以下功能**显式不做**：

- ❌ 状态机 / 流程控制 / procedure 切换（v0.2 考虑）
- ❌ 资源加载服务（`ResourceLoader` wrapper）/ 异步加载队列
- ❌ 事件总线（`Signal` 已经够用，先不抽象）
- ❌ 序列化 / 存档 / 配置中心
- ❌ 热更新 / Mod 加载
- ❌ 网络层
- ❌ Tweener / 动画系统（v0.2）
- ❌ Panel cache / reuse（v0.2 — MVP 每次 push 新建）
- ❌ Panel-scoped input binding（v0.2 — API 已留口子但未在 demo 演示）
- ❌ 自动 focus 恢复 / gamepad 友好性（v0.2）
- ❌ 完整 EditorPlugin（v0.2）
- ❌ 单元测试 / gdUnit 集成（v0.2）