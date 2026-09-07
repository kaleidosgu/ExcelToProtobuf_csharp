/****************************************************************************
 * Description: 
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
using HiFramework.Log;
using HiProtobuf.Lib;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HiProtobuf.Lib
{
    internal class LanguageGenerater
    {
        private string _languageFolder;
        public void Process()
        {
            _languageFolder = Settings.Export_Folder + Settings.language_folder;
            if (Directory.Exists(_languageFolder))
            {
                Directory.Delete(_languageFolder, true);
            }
            Directory.CreateDirectory(_languageFolder);

            var protoFolder = Settings.Export_Folder + Settings.proto_folder;
            //Process_csharpForILRumtime(protoFolder);
            Process_csharp(protoFolder);
            //Process_cpp(protoFolder);
            //Process_go(protoFolder);
            //Process_java(protoFolder);
            //Process_python(protoFolder);
        }

        private void Process_csharpForILRumtime(string protoPath)
        {
            var outFolder = _languageFolder + Settings.csharpForILRumtime_folder;
            Directory.CreateDirectory(outFolder);
            //递归查询
            string[] files = Directory.GetFiles(protoPath, "*.proto", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var filePath = files[i];
                var ilProtoc = Environment.CurrentDirectory + @"\protoc-3.8.0-win64\bin\protocILRuntime.exe";

                var command = ilProtoc + string.Format(" -I={0} --csharp_out={1} {2}", protoPath, outFolder, filePath);
                var log = Common.Cmd(command);
            }
            ConvertLineEndingsToCRLF(outFolder);
        }

        private void Process_csharp(string protoPath)
        {
            var outFolder = _languageFolder + Settings.csharp_folder;
            Directory.CreateDirectory(outFolder);
            //递归查询
            string[] files = Directory.GetFiles(protoPath, "*.proto", SearchOption.AllDirectories);
            // 先编译所有 proto 文件到同一个输出目录（为了处理 import）
            var allProtoFiles = string.Join(" ", files.Select(f => Path.GetFileName(f)));
            var command = Settings.Protoc_Path + string.Format(" -I={0} --csharp_out={1} {2}", protoPath, outFolder, allProtoFiles);
            Log.Info($"Proto编译命令(C#): {command}");
            var log = Common.Cmd(command);
            Log.Info($"Proto编译结果(C#): {log}");
            // 按命名空间移动生成的 .cs 文件到子目录
            var csFiles = Directory.GetFiles(outFolder, "*.cs");
            foreach (var csFile in csFiles)
            {
                string content = File.ReadAllText(csFile);
                string ns = "";
                var match = Regex.Match(content, @"namespace\s+([a-zA-Z0-9_.]+)");
                if (match.Success)
                {
                    ns = match.Groups[1].Value;
                }
                if (!string.IsNullOrEmpty(ns))
                {
                    var destFolder = Path.Combine(outFolder, ns);
                    var fileName = Path.GetFileName(csFile);
                    var destFile = Path.Combine(destFolder, fileName);
                    if (!Directory.Exists(destFolder))
                    {
                        Directory.CreateDirectory(destFolder);
                    }
                    if (!File.Exists(destFile))
                    {
                        File.Move(csFile, destFile);
                    }
                }
            }
            ConvertLineEndingsToCRLF(outFolder);
        }

        private void Process_cpp(string protoPath)
        {
            var outFolder = _languageFolder + Settings.cpp_folder;
            Directory.CreateDirectory(outFolder);
            //递归查询
            string[] files = Directory.GetFiles(protoPath, "*.proto", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var filePath = files[i];
                var command = Settings.Protoc_Path + string.Format(" -I={0} --cpp_out={1} {2}", protoPath, outFolder, filePath);
                Log.Info($"Proto编译命令(C++): {command}");
                var log = Common.Cmd(command);
                Log.Info($"Proto编译结果(C++): {log}");
            }
            ConvertLineEndingsToCRLF(outFolder);
        }

        private void Process_go(string protoPath)
        {
            var outFolder = _languageFolder + Settings.go_folder;
            Directory.CreateDirectory(outFolder);
            //递归查询
            string[] files = Directory.GetFiles(protoPath, "*.proto", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var filePath = files[i];
                var command = Settings.Protoc_Path + string.Format(" -I={0} --go_out={1} {2}", protoPath, outFolder, filePath);
                Log.Info($"Proto编译命令(Go): {command}");
                var log = Common.Cmd(command);
                Log.Info($"Proto编译结果(Go): {log}");
            }
            ConvertLineEndingsToCRLF(outFolder);
        }

        private void Process_java(string protoPath)
        {
            var outFolder = _languageFolder + Settings.java_folder;
            Directory.CreateDirectory(outFolder);
            //递归查询
            string[] files = Directory.GetFiles(protoPath, "*.proto", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var filePath = files[i];
                var command = Settings.Protoc_Path + string.Format(" -I={0} --java_out={1} {2}", protoPath, outFolder, filePath);
                Log.Info($"Proto编译命令(Java): {command}");
                var log = Common.Cmd(command);
                Log.Info($"Proto编译结果(Java): {log}");
            }
            ConvertLineEndingsToCRLF(outFolder);
        }

        private void Process_python(string protoPath)
        {
            var outFolder = _languageFolder + Settings.python_folder;
            Directory.CreateDirectory(outFolder);
            //递归查询
            string[] files = Directory.GetFiles(protoPath, "*.proto", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var filePath = files[i];
                var command = Settings.Protoc_Path + string.Format(" -I={0} --python_out={1} {2}", protoPath, outFolder, filePath);
                Log.Info($"Proto编译命令(Python): {command}");
                var log = Common.Cmd(command);
                Log.Info($"Proto编译结果(Python): {log}");
            }
            ConvertLineEndingsToCRLF(outFolder);
        }

        /// <summary>
        /// 将文件夹中所有文本文件的换行符从 LF 转换为 CRLF
        /// </summary>
        private void ConvertLineEndingsToCRLF(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return;

            // 递归获取所有文件
            string[] files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                string operation = "读取";
                try
                {
                    // 读取文件内容为文本（protoc 生成的文件通常是 UTF-8）
                    string content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);

                    // 检查是否包含单独的 LF（不是 CRLF 的一部分）
                    if (content.Contains("\n"))
                    {
                        // 先将所有 CRLF 统一为 LF，然后再将所有 LF 转换为 CRLF
                        // 这样可以确保所有换行符都是 CRLF
                        content = content.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

                        // 写回文件，使用无 BOM 的 UTF-8 编码
                        operation = "写入";
                        File.WriteAllText(filePath, content, new System.Text.UTF8Encoding(false));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"转换文件换行符失败。操作：{operation}，文件：{filePath}，异常类型：{ex.GetType().FullName}，HRESULT：0x{ex.HResult:X8}，消息：{ex.Message}");
                    continue;
                }
            }
        }
    }
}
