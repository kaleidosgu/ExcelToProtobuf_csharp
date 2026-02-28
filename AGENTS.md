# AGENTS.md - Agent Guidelines for HiProtobuf

## Project Overview

HiProtobuf is a C# .NET Framework 4.8 tool that converts Excel files to Protobuf format and generates C# code. It consists of three projects:
- **HiProtobuf.Lib**: Core library handling proto generation, compilation, and data export
- **HiProtobuf.UI**: Windows Forms UI application
- **HiProtobuf.Test**: MSTest unit tests

## Build Commands

### Building the Solution
```bash
# Using MSBuild (requires Visual Studio or MSBuild installed)
msbuild HiProtobuf.sln /p:Configuration=Debug
msbuild HiProtobuf.sln /p:Configuration=Release
```

### Building Individual Projects
```bash
msbuild HiProtobuf.Lib/HiProtobuf.Lib.csproj /p:Configuration=Debug
msbuild HiProtobuf.Test/HiProtobuf.Test.csproj /p:Configuration=Debug
msbuild HiProtobuf.UI/HiProtobuf.UI.csproj /p:Configuration=Debug
```

### Running Tests
```bash
# Run all tests via MSTest (requires Visual Studio Test Runner or vstest.console.exe)
vstest.console.exe HiProtobuf.Test/bin/Debug/HiProtobuf.Test.dll

# Run a single test method
vstest.console.exe HiProtobuf.Test/bin/Debug/HiProtobuf.Test.dll /Tests:TestExport

# Alternative: Run via Visual Studio Test Explorer
devenv /TestEnable HiProtobuf.sln
```

## Code Style Guidelines

### File Headers
Each source file should include a standard header:
```csharp
/****************************************************************************
 * Description: [brief description of file purpose]
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
```

### Imports/Using Statements
- Place `using` statements at the top of the file
- Order: System namespaces first, then third-party, then project-specific
- Example:
```csharp
using System;
using System.IO;
using System.Linq;
using Google.Protobuf;
using HiFramework.Assert;
using OfficeOpenXml;
using HiProtobuf.Lib;
```

### Naming Conventions
- **Classes/Types**: PascalCase (e.g., `ProtoHandler`, `DataHandler`)
- **Methods**: PascalCase (e.g., `Process()`, `Export()`)
- **Public Properties/Fields**: PascalCase
- **Private/Internal Fields**: camelCase with optional underscore suffix (e.g., `_assembly`, `_excelIns`)
- **Constants**: snake_case with underscore suffix (e.g., `double_`, `int32_s`, `NameSpace`)
- **Namespaces**: PascalCase (e.g., `HiProtobuf.Lib`)

### Type Usage
- Use explicit types rather than `var` for clarity in complex expressions
- Use `.NET` built-in types (e.g., `string`, `int`, `bool`) in C# code
- Use protobuf types (e.g., `ByteString`, `RepeatedField<T>`) when working with protobuf data

### Class Modifiers
- Use `internal` for classes that are not part of the public API
- Use `public static` for manager/utility classes (e.g., `Manager.Export()`)
- Use `private` for implementation details

### Error Handling
- Use `HiFramework.Assert` for precondition checks:
  - `AssertThat.IsNotNullOrEmpty(path)`
  - `AssertThat.IsTrue(condition, "message")`
  - `AssertThat.IsNotNull(obj, "message")`
- Use `HiFramework.Log` for logging:
  - `Log.Info("message")`
  - `Log.Error("message")`
  - `Log.Warning("message")`
- Return early on errors rather than deeply nesting

### Code Formatting
- Indent with 4 spaces (VS default)
- Place opening brace on same line
- Use braces even for single-line statements
- Maximum line length: 120 characters (soft limit)

### Code Patterns

#### Processing Excel Files
```csharp
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
using (ExcelPackage excelPackage = new ExcelPackage(fileInfo))
{
    var worksheets = excelPackage.Workbook.Worksheets;
    foreach (var worksheet in worksheets)
    {
        // Process worksheet
    }
}
```

#### Directory Management
```csharp
var path = Settings.Export_Folder + Settings.proto_folder;
if (Directory.Exists(path))
{
    Directory.Delete(path, true);
}
Directory.CreateDirectory(path);
```

#### Reflection for Type Creation
```csharp
var instance = _assembly.CreateInstance(typeName);
if (instance == null)
{
    Log.Error($"Failed to create instance of {typeName}");
    return;
}
```

### Dependency Versions
- **.NET Framework**: 4.8
- **EPPlus**: 5.0.4
- **Google.Protobuf**: (see lib folder)
- **HiFramework.Assert**: 1.0.2
- **HiFramework.Log**: 1.0.6
- **MSTest.TestFramework**: 2.1.0

### Important Implementation Notes
- Proto files are generated in `Settings.Export_Folder + Settings.proto_folder`
- Generated C# DLLs go to `Settings.Export_Folder + Settings.language_folder + Settings.csharp_dll_folder`
- Data files (.dat) are serialized using `MessageExtensions.WriteTo()`
- The DLL is loaded into memory (via `Assembly.Load(dllBytes)`) to avoid file locking

## Common Tasks

### Adding a New Proto Type
1. Add the type constant in `Common.cs` (e.g., `public const string newType_ = "newtype";`)
2. Add to `VariableType` array if it's a valid field type
3. Handle parsing in `DataHandler.GetVariableValue()`

### Adding a New Test
```csharp
[TestClass]
public class MyTests
{
    [TestMethod]
    public void MyTestMethod()
    {
        // Arrange
        var expected = ...;
        
        // Act
        var result = ...;
        
        // Assert
        Assert.AreEqual(expected, result);
    }
}
```

### Running the Application
- Build the solution in Debug or Release mode
- Execute `HiProtobuf.UI.exe` from the output folder
- Configure paths in the UI (Excel folder, export folder, compiler path)
