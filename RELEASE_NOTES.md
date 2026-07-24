# TranslationByLocalAI v1.0.1

本版本修复了划词检测可能干扰系统剪贴板的问题，并保留 v1.0.0 的即时输入翻译、全局划词翻译和本地 llama.cpp 推理能力。

## 修复内容

- 划选文字时只检测手势并显示翻译按钮，不再立即读取、清空或改写剪贴板。
- 只有点击翻译按钮后才复制当前选区并开始翻译。
- 点击翻译后，选中文字保留在剪贴板中，不再恢复成旧内容。
- 移除不再需要的 UI Automation 与 WPF 程序集依赖。
- 更新剪贴板行为和隐私说明。

## 下载与升级

1. 下载 `TranslationByLocalAI-v1.0.1-win-x64-cuda.zip`。
2. 退出旧版程序后，将新版解压到新目录。
3. 模型没有变化，可以继续使用 v1.0.0 已下载的 `Models` 文件夹。
4. 首次安装的用户可从 v1.0.0 Release 下载以下模型附件：
   - MiniCPM5-1B F16：下载 `00001` 和 `00002` 两个分片。
   - Qwen3-1.7B Q8_0：下载单个 GGUF 文件。
   - Qwen3-4B Q4_K_M：下载 `00001` 和 `00002` 两个分片。
5. 将模型放入新版的 `Models` 文件夹；分片模型在设置中选择 `00001` 文件。

模型下载页：
https://github.com/Leonard-china/TranslationByLocalAI/releases/tag/v1.0.0

## 系统要求

- Windows 10/11 x64
- .NET Framework 4.8
- 推荐支持 CUDA 12 的 NVIDIA 显卡
- 至少 4 GB 可用内存；Qwen3-4B 建议预留更多

模型和第三方组件许可见 `MODEL-LICENSES.md`。重要翻译内容请人工核验。
