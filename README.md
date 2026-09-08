# 轻量后台连点器

一个仅依赖 Windows/.NET Framework 的后台多点连点器。它通过窗口消息把点击发送给指定窗口，不移动真实鼠标，也不要求目标窗口保持前台。

## 功能

- 只向选定窗口发送点击
- 目标窗口最小化后继续发送
- 多个点击点按各自间隔独立触发
- 支持左键、右键、中键
- 按住“拖到目标位置”按钮即可抓取客户区坐标
- 单点测试
- 全局 `F6` 开始/停止
- 自动保存窗口偏好和点击点
- 无第三方依赖，空闲时不进行忙轮询

## 构建与运行

在 PowerShell 中执行：

```powershell
.\build.ps1
.\bin\Release\AUTO CLICKER by 一叶丶知秋.exe
```

后台点击集成测试：

```powershell
.\test.ps1
```

测试会创建一个本地最小化窗口，并验证点击消息能到达其窗口消息队列。

也可以使用 Visual Studio 打开 `AutoClicker.csproj`，以 Release 配置构建。

## 使用

1. 打开目标程序，并让需要点击的页面可见。
2. 启动连点器，选择目标窗口。
3. 按住“◎ 按住拖到目标位置”，拖到目标位置后松开。
4. 设置每个点的鼠标键和间隔（最小 10ms）。
5. 先用“测试选中点”验证，再点击“开始”或按 `F6`。
6. 此后可将目标窗口最小化；坐标始终相对目标窗口客户区。

## 技术边界

后台模式依赖标准 Win32 鼠标消息，并始终发送给所选窗口的客户区。多数传统桌面程序可处理这类消息；使用 Raw Input、DirectInput、独立渲染输入层或主动忽略后台消息的程序，可能不会响应。低权限进程也可能无法向以管理员身份运行的目标发送消息，此时让两者使用相同权限级别。

部分应用在最小化后会暂停自身业务逻辑。连点器仍会发送消息，但是否执行操作取决于目标程序本身。

## MuMu Android emulator

When the selected target is a `MuMuNxDevice` window, the application automatically switches to MuMu's local ADB touch channel. Captured window coordinates are scaled to the active Android display, so taps continue reaching Android after the emulator window is minimized. MuMu must be running, and the game should be visible while points are captured.
