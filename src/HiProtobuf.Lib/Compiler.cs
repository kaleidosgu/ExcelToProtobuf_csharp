/****************************************************************************
 * Description: 
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HiFramework.Log;

namespace HiProtobuf.Lib
{
    internal class Compiler
    {
        public static readonly string DllName = "/HiProtobuf.Excel.csharp.dll";

        public Compiler()
        {
            var folder = Settings.Export_Folder + Settings.language_folder + Settings.csharp_dll_folder;
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
            Directory.CreateDirectory(folder);
        }

        public void Porcess()
        {
            var commond = @"-target:library -out:{0} -reference:{1} -recurse:{2}";
            var dllPath = Settings.Export_Folder + Settings.language_folder + Settings.csharp_dll_folder + DllName;
            var csharpFolder = Settings.Export_Folder + Settings.language_folder + Settings.csharp_folder;
            commond = string.Format(commond,
                Quote(dllPath), Quote(Settings.Protobuf_Dll_Path), Quote(csharpFolder + @"\*.cs"));
            var result = Common.Run(Settings.Compiler_Path, commond);
            if (!File.Exists(dllPath))
            {
                throw new InvalidOperationException("C# 编译失败，未生成 " + dllPath
                    + Environment.NewLine + "编译器输出：" + Environment.NewLine + result);
            }
        }

        private static string Quote(string path)
        {
            return "\"" + (path ?? string.Empty) + "\"";
        }
    }
}
