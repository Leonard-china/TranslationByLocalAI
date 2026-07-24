# TranslationByLocalAI v1.0.0

首个公开发布版：Windows 本地 AI 划词与即时输入翻译工具。

## 本版亮点

- 输入或粘贴文字后自动翻译，无需点击按钮。
- 支持全局划词翻译、桌面悬浮入口和 11 种目标语言。
- 本地 `llama.cpp` 推理，默认不向云端发送文本。
- 包含 CUDA 12 Windows x64 运行环境。
- 提供 MiniCPM5-1B、Qwen3-1.7B 和 Qwen3-4B 三个模型。

## 下载

必须下载：

1. `TranslationByLocalAI-v1.0.0-win-x64-cuda.zip`
2. 至少一个模型的全部 GGUF 文件
3. `SHA256SUMS.txt`（用于校验下载完整性）

三个模型：

- MiniCPM5-1B F16：下载 `00001` 和 `00002` 两个分片。
- Qwen3-1.7B Q8_0：下载单个 GGUF 文件。
- Qwen3-4B Q4_K_M：下载 `00001` 和 `00002` 两个分片。

将模型文件放入便携版的 `Models` 文件夹，然后运行
`TranslationByLocalAI.exe`。分片模型应在设置中选择 `00001` 文件。

## 系统要求

- Windows 10/11 x64
- .NET Framework 4.8
- 推荐支持 CUDA 12 的 NVIDIA 显卡
- 至少 4 GB 可用内存；Qwen3-4B 建议预留更多

模型和第三方组件许可见 `MODEL-LICENSES.md`。模型可能生成错误内容，重要信息
请人工核验。
