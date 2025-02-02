﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using LumenWorks.Framework.IO.Csv;

public static class HelperDB
{
    public static DataTable ToDataTable<T>(this IEnumerable<T> collection)
    {
        var dt = new DataTable("DataTable");

        var t = typeof(T);
        var pia = t.GetProperties();

        //Inspect the properties and create the columns in the DataTable
        foreach (var pi in pia)
        {
            var ColumnType = pi.PropertyType;
            if (ColumnType.IsGenericType) ColumnType = ColumnType.GetGenericArguments()[0];
            dt.Columns.Add(pi.Name, ColumnType);
        }

        //Populate the data table
        foreach (var item in collection)
        {
            var dr = dt.NewRow();
            dr.BeginEdit();
            foreach (var pi in pia)
                if (pi.GetValue(item, null) != null)
                    dr[pi.Name] = pi.GetValue(item, null);
            dr.EndEdit();
            dt.Rows.Add(dr);
        }

        return dt;
    }

    public static DataTable csvToDataTableFromFile(string filePath)
    {
        return csvToDataTableFromFile(filePath, true, ',');
    }

    public static DataTable csvToDataTableFromFile(string filePath, bool isRowOneHeader, char separator)
    {
        var csvDataTable = new DataTable();
        using (var csv = new CsvReader(new StreamReader(filePath), isRowOneHeader, separator, '"', '\\', '#',
                   ValueTrimmingOptions.UnquotedOnly))
        {
            var fieldCount = csv.FieldCount;
            //CREATE HEADERS
            var aHeaders = new List<string>();
            if (isRowOneHeader)
                foreach (var h in csv.GetFieldHeaders())
                    aHeaders.Add(h.Replace(" ", "_"));
            else
                for (var i = 0; i < fieldCount; i++)
                    aHeaders.Add("col" + i);
            for (var i = 0; i < aHeaders.Count; i++)
                csvDataTable.Columns.Add(aHeaders[i], typeof(string));

            //ADD ROWS
            try
            {
                while (csv.ReadNextRecord())
                {
                    var oRow = csvDataTable.NewRow();
                    for (var i = 0; i < fieldCount; i++)
                        try
                        {
                            oRow[i] = csv[i];
                        }
                        catch (Exception ex)
                        {
                            oRow[i] = "";
                        }

                    csvDataTable.Rows.Add(oRow);
                }
            }
            catch
            {
            }
        }

        return csvDataTable;
    }

    public static DataTable csvToDataTable(string fileContent, bool isRowOneHeader)
    {
        return csvToDataTable(fileContent, isRowOneHeader, null, ",");
    }

    public static DataTable csvToDataTable(string fileContent, bool isRowOneHeader, List<Type> aColumnTypes)
    {
        return csvToDataTable(fileContent, isRowOneHeader, aColumnTypes, ",");
    }

    public static DataTable csvToDataTable(string fileContent, bool isRowOneHeader, List<Type> aColumnTypes,
        string separator)
    {
        var file = fileContent;
        TextReader sr = new StringReader(file);
        var csvDataTable = new DataTable();
        using (var csv = new CsvReader(sr, isRowOneHeader, separator.ToCharArray()[0]))
        {
            csv.MissingFieldAction = MissingFieldAction.ReplaceByEmpty;
            csv.SkipEmptyLines = true;

            var fieldCount = csv.FieldCount;
            //CREATE HEADERS
            var aHeaders = new List<string>();
            if (isRowOneHeader)
                foreach (var h in csv.GetFieldHeaders())
                    aHeaders.Add(h.Replace(" ", "_"));
            else
                for (var i = 0; i < fieldCount; i++)
                    aHeaders.Add("col" + i);
            for (var i = 0; i < aHeaders.Count; i++)
                if (aColumnTypes == null)
                    csvDataTable.Columns.Add(aHeaders[i], typeof(string));
                else
                    csvDataTable.Columns.Add(aHeaders[i], aColumnTypes[i]);

            //ADD ROWS
            try
            {
                while (csv.ReadNextRecord())
                {
                    var oRow = csvDataTable.NewRow();
                    for (var i = 0; i < fieldCount; i++)
                        try
                        {
                            oRow[i] = csv[i];
                        }
                        catch (Exception ex)
                        {
                            oRow[i] = "";
                        }

                    csvDataTable.Rows.Add(oRow);
                }
            }
            catch
            {
            }
        }

        return csvDataTable;


        //no try/catch - add these in yourselfs or let exception happen
        //String[] csvData = File.ReadAllLines(HttpContext.Current.Server.MapPath(file));
        var csvData = file.Split(Convert.ToChar("\n"));

        //if no data in file ‘manually' throw an exception
        if (csvData.Length == 0) throw new Exception("CSV File Appears to be Empty");

        string[] headings = null;
        headings = Regex.Split(csvData[0], separator);

        var index = 0; //will be zero or one depending on isRowOneHeader

        if (isRowOneHeader) //if first record lists headers
        {
            index = 1; //so we won't take headings as data

            //for each heading
            for (var i = 0; i < headings.Length; i++)
            {
                //replace spaces with underscores for column names
                headings[i] = headings[i].Replace(" ", "_");
                headings[i] = headings[i].Replace("\r", "");
                if (separator == "\"" || separator == "\",\"")
                    headings[i] = headings[i].Replace("\"", "").Replace(",\r", "");

                //add a column for each heading
                if (aColumnTypes == null)
                    csvDataTable.Columns.Add(headings[i], typeof(string));
                else
                    csvDataTable.Columns.Add(headings[i], aColumnTypes[i]);
            }
        }
        else //if no headers just go for col1, col2 etc.
        {
            for (var i = 0; i < headings.Length; i++)
                //create arbitary column names
                csvDataTable.Columns.Add("col" + (i + 1), typeof(string));
        }

        //populate the DataTable
        for (var i = index; i < csvData.Length; i++)
            if (csvData[i] != "")
                try
                {
                    //test data
                    if (csvData[i].Split(separator.ToCharArray()[0]).Length != headings.Length && separator == ",")
                        continue;
                    //create new rows
                    var row = csvDataTable.NewRow();
                    var memoField = "";
                    for (var j = 0; j < headings.Length; j++)
                    {
                        var data = "";
                        if (separator == "\"" || separator == "\",\"")
                        {
                            data = Regex.Split(csvData[i], separator)[j].Replace("\"", "").Replace(",\r", "");
                        }
                        else if (csvData[i].Substring(0, 1) == "\"")
                        {
                            var iPos = csvData[i].IndexOf("\"");
                            if (iPos > -1) //because a memo field exists (between quotes)
                            {
                                if (memoField == "")
                                    memoField = csvData[i].Substring(iPos, csvData[i].IndexOf("\"", iPos + 1) - iPos)
                                        .Replace("\"", "");
                                csvData[i] = csvData[i].Replace("\"" + memoField + "\"", "#memo");
                                data = Regex.Split(csvData[i], separator)[j];
                                data = data.Replace("#memo", memoField);
                            }
                            else
                            {
                                data = Regex.Split(csvData[i], separator)[j];
                            }
                        }
                        else
                        {
                            data = Regex.Split(csvData[i], separator)[j];
                        }

                        //fill them
                        if (csvDataTable.Columns[j].DataType == typeof(int))
                            data = data.ToInt().ToString();
                        if (csvDataTable.Columns[j].DataType == typeof(double))
                            data = data.ToDouble().ToString();
                        if (csvDataTable.Columns[j].DataType == typeof(DateTime))
                            data = data.ToDateTime().ToString();

                        row[j] = data.Replace("\r", "");
                    }

                    //add rows to over DataTable
                    csvDataTable.Rows.Add(row);
                }
                catch (Exception ex)
                {
                    throw ex;
                }

        //return the CSV DataTable
        return csvDataTable;
    }


    public static SqlDataReader ExecuteDR(string Query, int _timeOut)
    {
        if (ConfigurationManager.AppSettings["ConnectionString"] == null)
            return null;
        var sConnectionString = getConnectionString();
        var oConn = new SqlConnection(sConnectionString);
        SqlDataReader dr = null;
        var oComm = new SqlCommand(Query);
        oConn.Open();
        oComm.Connection = oConn;
        oComm.CommandTimeout = _timeOut;
        try
        {
            dr = oComm.ExecuteReader(CommandBehavior.CloseConnection);
            return dr;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public static DataSet ExecuteSQL(string Query, int _timeOut)
    {
        if (ConfigurationManager.AppSettings["ConnectionString"] == null)
            return null;
        var sConnectionString = getConnectionString();
        var ds = new DataSet();
        var da = new SqlDataAdapter(Query, sConnectionString);
        da.SelectCommand.CommandTimeout = _timeOut;
        try
        {
            da.Fill(ds);
            return ds;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public static DataSet ExeuteSQL(string Query)
    {
        return ExecuteSQL(Query, 240);
    }

    public static void ExecuteNonQuery(string Query)
    {
        if (ConfigurationManager.AppSettings["ConnectionString"] == null)
            return;
        var sConnectionString = getConnectionString();
        using (var oConn = new SqlConnection(sConnectionString))
        {
            oConn.Open();
            var oComm = new SqlCommand(Query, oConn);
            oComm.ExecuteNonQuery();
        }
    }

    public static object ExecuteScalar(string Query)
    {
        if (ConfigurationManager.AppSettings["ConnectionString"] == null)
            return -1;

        object oRet = null;
        var sConnectionString = getConnectionString();
        var oConn = new SqlConnection(sConnectionString);
        oConn.Open();
        var oComm = new SqlCommand(Query, oConn);
        oRet = oComm.ExecuteScalar();
        oConn.Close();

        return oRet;
    }

    private static string getConnectionString()
    {
        var sRet = "";

        sRet = ConfigurationManager.AppSettings["ConnectionString"];
        /*try
        {
            ExeConfigurationFileMap map = new ExeConfigurationFileMap { ExeConfigFilename = "EXECONFIG_PATH" };
            Configuration config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
            AppSettingsSection sectionApps = config.GetSection("appSettings") as AppSettingsSection;
            if (!sectionApps.SectionInformation.IsProtected)
            {
                sectionApps.SectionInformation.ProtectSection("DataProtectionConfigurationProvider");
                config.Save();
            }
            if (sectionApps.Settings["ConnectionString"] != null)
                sRet = sectionApps.Settings["ConnectionString"].Value;
        }
        catch { sRet = ConfigurationManager.AppSettings["ConnectionString"].ToString(); }*/
        return sRet;
    }
}