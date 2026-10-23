# HiProtobuf.Cli

无界面的导出入口，供脚本、CI 和外部工具（包括模型代理）调用。核心逻辑复用 `HiProtobuf.Lib`，与 UI 的行为一致。

## 构建

```powershell
msbuild src\HiProtobuf.Cli\HiProtobuf.Cli.csproj /p:Configuration=Debug
```

产物输出到 `dist\HiProtobuf.Cli\`（已在 `.gitignore` 中忽略）。也可以用 VS 打开 `HiProtobuf.sln` 构建。

## 用法

```powershell
dist\HiProtobuf.Cli\HiProtobuf.Cli.exe export --excel D:\proj\Excel --out D:\proj\Export
```

| 参数 | 必填 | 说明 |
|---|---|---|
| `--excel <目录>` | 是 | Excel 根目录，递归扫描 `.xlsx` |
| `--out <目录>` | 是 | 导出根目录，会写入 `proto/`、`language/`、`dat/` |
| `--csc <文件>` | 否 | `csc.exe`，默认 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（不存在时回退 32 位） |
| `--enum-proto <目录>` | 否 | 枚举 proto 源目录 |
| `--protoc-dir <目录>` | 否 | protoc 工具链目录，需包含 `bin\protoc.exe` 与 `include` |
| `--config <文件>` | 否 | 指定配置文件，默认同目录 `appsettings.json` |
| `--dry-run` | 否 | 只打印配置和将被清理的目录 |
| `--quiet` | 否 | 不打印 Lib 的 Info 日志 |

## 配置文件

`appsettings.json` 与 exe 同目录，所有字段可选；相对路径以配置文件所在目录为基准。

```json
{
  "protocDir": "../../Example/HiProtobuf/protoc-3.8.0-win64",
  "cscPath": "",
  "excelFolder": "",
  "exportFolder": "",
  "enumProtoFolder": "",
  "verbose": true
}
```

配置好 `excelFolder` / `exportFolder` 后可以零参数运行：

```powershell
HiProtobuf.Cli.exe export
```

## protoc 查找顺序

1. `--protoc-dir`
2. `appsettings.json` 的 `protocDir`
3. `<exe 目录>\protoc-3.8.0-win64`

都找不到时退出码为 `2`，并列出尝试过的路径。

## 退出码

| 码 | 含义 |
|---|---|
| 0 | 成功 |
| 1 | 导出失败（含 csc 编译失败、proto 解析失败） |
| 2 | 参数、配置文件或运行环境错误 |

## 注意

- 导出会先删除 `--out` 下的 `proto`、`language`、`dat`、`language\csharp_dll`，再重新生成；不确定时先加 `--dry-run`。
- 仅支持 Windows + .NET Framework 4.8。
- 生成结果的校验可以用 `HiProtobuf.ExportConsole` 读取 `.dat`。
