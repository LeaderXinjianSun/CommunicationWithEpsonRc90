using System;
using System.Data;
using System.IO;
using System.Text;

namespace BingLibrary.hjb
{
    public class Csvfile
    {
        /// <summary>
        /// 导出报表为Csv
        /// </summary>
        /// <param name="dt">DataTable</param>
        /// <param name="strFilePath">物理路径</param>
        /// <param name="tableheader">表头</param>
        /// <param name="columname">字段标题,逗号分隔</param>
        [Obsolete("dt2csv has been deprecated.  Use SaveToCsv.")]
        public static bool dt2csv(DataTable dt, string strFilePath, string tableheader, string columname)
        {
            try
            {
                string strBufferLine = "";
                StreamWriter strmWriterObj = new StreamWriter(strFilePath, false, System.Text.Encoding.UTF8);
                strmWriterObj.WriteLine(tableheader);
                strmWriterObj.WriteLine(columname);
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    strBufferLine = "";
                    for (int j = 0; j < dt.Columns.Count; j++)
                    {
                        if (j > 0)
                            strBufferLine += ",";
                        strBufferLine += dt.Rows[i][j].ToString();
                    }
                    strmWriterObj.WriteLine(strBufferLine);
                }
                strmWriterObj.Close();
                return true;
            }
            catch
            {
                Tool.DebugInfo("导出CSV文件失败");
                return false;
            }
        }

        /// <summary>
        /// 将Csv读入DataTable
        /// </summary>
        /// <param name="filePath">csv文件路径</param>
        /// <param name="n">表示第n行是字段title,第n+1行是记录开始</param>
        [Obsolete("csv2dt has been deprecated.  Use GetFromCsv.")]
        public static DataTable csv2dt(string filePath, int n, DataTable dt)
        {
            StreamReader reader = new StreamReader(filePath, System.Text.Encoding.UTF8, false);
            int i = 0, m = 0;
            reader.Peek();
            while (reader.Peek() > 0)
            {
                m = m + 1;
                string str = reader.ReadLine();
                if (m >= n + 1)
                {
                    string[] split = str.Split(',');

                    System.Data.DataRow dr = dt.NewRow();
                    for (i = 0; i < split.Length; i++)
                    {
                        dr[i] = split[i];
                    }
                    dt.Rows.Add(dr);
                }
            }
            return dt;
        }

        /// <summary>
        /// 写入一行数据
        /// </summary>
        /// <param name="strFilePath"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [Obsolete("savetocsv has been deprecated.  Use AddNewLine.")]
        public static bool savetocsv(string strFilePath, string[] value)
        {
            try
            {
                var filewriter = new StreamWriter(strFilePath, true, Encoding.Default);
                filewriter.WriteLine(String.Join(",", value));
                filewriter.Flush();
                filewriter.Close(); return true;
            }
            catch { return false; }
        }




        public static bool SaveToCsv(DataTable dt, string strFilePath, string tableHeader, string columName) 
        {
            try 
            { 
                string strBufferLine = "";
                StreamWriter strmWriterObj = new StreamWriter(strFilePath, false, System.Text.Encoding.UTF8);
                strmWriterObj.WriteLine(tableHeader);
                strmWriterObj.WriteLine(columName);
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    strBufferLine = "";
                    for (int j = 0; j < dt.Columns.Count; j++)
                    {
                        if (j > 0)
                            strBufferLine += ",";
                        strBufferLine += dt.Rows[i][j].ToString();
                    }
                    strmWriterObj.WriteLine(strBufferLine);
                }
                strmWriterObj.Close();
                return true;
            }
            catch
            {
                Tool.DebugInfo("保存CSV文件失败");
                return false;
            }
        }


        public static DataTable GetFromCsv(string filePath, int n, DataTable dt)
        { 
            StreamReader reader = new StreamReader(filePath, System.Text.Encoding.UTF8, false);
            int i = 0, m = 0;
            reader.Peek();
            while (reader.Peek() > 0)
            {
                m = m + 1;
                string str = reader.ReadLine();
                if (m >= n + 1)
                {
                    string[] split = str.Split(',');

                    System.Data.DataRow dr = dt.NewRow();
                    for (i = 0; i < split.Length; i++)
                    {
                        dr[i] = split[i];
                    }
                    dt.Rows.Add(dr);
                }
            }
            reader.Close();
            return dt;
        }


        public static bool AddNewLine(string strFilePath, string[] value)
        {
            try
            {
                var filewriter = new StreamWriter(strFilePath, true, Encoding.Default);
                filewriter.WriteLine(String.Join(",", value));
                filewriter.Flush();
                filewriter.Close(); return true;
            }
            catch {
                Tool.DebugInfo("CSV添加一行数据失败");
                return false; }
        }

    }
}