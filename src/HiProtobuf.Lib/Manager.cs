/****************************************************************************
 * Description: 
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/

using System.IO;
using HiFramework.Log;

namespace HiProtobuf.Lib
{
    public static class Manager
    {
        /// <summary>
        /// 执行导出（保留原有签名，兼容已编译的 UI）。
        /// </summary>
        public static void Export()
        {
            TryExport();
        }

        /// <summary>
        /// 执行导出。成功返回 true；配置缺失等前置错误返回 false（详细原因见日志）。
        /// 导出过程中的异常不在此捕获，由调用方处理。
        /// </summary>
        public static bool TryExport()
        {
            if (string.IsNullOrEmpty(Settings.Export_Folder))
            {
                Log.Error("导出文件夹未配置");
                return false;
            }
            if (string.IsNullOrEmpty(Settings.Excel_Folder))
            {
                Log.Error("Excel文件夹未配置");
                return false;
            }
            if (string.IsNullOrEmpty(Settings.Compiler_Path))
            {
                Log.Error("编译器路径未配置");
                return false;
            }
            if (!File.Exists(Settings.Compiler_Path))
            {
                Log.Error("编译器不存在：" + Settings.Compiler_Path);
                return false;
            }
            EnumRegistry enumRegistry = EnumRegistry.Preflight();
            Log.Info("开始生成协议");
            new ProtoHandler().Process();
            Log.Info("生成协议结束");
            Log.Info("开始生成语言");
            new LanguageGenerater().Process();
            Log.Info("生成语言结束");
            Log.Info("开始编译语言");
            new Compiler().Porcess();
            Log.Info("编译语言结束");
            Log.Info("开始生成数据");
            new DataHandler(enumRegistry).Process();
            Log.Info("生成数据结束");
            enumRegistry.SaveSnapshot();
            return true;
        }
    }
}
