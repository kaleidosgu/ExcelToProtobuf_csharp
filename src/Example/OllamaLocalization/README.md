# OllamaLocalization 使用说明

OllamaLocalization 是一个本地化 JSON 翻译工具。它会读取一个源语言 JSON 文件，调用本机 Ollama 的翻译模型生成多个目标语言文件，并自动维护翻译词汇表，避免重复内容反复请求模型。

## 使用前准备

请先确认本机 Ollama 服务已经启动，并且已经安装翻译模型：

```text
translategemma:12b
```

默认会请求：

```text
http://localhost:11434/api/generate
```

如果你的 Ollama 地址或模型名不同，可以在 `appsettings.json` 中修改。

## 运行方式

把需要翻译的源文件和配置文件放在工具目录下，例如：

```text
OllamaLocalization.exe
appsettings.json
localization.json
```

然后运行：

```text
OllamaLocalization.exe appsettings.json
```

如果不指定配置文件，默认会读取当前目录下的：

```text
appsettings.json
```

## 配置文件

主要配置在 `appsettings.json` 中。

### 源语言

```json
"SourceLanguage": "zh-CN"
```

表示原始 JSON 文件的语言。

### 目标语言

```json
"TargetLanguages": [
  "en-US",
  "ja-JP",
  "ko-KR"
]
```

表示需要生成哪些语言。

如果只想翻译英文，可以改成：

```json
"TargetLanguages": [
  "en-US"
]
```

这样不会删除已经生成过的其他语言文件，只会处理当前配置里的语言。

### 默认来源语言

```json
"DefaultTranslationSourceLanguage": "zh-CN"
```

表示如果某个目标语言没有单独指定来源语言，就默认从 `zh-CN` 翻译。

### 每个语言的来源语言

```json
"LanguageSourceLanguages": {
  "en-US": "zh-CN",
  "ja-JP": "zh-CN",
  "ko-KR": "zh-CN"
}
```

表示每个目标语言从哪个语言翻译。

例如想让日语基于英语翻译，可以写成：

```json
"LanguageSourceLanguages": {
  "en-US": "zh-CN",
  "ja-JP": "en-US",
  "ko-KR": "zh-CN"
}
```

工具会自动先生成 `en-US`，再用 `en-US` 作为来源生成 `ja-JP`。

## Ollama 参数

```json
"Ollama": {
  "Endpoint": "http://localhost:11434/api/generate",
  "Model": "translategemma:12b",
  "TimeoutSeconds": 300,
  "BatchSize": 16,
  "MaxRetries": 2,
  "NumPredict": 4096,
  "Temperature": 0.0,
  "TopP": 0.9
}
```

常用参数说明：

| 参数 | 说明 |
| --- | --- |
| `Endpoint` | Ollama API 地址 |
| `Model` | 使用的 Ollama 模型 |
| `TimeoutSeconds` | 单次请求超时时间 |
| `BatchSize` | 每次请求翻译多少条文本 |
| `MaxRetries` | 单个批次失败后的重试次数 |
| `NumPredict` | 模型单次最多生成多少 token |
| `Temperature` | 输出随机性，翻译建议使用 `0.0` |
| `TopP` | 采样参数，一般保持默认即可 |

`BatchSize` 的“条”指 JSON 中一个需要翻译的字符串值。

例如：

```json
{
  "title": "开始游戏",
  "button": "确定",
  "tips": [
    "金币不足",
    "请稍后再试"
  ]
}
```

这里一共有 4 条需要翻译的文本。

如果翻译时经常出现模型返回不完整 JSON、返回数量不一致等问题，可以优先把 `BatchSize` 调小，例如：

```json
"BatchSize": 4
```

如果返回内容被截断，可以把 `NumPredict` 调大，例如：

```json
"NumPredict": 8192
```

## 目标文件配置

```json
"TargetFiles": [
  {
    "SourceFilePath": "localization.json",
    "OutputDirectory": "output",
    "OutputFileNamePattern": "localization.json",
    "WriteSourceLanguageCopy": true
  }
]
```

说明：

| 参数 | 说明 |
| --- | --- |
| `SourceFilePath` | 源语言 JSON 文件路径 |
| `OutputDirectory` | 输出根目录 |
| `OutputFileNamePattern` | 每个语言目录下生成的文件名 |
| `WriteSourceLanguageCopy` | 是否把源语言也写入输出目录 |

`SourceFilePath` 和 `OutputDirectory` 都支持相对路径。

相对路径会以 `appsettings.json` 所在目录为基准。

例如：

```json
"SourceFilePath": "../../localization.json"
```

表示从配置文件所在目录向上两级寻找 `localization.json`。

## 输出目录

输出文件会按语言分目录生成。

例如配置：

```json
"OutputDirectory": "output",
"OutputFileNamePattern": "localization.json"
```

会生成：

```text
output/zh-CN/localization.json
output/en-US/localization.json
output/ja-JP/localization.json
output/ko-KR/localization.json
```

如果 `WriteSourceLanguageCopy` 为 `true`，会生成源语言副本：

```text
output/zh-CN/localization.json
```

已经存在的同名文件会被覆盖。

不在 `TargetLanguages` 中的其他语言目录不会被删除。

## 翻译词汇表

工具会自动保存翻译词汇表：

```json
"TranslationMemory": {
  "Directory": "translation-memory",
  "Enabled": true,
  "SaveIndented": true
}
```

词汇表文件名格式：

```text
源语言_to_目标语言.json
```

例如：

```text
translation-memory/zh-CN_to_en-US.json
translation-memory/zh-CN_to_ja-JP.json
translation-memory/en-US_to_ja-JP.json
```

每次翻译时，工具都会先读取对应词汇表。

如果某条文本已经存在翻译结果，会直接复用，不再请求 Ollama。

例如词汇表中已有：

```json
{
  "开始游戏": "Start Game"
}
```

下次再遇到 `开始游戏` 时，会直接使用 `Start Game`。

## 日志说明

运行时可能看到：

```text
Saved: output/zh-CN/localization.json
```

这表示文件已经写出到磁盘。

如果开启了 `WriteSourceLanguageCopy`，程序开始时会先保存源语言副本，所以这不代表翻译已经完成。

也可能看到：

```text
Ollama retry 1/2: ...
Batch failed, split 16 into 8+8: ...
```

这表示某个批次模型返回不稳定，工具正在自动重试或拆小批次继续翻译。

只要最后出现：

```text
Done!
```

就表示本次任务最终完成。

如果最后出现：

```text
Failed: ...
```

才表示本次任务失败。

## 常见建议

推荐初始配置：

```json
"BatchSize": 8,
"NumPredict": 4096
```

如果文本较长，或者目标语言翻译经常失败：

```json
"BatchSize": 4,
"NumPredict": 8192
```

如果翻译速度更重要，并且文本都比较短：

```json
"BatchSize": 16,
"NumPredict": 4096
```

翻译完成后，建议保留 `translation-memory` 目录。下次翻译可以复用已有结果，速度会更快，也能保持用词一致。
