# 模型与第三方组件许可

发布版包含三个 GGUF 模型文件。模型权重的上游项目均以 Apache License 2.0
发布，GGUF 文件仅用于本地推理：

| 发布文件 | 上游模型 | 许可 |
| --- | --- | --- |
| `MiniCPM5-1B-F16-*.gguf` | [openbmb/MiniCPM5-1B](https://huggingface.co/openbmb/MiniCPM5-1B) | Apache-2.0 |
| `Qwen3-1.7B-Q8_0.gguf` | [Qwen/Qwen3-1.7B](https://huggingface.co/Qwen/Qwen3-1.7B) | Apache-2.0 |
| `Qwen3-4B-Q4_K_M-*.gguf` | [Qwen/Qwen3-4B](https://huggingface.co/Qwen/Qwen3-4B) | Apache-2.0 |

发布版还包含：

- [llama.cpp](https://github.com/ggml-org/llama.cpp)，MIT License。
- [ECDICT](https://github.com/skywind3000/ECDICT) 的精简离线词典数据，
  MIT License；完整许可文本随程序放在 `licenses/ECDICT-MIT.txt`。
- NVIDIA CUDA 运行时可再发行组件；其使用受
  [NVIDIA CUDA Toolkit EULA](https://docs.nvidia.com/cuda/eula/) 约束。

模型可能生成不准确、有偏差或不安全的内容。请在高风险场景中核验输出，并遵守
适用法律、模型许可和第三方组件许可。
