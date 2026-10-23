# 导出数据控制台示例

这是一个 .NET Framework 4.8 控制台应用。它根据 `.dat` 文件名查找对应的 `Excel_表名` 生成类型，打印每条记录，并检查记录非空；若记录有数值型 `ID` 属性，还会检查 ID 非零且不重复。

在 Visual Studio 中将 `HiProtobuf.ExportConsole` 设为启动项目后直接运行，默认读取 `Example/Export/dat/Excel_EnemyInfo.dat`。传入其他 `.dat` 文件的完整路径时，会自动从同一导出目录的 `language/csharp_dll/HiProtobuf.Excel.csharp.dll` 加载对应类型：

```powershell
HiProtobuf.ExportConsole.exe D:\GameProjects\dark-side\Tools\ExcelToProtobuf\Export\dat\Excel_TaskLayoutInfo.dat
```

若生成 DLL 在其他位置，可用第二个参数指定完整路径。成功时退出码为 `0`；文件不存在时为 `2`；解析或数据检查失败时为 `1`。`.dat` 与生成 DLL 应来自同一次导出。
