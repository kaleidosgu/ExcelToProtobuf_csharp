/****************************************************************************
 * Description: 
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/

using System.IO;

namespace HiProtobuf.Lib
{
    internal class Common
    {
        //double
        //float
        //int32
        //int64
        //uint32
        //uint64
        //sint32
        //sint64
        //fixed32
        //fixed64
        //sfixed32
        //sfixed64
        //bool
        //string
        //bytes
        public const string double_ = "double";
        public const string float_ = "float";
        public const string int32_ = "int32";
        public const string int64_ = "int64";
        public const string uint32_ = "uint32";
        public const string uint64_ = "uint64";
        public const string sint32_ = "sint32";
        public const string sint64_ = "sint64";
        public const string fixed32_ = "fixed32";
        public const string fixed64_ = "fixed64";
        public const string sfixed32_ = "sfixed32";
        public const string sfixed64_ = "sfixed64";
        public const string bool_ = "bool";
        public const string string_ = "string";
        public const string bytes_ = "bytes";
        public const string text_ = "text";
        public const string vector2_ = "vector2";
        public const string vector3_ = "vector3";

        public const string double_s = "double[]";
        public const string float_s = "float[]";
        public const string int32_s = "int32[]";
        public const string int64_s = "int64[]";
        public const string uint32_s = "uint32[]";
        public const string uint64_s = "uint64[]";
        public const string sint32_s = "sint32[]";
        public const string sint64_s = "sint64[]";
        public const string fixed32_s = "fixed32[]";
        public const string fixed64_s = "fixed64[]";
        public const string sfixed32_s = "sfixed32[]";
        public const string sfixed64_s = "sfixed64[]";
        public const string bool_s = "bool[]";
        public const string string_s = "string[]";
        public const string bytes_s = "bytes[]";
        public const string text_s = "text[]";


        public static readonly string[] VariableType = new[] {
            "double", "float", "int32", "int64", "uint32", "uint64", "sint32", "sint64", "fixed32", "fixed64","sfixed32", "sfixed64", "bool", "string", "bytes", "text", "vector2", "vector3",
            "double[]", "float[]", "int32[]", "int64[]", "uint32[]", "uint64[]", "sint32[]", "sint64[]", "fixed32[]", "fixed64[]","sfixed32[]", "sfixed64[]", "bool[]", "string[]", "bytes[]", "text[]"
        };

        internal static string Run(string fileName, string arguments)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments ?? string.Empty;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.StandardOutputEncoding = System.Text.Encoding.Default;
            process.StartInfo.StandardErrorEncoding = System.Text.Encoding.Default;
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return "ERROR(" + process.ExitCode + "): " + error + "\nOUTPUT: " + output;
            }
            if (!string.IsNullOrEmpty(error))
            {
                return output + "\nSTDERR: " + error;
            }
            return output;
        }
    }
}
