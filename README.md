[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

# Audio Segment Matcher & Cutter

一个用于整理和清理有声小说音频文件的 Windows 桌面工具。

用户可以从一个音频文件中选出一段声音作为 Sample，然后在指定目录中搜索这段声音，并根据匹配结果批量生成清理后的新文件。原始音频始终不会被修改。

---

## 项目简介

很多有声小说在长期收集后会出现这些问题：

- 章节数量很多，手工处理成本高
- 片头过长，或片头位置不固定
- 片尾过长，或片尾位置不固定
- 章节中间插入重复广告
- 同一段声音可能在很多文件中反复出现

本工具的目标不是判断两个完整音频文件是否相同，而是：

> 在较长的音频文件中，查找用户选出的那段 Sample。

当前版本已经可以完成「选择 Sample → 批量搜索 → 预览结果 → 生成新文件」这条主流程。匹配结果仍需要用户确认后再处理，以避免误删正文。

---

## 功能

### 已实现 ✅

- ✅ 选择源音频并播放、暂停、停止、跳转
- ✅ 显示波形，并支持缩放、平移和横向滚动条
- ✅ 用鼠标拖选或精确输入 Start / End 来设置 Sample
- ✅ 扫描指定目录中的音频文件，可包含子目录
- ✅ 在多个文件中搜索 Sample，并显示匹配位置、置信度和多个匹配
- ✅ 删除一个文件中的全部匹配片段（Remove All Matches）
- ✅ 从第一个匹配的结束点保留到文件末尾（Cut Before First Match，从头到 End 被裁掉）
- ✅ 从文件开头保留到第一个匹配的起点（Cut After First Match，从 Start 起至文件末尾被裁掉）
- ✅ 裁剪时复制原音频流，不重编码，保持原码率和音质
- ✅ 搜索与处理默认 10 线程并行
- ✅ 将处理结果写到独立输出目录，不修改、不覆盖原始文件
- ✅ 输出文件已存在时默认跳过（Skip）
- ✅ 搜索和处理过程显示进度，支持取消
- ✅ 单个文件失败不会中断整批处理
- ✅ 处理日志写入程序目录下的 `logs` 文件夹，可从 Failed 入口打开
- ✅ 拖放音频文件或文件夹
- ✅ 常用快捷键（空格播放/暂停，I/O 设置起止点；左右键微调选区右边界，Shift+左右键微调左边界）

当前支持的音频扩展名：`.mp3`、`.wav`、`.flac`、`.m4a`、`.aac`、`.ogg`、`.wma`。实际能否解码取决于本机 FFmpeg。

### 计划中 📋

- 📋 结果列表中的波形预览
- 📋 点击匹配结果试听目标文件中的匹配片段
- 📋 保存常用匹配规则（Saved Rules）
- 📋 输出文件名冲突时的更多策略（替换、自动改名）
- 📋 更稳健的指纹匹配与缓存
- 📋 官方 Release 安装包
- 📋 命令行模式

---

## 使用场景

一本有声小说可能有几百个章节。如果每个章节开头都有同一段片头，或中间反复插入同一段广告，用户可以：

1. 打开其中一个章节
2. 在波形上选出这段片头或广告
3. 对整本书所在目录执行搜索
4. 检查匹配结果
5. 选择处理方式并生成新文件

当前版本已经支持上述流程。匹配算法对音量差异和轻微编码差异有一定容错，但**不能保证 100% 准确**。请先查看结果，再决定是否处理。

---

## 技术栈

- C#
- .NET 10
- WPF
- NAudio（播放）
- MathNet.Numerics（匹配计算）
- CommunityToolkit.Mvvm
- FFmpeg（解码、波形生成、音频导出）

---

## 系统要求

- Windows 10 / Windows 11
- .NET 10 SDK（从源码编译或 `dotnet run`）
- 本机已安装 FFmpeg（搜索和处理都需要）

当前没有正式 Release 安装包。从源码运行属于**方案 B + 方案 C**：需要安装 .NET 10，并且需要 FFmpeg。

播放部分可以在没有 FFmpeg 时尝试打开部分格式；**搜索和导出必须使用 FFmpeg**。

---

## 安装 / 运行

当前项目仍处于开发阶段，暂未提供正式 Release。

从源码运行：

```text
1. 安装 .NET 10 SDK
2. 准备 FFmpeg（见下一节）
3. 克隆本仓库
4. 在仓库根目录执行：

   dotnet restore AudioMatcher.slnx
   dotnet run --project AudioMatcher.App
```

编译：

```text
dotnet build AudioMatcher.slnx
```

测试：

```text
dotnet test AudioMatcher.slnx
```

---

## FFmpeg

程序使用 FFmpeg 解码音频、生成波形，并导出处理后的新文件。本项目不会内置音频编解码器。

### 获取 FFmpeg

请从 [FFmpeg 官网](https://ffmpeg.org/download.html) 获取适用于 Windows 的构建。FFmpeg 是独立的第三方软件，其许可证遵循 FFmpeg 自身的许可要求，不由本项目的 MIT 许可证覆盖。

至少需要 `ffmpeg.exe`。如果同目录有 `ffprobe.exe`，程序会优先用它读取音频信息。

### 程序查找顺序

启动时会按下面顺序查找 `ffmpeg.exe`：

1. 上次通过「Locate FFmpeg」保存的路径  
   `%LocalAppData%\Audio Segment Matcher\ffmpeg.path`
2. 程序目录
3. 程序目录 `\ffmpeg`
4. 程序目录 `\tools\ffmpeg`
5. 程序目录 `\tools`
6. 当前工作目录 `\tools\ffmpeg`
7. `C:\ffmpeg\bin`
8. `%ProgramFiles%\ffmpeg\bin`
9. `%LocalAppData%\Audio Segment Matcher\ffmpeg`
10. 系统 PATH

也可以在程序窗口右上角点击 **Locate FFmpeg**，手动选择 `ffmpeg.exe`。选中后路径会保存，下次启动仍可使用。

请不要把 FFmpeg 可执行文件提交到 Git 仓库。可以把 `ffmpeg.exe` 放到仓库的 `tools\ffmpeg\` 下供本机使用，该目录已被忽略。

---

## 快速开始

```text
启动程序
    ↓
如未检测到 FFmpeg，点击 Locate FFmpeg
    ↓
选择一个源音频文件（或拖入文件）
    ↓
播放并在波形上选出 Sample 的 Start / End
    ↓
选择 Search Folder（或拖入文件夹）
    ↓
点击 Search
    ↓
检查匹配结果，勾选要处理的文件
    ↓
选择 Action：
    Remove All Matches
    Cut Before First Match
    Cut After First Match
    ↓
确认 Output Folder（默认是搜索目录下的 Processed）
    ↓
点击 Process Selected
    ↓
在输出目录查看新文件
```

处理规则：

- **Remove All Matches**：删除该文件中所有有效匹配
- **Cut Before First Match**：从最早匹配的 **End** 保留到文件末尾（从头到 End 被裁掉，含匹配段）
- **Cut After First Match**：从文件开头保留到最早匹配的 **Start**（Start 到文件末尾被裁掉，含匹配段）
- 输出使用 FFmpeg 流复制（`-c copy`），不改变原文件的编码、码率和音质。文件变短后体积应更小。
- 搜索与批量处理默认各使用 10 个并行线程。

原始文件只读。输出默认写入 `Processed` 子目录。若目标文件已存在，当前版本会跳过。

日志位于程序运行目录下的 `logs` 文件夹。如果处理出现失败，可点击状态栏的 **Failed** 打开该目录。

---

## 项目状态

本项目目前处于开发阶段。

- 主流程已经可以在真实有声小说目录上使用
- 匹配准确率仍依赖 Sample 质量和音频差异，短 Sample 更容易误匹配
- 导出时会重新编码，有损格式可能再损失一点音质
- 界面、参数和输出策略都可能继续调整
- 当前不承诺稳定 API 或稳定二进制发布

---

## Roadmap

- 结果列表波形与匹配片段试听
- 输出冲突策略（跳过 / 替换 / 自动改名）
- 保存常用 Sample 和处理规则
- 更稳健的音频指纹与缓存
- 提供 Windows Release 包
- 命令行批量模式

---

## 项目结构

```text
Audio Segment Matcher/
├── .github/
│   ├── ISSUE_TEMPLATE/
│   ├── pull_request_template.md
│   └── workflows/
│       └── build.yml
├── AudioMatcher.App/              # WPF 界面
├── AudioMatcher.Core/             # 匹配与处理规则
├── AudioMatcher.Infrastructure/   # FFmpeg 与日志
├── AudioMatcher.Tests/            # 单元测试
├── AudioMatcher.slnx
├── Directory.Build.props
├── NuGet.config
├── README.md
├── LICENSE
├── CONTRIBUTING.md
└── .gitignore
```

---

## 开源许可证

本项目使用 [MIT License](LICENSE)。

FFmpeg、NAudio 等第三方组件遵循各自的许可证。
