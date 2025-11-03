# HiProtobuf 技术设计文档 / Technical Design Document

---

## 1. 项目简介 / Project Overview

HiProtobuf 是一个自动化工具，旨在将 Excel 表格数据转换为 Protobuf 协议，并生成多语言代码和数据文件，广泛应用于游戏、数据驱动开发等场景。它支持一键导出、编译和数据序列化，极大提升数据流转效率。

HiProtobuf is an automation tool designed to convert Excel spreadsheet data into Protobuf definitions, and generate multi-language code and data files. It is widely used in game development and data-driven scenarios, supporting one-click export, compilation, and data serialization to greatly improve data workflow efficiency.

---

## 2. 系统架构 / System Architecture

```mermaid
graph TD
    A[Excel Files] --> B[ProtoHandler]
    B --> C[ProtoGenerater]
    C --> D[.proto Files]
    D --> E[LanguageGenerater]
    E --> F[Multi-language Code]
    F --> G[Compiler (C#)]
    G --> H[C# DLL]
    H --> I[DataHandler]
    A --> I
    I --> J[Binary Data Files]
    K[UI/Manager] --> B
    K --> E
    K --> G
    K --> I
```

---

## 3. 主要模块说明 / Main Module Description

### 3.1 HiProtobuf.Lib

- **ProtoHandler**  
  递归扫描 Excel 文件，调用 `ProtoGenerater` 生成 `.proto` 文件。  
  Recursively scans Excel files and calls `ProtoGenerater` to generate `.proto` files.

- **ProtoGenerater**  
  解析 Excel 表头和字段，生成 Protobuf 协议定义文件。  
  Parses Excel headers and fields to generate Protobuf definition files.

- **LanguageGenerater**  
  调用 `protoc` 工具，将 `.proto` 文件编译为多语言代码（C#、C++、Java、Go、Python 等）。  
  Uses `protoc` to compile `.proto` files into multi-language code (C#, C++, Java, Go, Python, etc.).

- **Compiler**  
  自动编译 C# 代码为 DLL，便于后续数据序列化。  
  Automatically compiles C# code into DLLs for subsequent data serialization.

- **DataHandler**  
  反射加载生成的 DLL，读取 Excel 数据，填充 Protobuf 对象并序列化为二进制数据文件。  
  Loads generated DLLs via reflection, reads Excel data, populates Protobuf objects, and serializes them into binary data files.

- **Common/Settings**  
  定义全局常量、类型映射、命令行工具调用、路径配置等。  
  Defines global constants, type mappings, command-line tool calls, and path configurations.

- **Manager**  
  统一调度导出流程（协议生成、代码生成、编译、数据生成）。  
  Orchestrates the export process (protocol generation, code generation, compilation, data generation).

### 3.2 HiProtobuf.UI

- Windows Forms 界面，支持导出路径、Excel 路径、编译器路径等配置，提供一键导出按钮，日志输出等功能。
- Windows Forms UI for configuring export path, Excel path, compiler path, one-click export, and log output.

### 3.3 HiProtobuf.Test

- 单元测试，主要测试导出流程是否能顺利执行。
- Unit tests, mainly to verify the export process.

---

## 4. 主要流程 / Main Workflow

1. **配置导出路径、Excel 路径、编译器路径**（UI 或配置文件）。  
   Configure export path, Excel path, and compiler path (via UI or config file).
2. **点击导出**，`Manager.Export()` 依次执行：  
   Click export, `Manager.Export()` executes in order:
   - `ProtoHandler.Process()`：生成所有 `.proto` 文件 / Generate all `.proto` files
   - `LanguageGenerater.Process()`：生成多语言代码 / Generate multi-language code
   - `Compiler.Porcess()`：编译 C# 代码为 DLL / Compile C# code to DLL
   - `DataHandler.Process()`：读取 Excel，填充 Protobuf 对象，序列化为二进制数据 / Read Excel, populate Protobuf objects, serialize to binary data
3. **输出结果 / Output Results**：
   - `proto/`：所有协议文件 / All proto files
   - `language/`：多语言代码 / Multi-language code
   - `dat/`：二进制数据文件 / Binary data files
   - `csharp_dll/`：C# DLL

---

## 5. 依赖与技术栈 / Dependencies & Tech Stack

- .NET Framework (C#)
- Windows Forms
- OfficeOpenXml (EPPlus)
- Google.Protobuf
- protoc (Protocol Buffers Compiler)
- MSTest (Unit Testing)

---

## 6. 典型用例 / Typical Use Cases

- 游戏策划在 Excel 中维护数据表，开发一键导出，自动生成协议、代码和数据，前后端/客户端/服务器多端共享数据结构。
- Game designers maintain data in Excel, developers use one-click export to auto-generate protocol, code, and data, enabling data structure sharing across client/server/backend.

- 支持多语言项目的数据同步和结构一致性。
- Supports data synchronization and structure consistency in multi-language projects.

---

## 7. 设计亮点 / Design Highlights

- **自动化全流程 / Full Automation**：从 Excel 到多语言代码和数据文件全自动化。
- **多语言支持 / Multi-language Support**：支持 C#、C++、Java、Go、Python 等多端开发。
- **可扩展性 / Extensibility**：通过配置和模块化设计，便于扩展支持更多语言或自定义导出逻辑。
- **UI 友好 / User-friendly UI**：提供简单易用的 Windows 界面，适合非程序人员操作。

---

## 8. 目录结构说明 / Directory Structure

- `HiProtobuf.Lib/`：核心逻辑库 / Core logic library
- `HiProtobuf.UI/`：界面层 / UI layer
- `HiProtobuf.Test/`：测试工程 / Test project
- `packages/`、`lib/`：依赖库 / Dependencies
- `bin/`、`obj/`：编译输出 / Build outputs

---

如需更详细的设计细节或代码注释，可进一步指定模块或功能点。

For more detailed design or code comments, please specify the module or feature. 