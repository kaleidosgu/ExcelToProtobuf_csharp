/****************************************************************************
 * Description: 
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
using System;
using System.IO;
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
            for (int i = 0; i < files.Length; i++)
            {
                var filePath = files[i];
                // 解析 proto 文件中的 namespace
                string protoContent = File.ReadAllText(filePath);
                string ns = "";
                var match = Regex.Match(protoContent, @"option\s+csharp_namespace\s*=\s*""([^""]+)""");
                if (match.Success)
                {
                    ns = match.Groups[1].Value;
                }
                else
                {
                    // fallback: try to get package
                    match = Regex.Match(protoContent, @"package\s+([a-zA-Z0-9_.]+)\s*;");
                    if (match.Success)
                    {
                        ns = match.Groups[1].Value;
                    }
                }
                string nsPath = string.IsNullOrEmpty(ns) ? "default" : ns; // 不再替换点为路径分隔符
                string nsOutFolder = Path.Combine(outFolder, nsPath);
                Directory.CreateDirectory(nsOutFolder);
                var command = Settings.Protoc_Path + string.Format(" -I={0} --csharp_out={1} {2}", protoPath, nsOutFolder, filePath);
                var log = Common.Cmd(command);
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
                var log = Common.Cmd(command);
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
                var log = Common.Cmd(command);
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
                var log = Common.Cmd(command);
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
                var log = Common.Cmd(command);
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
                try
                {
                    // 读取文件内容为文本（protoc 生成的文件通常是 UTF-8）
                    string content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                    
                    // 检查是否包含单独的 LF（不是 CRLF 的一部分）
                    if (content.Contains("\n"))
                    {
                        // 先将所有 CRLF 统一为 LF，然后再将所有 LF 转换为 CRLF
                        // 这样可以确保所有换行符都是 CRLF
                        content = content.Replace("\r\n", "\n").Replace("\n", "\r\n");
                        
                        // 写回文件，使用 UTF-8 编码
                        File.WriteAllText(filePath, content, System.Text.Encoding.UTF8);
                    }
                }
                catch
                {
                    // 忽略无法处理的文件（可能是二进制文件或编码问题）
                    continue;
                }
            }
        }
    }
}
