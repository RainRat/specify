﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using Newtonsoft.Json;

namespace specify_client
{
    /**
     * The big structure of all the things
     */
    public class Monolith
    {
        public MonolithMeta Meta;
        public MonolithBasicInfo BasicInfo;
        public IDictionary UserVariables;
        public IDictionary SystemVariables;
        public List<OutputProcess> RunningProcesses;

        /**
         * Debating making this static, because I don't like OOP
         */
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented) + Environment.NewLine;
        }

        public static void WriteFile()
        {
            Program.time.Stop();

            var serialized = MonolithCache.Monolith.Serialize();

            if (Settings.RedactUsername)
            {
                serialized = serialized.Replace(DataCache.Username, "[REDACTED]");
            }
            
            File.WriteAllText("specify_specs.json", serialized);
        }

        private static void CacheError(object thing)
        {
            throw new Exception("MonolithCache item doesn't exist: " + nameof(thing));
        }
    }
    
    public struct MonolithMeta
    {
        public long ElapsedTime;
    }
    
    public struct MonolithBasicInfo
    {
        public string Edition;
        public string Version;
        public string InstallDate;
        public string Uptime;
        public string Hostname;
        public string Username;
        public string Domain;
        public string BootMode;
        public string BootState;

        public static MonolithBasicInfo Create()
        {
            var os = DataCache.Os;
            var cs = DataCache.Cs;

            return new MonolithBasicInfo
            {
                Edition = (string) os["Caption"],
                Version = (string) os["Version"],
                InstallDate = Data.CimToIsoDate((string) os["InstallDate"]),
                Uptime = (DateTime.Now - ManagementDateTimeConverter.ToDateTime((string) os["LastBootUpTime"])).ToString("g"),
                Hostname = Dns.GetHostName(),
                Username = DataCache.Username,
                Domain = Environment.GetEnvironmentVariable("userdomain"),
                BootMode = Environment.GetEnvironmentVariable("firmware_type"),
                BootState = (string) cs["BootupState"]
            };
        }
    }

    public static class MonolithCache
    {
        public static Monolith Monolith { get; private set; }

        public static void AssembleCache()
        {
            Monolith = new Monolith
            {
                Meta = new MonolithMeta
                {
                    ElapsedTime = Program.time.ElapsedMilliseconds
                },
                BasicInfo = MonolithBasicInfo.Create(),
                UserVariables = DataCache.UserVariables,
                SystemVariables = DataCache.SystemVariables,
                RunningProcesses = DataCache.RunningProcesses
            };
        }
    }
}
