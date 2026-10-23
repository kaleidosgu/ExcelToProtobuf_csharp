# HiExcel2Protobuf(Excel转Protobuf工具)

推荐使用python版本: [https://github.com/hiram3512/ExcelToProtobuf_python](https://github.com/hiram3512/ExcelToProtobuf_python)

优势:
- 跨平台支持(win,macOS)
- 自定义proto作为规则
- 不用再二次编译文件填充数据
- 不用再依赖.Net Framework

-----

#### 功能说明
- 将Excel文件生成protobuf文件和protobuf数据
- 目前支持生成五种语言C++(生成.cpp),C#(生成.cs和.dll),Golang(生成.go),Java(生成.java),Python(生成.py)

#### 如何使用
![VVbONwkevi](Others/VVbONwkevi.png)

从此处下载: [https://github.com/hiramtan/HiProtobuf/releases](https://github.com/hiramtan/HiProtobuf/releases)
1. 选择导出文件的存放目录
2. 选择Excel文件的存放目录
3. 选择编译器csc.exe
4. 点击导出

#### 类型对应关系
![EXCEL_5u9BDIL5mX](Others/EXCEL_5u9BDIL5mX.png)

#### 详细说明
- 大多数Windows电脑上默认安装.Net Framework,编译器路径一般在C:\Windows\Microsoft.NET\Framework64.如果没有安装从此处下载安装 [https://dotnet.microsoft.com/download/dotnet-framework](https://dotnet.microsoft.com/download/dotnet-framework)
- 如果希望导出.go文件,需要额外做一些操作,具体参照官方介绍: [https://developers.google.cn/protocol-buffers/docs/gotutorial](https://developers.google.cn/protocol-buffers/docs/gotutorial)
- C#语言额外导出了一份.dll,可以使用.cs源码或者.dll文件
- 会递归遍历Excel目录下的所有文件
- 会在导出目录下自动生成三个目录:
  - proto 存放.proto原始文件
  - language 存放各语言的生成文件(比如language/cpp/xx.cpp)
  - dat 存放.dat protobuf序列化数据
- 支持的数据类型有: "double", "float", "int32", "int64", "uint32", "uint64", "sint32", "sint64", "fixed32", "fixed64","sfixed32", "sfixed64", "bool", "string", "bytes"

- 支持对应的数组类型,数据分割用|

  ![qAn9ZhK3fO](Others/qAn9ZhK3fO.png)
- Excel配置格式如下:
  - 第一行为注释说明
  - 第二行为变量类型
  - 第三行为变量命名

  ![icZblwSnCx](Others/icZblwSnCx.png)
- 运行时使用如下:

  ![7U7wQbS0FP](Others/7U7wQbS0FP.png)
- 扩展:源码里面的对象很容易生成其他语言(Lua/Ruby/PHP...)和数据(Json/Xml/Bin...)

### 用 proto 枚举名填写 uint32 字段

在界面中配置“枚举 Proto 源目录”，指向维护枚举定义的目录。该目录应包含枚举 proto 及其 `import` 依赖，不要指向导出目录下的 `proto` 文件夹。

Excel 的类型行直接写带 proto `package` 的枚举全名，例如 `GunCross.Data.Task.ETaskLayoutType`；数组在末尾加 `[]`，多个值用 `|` 分隔。数据行填写对应枚举成员 `=` 后的行尾注释全文，例如 `ETaskLayoutType_Ring = 1; // 圆环排布` 对应填写 `圆环排布`。注释文字须为单行，且在同一枚举中唯一。没有行尾注释时可填写成员原名 `ETaskLayoutType_Ring`；旧类型写法 `uint32@...` 和 `uint32[]@...` 仍兼容。生成的 proto 字段仍是 `uint32` 或 `repeated uint32`；空的单值单元格使用枚举的零值，空数组保持为空。不接受数字代替名称。

如果暂不修改现有 `GunCross.Data.Task.proto`，`ETaskLayoutType_Ring` 当前的行尾注释是 `圆环；世界空间两个半轴不等时为椭圆环。`，Excel 也必须填写这段完整文字。将来把注释改成 `圆环排布` 后，Excel 才填写 `圆环排布`。

导出前会用 `protoc` 读取源 proto 并检查表格。未知类型、未知注释、未知成员和负数枚举值不能导出。上次成功导出的名称与注释映射保存在导出目录的 `enum-mapping.tsv` 中；同一填写文字被改配到另一个数字时，导出会停止。修改或删除注释后，仍需迁移 Excel 中的旧文字。

### 控制台读取示例

`src/HiProtobuf.ExportConsole` 是一个 .NET Framework 4.8 控制台项目，演示读取并检查导出的 `.dat` 文件。它根据文件名匹配生成类型，并从同一导出目录加载生成 DLL。在 Visual Studio 中将该项目设为启动项目后即可运行；也可通过命令行参数指定任意导出表的 `.dat` 路径。使用方法见项目内的 `README.md`。

---------------------------------
Support: hiramtan@live.com
