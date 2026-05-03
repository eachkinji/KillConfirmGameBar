# KillConfirmGameBar 中文说明

## 路线图

目前已经实现：

- 击杀语音。
- 击杀图标和击杀动画显示。

后续计划：

- 实现单纯由代码驱动的击杀特效，用来适配各种击杀图标。目前仍然是动画帧形式。
- 适配更多角色语音。
- 实现更完整的设置功能，比如更精细地调节动画帧数等。
- 实现更多对局内可用语音，比如 C4、回合开始语音、回合结束语音等。

## 注意事项

- 插件会监听本地端口来接收 CS2 Game State Integration 事件。开启系统代理可能会导致本地服务收不到事件，表现为组件已启动，但进游戏击杀后没有语音和图标。
- 如果使用类似 Clash 的系统代理工具，游玩前建议关闭系统代理。视频简介和安装说明里也建议提醒这一点，方便用户排查“击杀没反应”的问题。

[English](README.md) | [简体中文](README.zh-CN.md)

KillConfirmGameBar 是一个用于 Counter-Strike 2 的击杀确认 Xbox Game Bar 悬浮窗。

它通过 CS2 Game State Integration 接收击杀事件，然后播放语音并显示击杀动画。悬浮窗运行在 Xbox Game Bar 里，游戏中可以用 `Win + G` 打开。

## 功能

- CS2 击杀确认音效。
- Xbox Game Bar 悬浮窗。
- 支持普通击杀、爆头、刀杀、首杀、最后一杀等动画效果。
- 可以在控制面板里切换语音包。
- 支持小范围分发用的 Windows 安装包。

当前语音包：

- `swat GR / swat BL`
- `tiger GR / tiger BL`
- `cfsex`
- `women GR / women BL`

## 使用要求

- Windows 10/11
- 已启用 Xbox Game Bar
- Counter-Strike 2

如果你要从源码构建，还需要：

- Rust 工具链
- Visual Studio 或 Visual Studio Build Tools，并安装 Windows/UWP/MSIX 相关工具
- Inno Setup 6，仅在需要构建 `.exe` 安装器时使用

## 安装

普通用户请下载 release 包，然后运行里面的安装器或安装脚本。

安装后：

1. 按 `Win + G` 打开 Xbox Game Bar。
2. 打开 Kill Confirm Overlay 小组件。
3. 启动 CS2。
4. 安装器会尝试自动配置 CS2 Game State Integration。

这个项目会使用一个本地 companion service。安装器会帮你安装应用包，并设置 Xbox Game Bar 小组件需要的本地连接。

## CS2 Game State Integration

CS2 需要一个 GSI 配置，地址指向：

```text
http://127.0.0.1:3000/
```

安装器会尝试自动创建这个配置。如果击杀事件没有触发，可以手动把 `KillConfirmService/gsi/gamestate_integration_killconfirm.cfg` 放到 CS2 的 cfg 目录：

```text
C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\
```

上游参考 GSI 配置在这里：

```text
https://github.com/st0nie/gsi-cs2-rs/blob/main/gsi_cfg/gamestate_integration_fast.cfg
```

## 从源码构建

在仓库根目录运行：

```powershell
.\Build-IntegratedPackage.ps1
```

创建可转移安装包：

```powershell
.\Build-TransferPackage.ps1
```

创建可选的安装器：

```powershell
.\Build-Installer.ps1
```

## 项目结构

- `KillConfirmService`：Rust 本地服务，负责接收 CS2 GSI 和播放音频。
- `Widget`：Xbox Game Bar 小组件。
- `Package`：Windows 打包项目。
- `Installer`：安装器相关文件。
- `SourceAssets`：源动画、音频、图标和语音包。

构建时会从 `SourceAssets` 刷新生成最终打包用的资源文件。

## 说明

- 应用只和本机 `127.0.0.1` 上的本地服务通信。
- 每个语音包里的 `sound.lua` 控制语音播放逻辑。
- 请只安装你信任来源的语音包。
- 测试签名文件只用于开发构建。正式发布建议使用正式证书或可信签名服务。

## 致谢

Rust 服务基于 st0nie 的开源项目 `cskillconfirm`：

```text
https://github.com/st0nie/cskillconfirm
```

本项目也使用了 `gsi-cs2-rs`：

```text
https://github.com/st0nie/gsi-cs2-rs
```

## 许可证

本项目使用 GNU Affero General Public License v3.0。详见 `LICENSE`。

本项目与 Valve、Microsoft、Xbox、CrossFire、Valorant、Battlefield 或其他游戏发行商无官方关联。
