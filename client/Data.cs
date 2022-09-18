﻿using System.Management;
using Microsoft.Win32.TaskScheduler;

namespace specify_client;

public class Data
{
    /**
     * Microsoft recommends using the CIM libraries (Microsoft.Management.Infrastructure)
     * However, some classes can't be called in CIM and only in WMI (e.g. Win32_PhysicalMemory)
     */
    public static List<Dictionary<string, object>> GetWmi(string cls, string ns = @"root\cimv2")
    {
        var scope = new ManagementScope(ns);
        scope.Connect();
        
        var query = new ObjectQuery("SELECT * FROM " + cls);
        var collection = new ManagementObjectSearcher(scope, query).Get();
        var res = new List<Dictionary<string, object>>();

        foreach (var i in collection)
        {
            var tempD = new Dictionary<string, object>();
            foreach (var j in i.Properties)
            {
                tempD[j.Name] = j.Value;
            }
            
            res.Add(tempD);
        }

        return res;
    }
    
    public static List<Microsoft.Win32.TaskScheduler.Task> GetTsStartupTasks()
    {
        var ts = new TaskService();
        return EnumTsTasks(ts.RootFolder);
    }

    private static List<Microsoft.Win32.TaskScheduler.Task> EnumTsTasks(TaskFolder fld)
    {
        var res = new List<Microsoft.Win32.TaskScheduler.Task>();
        foreach (var task in fld.Tasks)
        {
            if (
                !string.IsNullOrEmpty(task.Definition.RegistrationInfo.Author) &&
                task.Definition.RegistrationInfo.Author.StartsWith("Microsof")
                ) continue;

            if (task.State is not TaskState.Ready or TaskState.Running) continue;
            
            var triggers = task.Definition.Triggers;
            var triggersFlag = true;
            foreach (var trigger in triggers)
            {
                if (trigger.TriggerType is TaskTriggerType.Logon or TaskTriggerType.Boot)
                    triggersFlag = false;
            }

            if (triggersFlag) continue;

            res.Add(task);
        }
        
        foreach (var sfld in fld.SubFolders)
            res.AddRange(EnumTsTasks(sfld));

        return res;
    }

    public static string CimToISODate(string cim)
    {
        return ManagementDateTimeConverter.ToDateTime((string)Data.GetWmi("Win32_OperatingSystem")
            .First()["LastBootUpTime"]).ToString("yyyy-MM-ddTHH:mm:sszzz");
    }
}
