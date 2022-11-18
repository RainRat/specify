﻿// everything that handles actual data collection to be passed over to the monolith
using System;
using System.Collections;
using System.Management;
using Microsoft.Win32.TaskScheduler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using Action = System.Action;

namespace specify_client
{
    public class Data
    {
        /**
         * Microsoft recommends using the CIM libraries (Microsoft.Management.Infrastructure)
         * However, some classes can't be called in CIM and only in WMI (e.g. Win32_PhysicalMemory)
         */
        public static List<Dictionary<string, object>> GetWmi(string cls, string selected = "*", string ns = @"root\cimv2")
        {
            var scope = new ManagementScope(ns);
            scope.Connect();
            
            var query = new ObjectQuery($"SELECT {selected} FROM {cls}");
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
        
        /**
         * Gets tasks from Task Scheduler that satisfy all of the following conditions:
         *  - Author isn't Microsoft
         *  - State is Ready or Running
         *  - Triggered at boot or login
         */
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

                if (task.State != TaskState.Ready || task.State != TaskState.Running) continue;
                
                var triggers = task.Definition.Triggers;
                var triggersFlag = true;
                foreach (var trigger in triggers)
                {
                    if (trigger.TriggerType == TaskTriggerType.Logon || trigger.TriggerType == TaskTriggerType.Boot)
                        triggersFlag = false;
                }

                if (triggersFlag) continue;

                res.Add(task);
            }
            
            foreach (var sfld in fld.SubFolders)
                res.AddRange(EnumTsTasks(sfld));

            return res;
        }

        /**
         * Convert a CIM date (what would be gotten from WMI) into an ISO date
         * See https://learn.microsoft.com/en-us/windows/win32/wmisdk/cim-datetime
         */
        public static string CimToIsoDate(string cim)
        {
            return DateTimeToIsoDate(ManagementDateTimeConverter.ToDateTime(cim));
        }

        public static string DateTimeToIsoDate(DateTime d)
        {
            return d.ToString("yyyy-MM-ddTHH:mm:sszzz");
        }
    }

    /**
     * Cache of values so they don't have to be called multiple times
     */
    public static class DataCache
    {
        public static Dictionary<string, object> Os { get; private set; }
        public static Dictionary<string, object> Cs { get; private set; }
        public static IDictionary SystemVariables { get; private set; }
        public static IDictionary UserVariables { get; private set; }
        public static List<OutputProcess> RunningProcesses { get; private set; }
        public static List<Dictionary<string, object>> Services { get; private set; }
        public static List<Dictionary<string, object>> InstalledApps { get; private set; }
        public static List<string> AvList { get; private set; }
        public static List<string> FwList { get; private set; }
        public static string HostsFile { get; private set;  }
        public static bool UacEnabled { get; private set; }
        public static List<Dictionary<string, object>> NetAdapters { get; private set; }

        public static string Username => Environment.UserName;
        // all the hardware stuff
        //each item in the list is a stick of ram
        public static List<Dictionary<String, Object>> Ram {get; private set;}
        public static Dictionary<String, Object> Cpu {get; private set;}
        public static List<Dictionary<String, Object>> Gpu {get; private set;}
        public static Dictionary<String, Object> Motherboard {get; private set;}

        public static void MakeMainData()
        {
            Os = Data.GetWmi("Win32_OperatingSystem").First();
            Cs = Data.GetWmi("Win32_ComputerSystem").First();

            Ram = Data.GetWmi("Win32_PhysicalMemory", "DeviceLocator, Capacity, ConfiguredClockSpeed, Manufacturer, PartNumber, SerialNumber");
            Cpu = Data.GetWmi("Win32_Processor", "CurrentClockSpeed, Manufacturer, Name, SocketDesignation").First();
            Gpu = Data.GetWmi("Win32_VideoController", "Description, AdapterRam, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, CurrentBitsPerPixel");
            Motherboard = Data.GetWmi("Win32_BaseBoard", "Manufacturer, Product, SerialNumber").First();
        }

        public static void DummyTimer()
        {
            Thread.Sleep(5000);
        }

        public static void MakeSystemData()
        {
            SystemVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            UserVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
            Services = Data.GetWmi("Win32_Service", "Name, Caption, PathName, StartMode, State");
            InstalledApps = Data.GetWmi("Win32_Product", "Name, Version");
            HostsFile = System.IO.File.ReadAllText(@"C:\Windows\system32\drivers\etc\hosts");

            RunningProcesses = new List<OutputProcess>();
            var rawProcesses = Process.GetProcesses();
            foreach (var rawProcess in rawProcesses)
            {
                double cpuPercent = -1.0; // TODO: make this actually work properly
                var exePath = "";
                /*try
                {
                    var counter = new PerformanceCounter("Process", "% Processor Time", rawProcess.ProcessName);
                    counter.NextValue();
                    Thread.Sleep(100);
                    cpuPercent = counter.NextValue();
                }
                catch (Win32Exception e)
                {
                    cpuPercent = -1;
                }*/

                try
                {
                    exePath = rawProcess.MainModule?.FileName;
                }
                catch (Win32Exception e)
                {
                    exePath = "<unknown>";
                }

                RunningProcesses.Add(new OutputProcess
                {
                    ProcessName = rawProcess.ProcessName,
                    ExePath = exePath,
                    Id = rawProcess.Id,
                    WorkingSet = rawProcess.WorkingSet64,
                    CpuPercent = cpuPercent
                });
            }
        }

        public static void MakeSecurityData()
        {
            AvList = Data.GetWmi("AntivirusProduct", "displayName", @"root\SecurityCenter2")
                .Select(x => (string)x["displayName"]).ToList();
            FwList = Data.GetWmi("FirewallProduct", "displayName", @"root\SecurityCenter2")
                .Select(x => (string)x["displayName"]).ToList();


            var key = Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");

            if (key != null)
            {
                var enableLua = key.GetValue("EnableLUA");
                UacEnabled = (int)enableLua == 1;
            }
        }

        public static void MakeNetworkData()
        {
            NetAdapters = Data.GetWmi("Win32_NetworkAdapterConfiguration", 
                "Description, DHCPEnabled, DHCPServer, DNSDomain, DNSDomainSuffixSearchOrder, DNSHostName, "
                    + "DNSServerSearchOrder, IPEnabled, IPAddress, IPSubnet, DHCPLeaseObtained, DHCPLeaseExpires, "
                    + "DefaultIPGateway");
        }
    }

    public class OutputProcess
    {
        public string ProcessName;
        public string ExePath;
        public int Id;
        public long WorkingSet;
        public double CpuPercent;
    }

    public class WMIService
    {
        public string Name;
        public string Caption;
        public string PathName;
        public string State;
        public string StartMode;
    }
}
