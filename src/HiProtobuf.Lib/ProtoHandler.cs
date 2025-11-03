/****************************************************************************
 * Description: 
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/

using HiFramework.Assert;
using OfficeOpenXml;
using System.IO;
using System.Collections.Generic;

namespace HiProtobuf.Lib
{
    internal class ProtoHandler
    {
        public static Dictionary<string, string> ClassNamespaceMap = new Dictionary<string, string>();
        public ProtoHandler()
        {
            var path = Settings.Export_Folder + Settings.proto_folder;
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);
        }

        public void Process()
        {
            //递归查询
            string[] files = Directory.GetFiles(Settings.Excel_Folder, "*.xlsx", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var path = files[i];
                if (path.Contains("~$"))//已打开的表格格式
                {
                    continue;
                }
                ProcessExcel(path);
            }
        }

        void ProcessExcel(string path)
        {
            AssertThat.IsNotNullOrEmpty(path);
            var fileInfo = new FileInfo(path);
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (ExcelPackage excelPackage = new ExcelPackage(fileInfo))
            {
                var worksheets = excelPackage.Workbook.Worksheets;
                var strNameSpace = Path.GetFileNameWithoutExtension(path);
                foreach (var worksheet in worksheets)
                {
                    AssertThat.IsNotNull(worksheet, "Excel's sheet is null");
                    var strNameClass = worksheet.Name;
                    // 记录 class-namespace 映射
                    if (!ClassNamespaceMap.ContainsKey(strNameClass))
                        ClassNamespaceMap.Add(strNameClass, strNameSpace);
                    new ProtoGenerater(strNameSpace, strNameClass, worksheet).Process();
                }
            }
        }
    }
}
