/****************************************************************************
 * Description: 
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/

using System;

namespace HiProtobuf.Lib
{
    public static class Settings
    {
        /// <summary>
        /// 导出文件夹
        /// </summary>
        public static string Export_Folder = string.Empty;

        /// <summary>
        /// Excel文件夹
        /// </summary>
        public static string Excel_Folder = string.Empty;

        /// <summary>
        /// Source directory for proto enum definitions (never an output directory).
        /// </summary>
        public static string Enum_Proto_Folder = string.Empty;

        /// <summary>
        /// 编译器路径
        /// </summary>
        public static string Compiler_Path = string.Empty;

        /// <summary>
        /// protobuf dll（始终与程序同目录，避免依赖当前工作目录）
        /// </summary>
        public static readonly string Protobuf_Dll_Path = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Google.Protobuf.dll");

        /// <summary>
        /// protoc 工具链根目录（默认与程序同级，可由命令行工具覆盖）
        /// </summary>
        public static string Protoc_Dir = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "protoc-3.8.0-win64");

        /// <summary>
        /// protoc路径
        /// </summary>
        public static string Protoc_Path
        {
            get { return System.IO.Path.Combine(Protoc_Dir, "bin", "protoc.exe"); }
        }

        /// <summary>
        /// Proto文件存放目录
        /// </summary>
        internal static readonly string proto_folder = "/proto";

        /// <summary>
        /// 语言存放目录
        /// </summary>
        internal static readonly string language_folder = "/language";

        /// <summary>
        /// 数据存放目录
        /// </summary>
        internal static readonly string dat_folder = "/dat";

        /// <summary>
        /// 本地化文件存放目录
        /// </summary>
        internal static readonly string localization_folder = "/Localization";

        //导出语言文件夹
        internal static readonly string csharp_folder = "/csharp";
        internal static readonly string csharpForILRumtime_folder = "/csharpForILRumtime";
        internal static readonly string csharp_dll_folder = "/csharp_dll";
        internal static readonly string cpp_folder = "/cpp";
        internal static readonly string go_folder = "/go";
        internal static readonly string java_folder = "/java";
        internal static readonly string python_folder = "/python";
    }
}
