﻿using Microsoft.Win32;
using Newtonsoft.Json;
using specify_client.data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace specify_client;

/**
 * The big structure of all the things
 */

[Serializable]
public class Monolith
{
    // it will say these are never used, but they are serialized
    public string Version;

    public MonolithMeta Meta;
    public MonolithBasicInfo BasicInfo;

    // This being called "System" causes compiler issues with windows System objects.
    // Please change if possible, however it will cause Specified to fail.
    public MonolithSystem System;

    public MonolithHardware Hardware;
    public MonolithSecurity Security;
    public MonolithNetwork Network;
    /** For issues with gathering the data itself. No diagnoses based on the info will be made in this program. */
    public List<string> Issues;
    public string DebugLogText;

    public Monolith()
    {
        Version = Program.SpecifyVersion;
        Meta = new MonolithMeta
        {
            ElapsedTime = Program.Time.ElapsedMilliseconds
        };
        BasicInfo = new MonolithBasicInfo();
        System = new MonolithSystem();
        Hardware = new MonolithHardware();
        Security = new MonolithSecurity();
        Network = new MonolithNetwork();
        Issues = Cache.Issues;
    }

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented) + Environment.NewLine;
    }

    public static async Task Specificialize()
    {
        await DebugLog.LogEventAsync("Serialization starts");

        Program.Time.Stop();
        var m = new Monolith();
        await DebugLog.LogEventAsync("Monolith created");
        m.Meta.GenerationDate = DateTime.Now;
        m.DebugLogText = DebugLog.LogText;
        var serialized = m.Serialize();
        await DebugLog.LogEventAsync("Monolith serialized");

        if (Settings.RedactOneDriveCommercial)
        {
            try
            {
                var stringToRedact = (string)Cache.UserVariables["OneDriveCommercial"]; // The path containing the Commercial OneDrive
                stringToRedact = stringToRedact.Replace(@"\", @"\\"); // Changing a single \ to two \\ as that is how it shows up in the generated json
                serialized = serialized.Replace(stringToRedact, "[REDACTED]");
            }
            catch (Exception e)
            {
                m.Issues.Add("Commercial OneDrive redaction failed. This usually happens when Commerical OneDrive is not installed.");
                await DebugLog.LogEventAsync("Commercial OneDrive redaction failed. Serialization restarts." + e, DebugLog.Region.Misc, DebugLog.EventType.ERROR);
                Settings.RedactOneDriveCommercial = false;
                await Specificialize();
                return;
            }
            await DebugLog.LogEventAsync("Commercial OneDrive label redacted from report");
        }

        if (Settings.RedactUsername)
        {
            //C:\Users\Username -> C:\Users\[REDACTED]
            serialized = serialized.Replace($@"C:\\Users\\{Cache.Username}", @"C:\\Users\\[REDACTED]");

            //COMPUTERNAME\Username -> COMPUTERNAME\[REDACTED]
            serialized = serialized.Replace($@"{m.BasicInfo.Hostname}\\{Cache.Username}", $@"{m.BasicInfo.Hostname}\\[REDACTED]");

            //Redacts the username from BasicInfo
            serialized = serialized.Replace($@"""Username"": ""{Cache.Username}""", @"""Username"": ""[REDACTED]""");

            //DOMAIN\Username -> DOMAIN\[REDACTED]
            serialized = serialized.Replace($@"{m.BasicInfo.Domain}\\{Cache.Username}", $@"{m.BasicInfo.Domain}\\[REDACTED]");

            // ... for USERNAME -> ... for [REDACTED]
            serialized = serialized.Replace($@"for {Cache.Username}", $@"for [REDACTED]");

            await DebugLog.LogEventAsync("Username Redacted from report");
        }

        if (Settings.DontUpload)
        {
            var filename = "specify_specs.json";

            File.WriteAllText(filename, serialized);
            await DebugLog.LogEventAsync($"Report saved to {filename}");
            await DebugLog.StopDebugLog();
            ProgramDone(1);
            return;
        }
        String url = null;
        try
        {
            var requestTask = DoRequest(serialized);
            requestTask.Wait();
            url = requestTask.Result;
            if (url == null)
            {
                ProgramDone(2);
                throw new HttpRequestException("Upload failed. See previous log for details");
            }
        }
        catch (Exception e)
        {
            await DebugLog.LogEventAsync($"JSON upload failed. Retrying serialization to local file.", DebugLog.Region.Misc, DebugLog.EventType.ERROR);
            await DebugLog.LogEventAsync($"{e}", DebugLog.Region.Misc, DebugLog.EventType.INFORMATION);
            Settings.DontUpload = true;
            await Specificialize();
        }
        await DebugLog.LogEventAsync($"File uploaded successfully: {url}", DebugLog.Region.Misc);
        var t = new Thread(() =>
        {
            Clipboard.SetText(url);
            Process.Start(url);
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();

        t.Join();

        ProgramDone(0);

        // Program ends here.
        await DebugLog.StopDebugLog();
    }

    private static async Task<string> DoRequest(string str)
    {
        // const string specifiedUploadDomain = "http://localhost";
        // const string specifiedUploadEndpoint = "specified/upload.php";
        const string specifiedUploadDomain = "https://spec-ify.com";
        const string specifiedUploadEndpoint = "upload.php";
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{specifiedUploadDomain}/{specifiedUploadEndpoint}");
        request.Content = new StringContent(str);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        await DebugLog.LogEventAsync("File sent. Awaiting HTTP Response.");
        try
        {
            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                await DebugLog.LogEventAsync($"Unsuccessful HTTP Response: {response.StatusCode}", DebugLog.Region.Misc, DebugLog.EventType.ERROR);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nCould not upload. The file has been saved to specify_specs.json.");
                Console.WriteLine($"Please go to {specifiedUploadDomain} to upload the file manually.");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return null;
            }
            var location = response.Headers.Location.ToString();
            //Console.WriteLine(specifiedUploadDomain + location);
            return specifiedUploadDomain + location;
        }
        catch (Exception ex)
        {
            await DebugLog.LogEventAsync($"Unsuccessful HTTP Request. An Unexpected Exception occured", DebugLog.Region.Misc, DebugLog.EventType.ERROR);
            await DebugLog.LogEventAsync($"{ex}");
            return null;
        }
    }

    private static void CacheError(object thing)
    {
        throw new Exception("MonolithCache item doesn't exist: " + nameof(thing));
    }

    public static void ProgramDone(int noUpload)
    {
        switch (noUpload)
        {
            case 0:
                App.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var main = App.Current.MainWindow as Landing;
                    main.ProgramFinalize();
                }));
                break;

            case 1:
                App.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var main = App.Current.MainWindow as Landing;
                    main.ProgramFinalizeNoUpload();
                }));
                break;

            case 2:
                App.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var main = App.Current.MainWindow as Landing;
                    main.UploadFailed();
                }));
                break;

            case 3:
                App.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var main = App.Current.MainWindow as Landing;
                    main.ProgramFail();
                }));
                break;
        }
    }

    public struct MonolithMeta
    {
        public long ElapsedTime;
        public DateTime GenerationDate;
    }

    [Serializable]
    public class MonolithBasicInfo
    {
        public string Edition;
        public string Version;
        public string FriendlyVersion;
        public long InstallDate;
        public long Uptime;
        public string Hostname;
        public string Username;
        public string Domain;
        public string BootMode;
        public string BootState;
        public bool SMBiosRamInformation;

        public MonolithBasicInfo()
        {
            //win32 operating system class
            var os = Cache.Os;
            //win32 computersystem wmi class
            var cs = Cache.Cs;

            Edition = (string)os["Caption"];
            Version = (string)os["Version"];
            FriendlyVersion = Utils.GetRegistryValue<string>(Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "DisplayVersion") ?? Utils.GetRegistryValue<string>(Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "ReleaseId");
            InstallDate = DateTimeOffset.Parse(Utils.CimToIsoDate((string)os["InstallDate"]).ToString()).ToUnixTimeSeconds();
            Uptime = DateTimeOffset.Now.ToUnixTimeSeconds() - new DateTimeOffset(ManagementDateTimeConverter.ToDateTime((string)os["LastBootUpTime"])).ToUnixTimeSeconds();
            Hostname = Dns.GetHostName();
            Username = Cache.Username;
            Domain = Environment.GetEnvironmentVariable("userdomain");
            BootMode = Environment.GetEnvironmentVariable("firmware_type");
            BootState = (string)cs["BootupState"];
            SMBiosRamInformation = Cache.SMBiosRamInfo;
        }
    }

    [Serializable]
    public class MonolithSecurity
    {
        public List<string> AvList;
        public List<string> FwList;
        public bool? UacEnabled;
        public bool? SecureBootEnabled;
        public int? UacLevel;
        public Dictionary<string, object> Tpm;

        public MonolithSecurity()
        {
            AvList = Cache.AvList;
            FwList = Cache.FwList;
            UacEnabled = Cache.UacEnabled;
            SecureBootEnabled = Cache.SecureBootEnabled;
            Tpm = Cache.Tpm;
            UacLevel = Cache.UacLevel;
        }
    }

    [Serializable]
    public class MonolithHardware
    {
        public List<RamStick> Ram;
        public Dictionary<string, object> Cpu;
        public List<Dictionary<string, object>> Gpu;
        public Dictionary<string, object> Motherboard;
        public List<Dictionary<string, object>> AudioDevices;
        public List<data.Monitor> Monitors;
        public List<Dictionary<string, object>> Drivers;
        public List<Dictionary<string, object>> Devices;
        public List<Dictionary<string, object>> BiosInfo;
        public List<DiskDrive> Storage;
        public List<TempMeasurement> Temperatures;
        public List<BatteryData> Batteries;

        public MonolithHardware()
        {
            Ram = Cache.Ram;
            Cpu = Cache.Cpu;
            Gpu = Cache.Gpu;
            Motherboard = Cache.Motherboard;
            AudioDevices = Cache.AudioDevices;
            Monitors = Cache.MonitorInfo;
            Drivers = Cache.Drivers;
            Devices = Cache.Devices;
            BiosInfo = Cache.BiosInfo;
            Storage = Cache.Disks;
            Temperatures = Cache.Temperatures;
            Batteries = Cache.Batteries;
        }
    }

    [Serializable]
    public class MonolithSystem
    {
        public IDictionary UserVariables;
        public IDictionary SystemVariables;
        public List<OutputProcess> RunningProcesses;
        public List<Dictionary<string, object>> Services;
        public List<InstalledApp> InstalledApps;
        public List<Dictionary<string, object>> InstalledHotfixes;
        public List<ScheduledTask> ScheduledTasks;
        public List<ScheduledTask> WinScheduledTasks;
        public List<StartupTask> StartupTasks;
        public List<Dictionary<string, object>> PowerProfiles;
        public List<string> MicroCodes;
        public int RecentMinidumps;
        public string DumpZip;
        public bool? StaticCoreCount;
        public List<IRegistryValue> ChoiceRegistryValues;
        public bool? UsernameSpecialCharacters;
        public int? OneDriveCommercialPathLength;
        public int? OneDriveCommercialNameLength;
        public List<Browser> BrowserExtensions;
        public string DefaultBrowser;
        public IDictionary PageFile;

        public MonolithSystem()
        {
            UserVariables = Cache.UserVariables;
            SystemVariables = Cache.SystemVariables;
            RunningProcesses = Cache.RunningProcesses;
            Services = Cache.Services;
            InstalledApps = Cache.InstalledApps;
            InstalledHotfixes = Cache.InstalledHotfixes;
            ScheduledTasks = Cache.ScheduledTasks;
            WinScheduledTasks = Cache.WinScheduledTasks;
            StartupTasks = Cache.StartupTasks;
            PowerProfiles = Cache.PowerProfiles;
            MicroCodes = Cache.MicroCodes;
            RecentMinidumps = Cache.RecentMinidumps;
            DumpZip = Cache.DumpZip;
            StaticCoreCount = Cache.StaticCoreCount;
            ChoiceRegistryValues = Cache.ChoiceRegistryValues;
            UsernameSpecialCharacters = Cache.UsernameSpecialCharacters;
            OneDriveCommercialPathLength = Cache.OneDriveCommercialPathLength;
            OneDriveCommercialNameLength = Cache.OneDriveCommercialNameLength;
            BrowserExtensions = Cache.BrowserExtensions;
            DefaultBrowser = Cache.DefaultBrowser;
            PageFile = Cache.PageFile;
        }
    }

    [Serializable]
    public class MonolithNetwork
    {
        public List<Dictionary<string, object>> Adapters;
        public List<Dictionary<string, object>> Adapters2;
        public List<Dictionary<string, object>> Routes;
        public List<NetworkConnection> NetworkConnections;
        public string HostsFile;
        public string HostsFileHash;

        public MonolithNetwork()
        {
            Adapters = Cache.NetAdapters;
            Adapters2 = Cache.NetAdapters2;
            Routes = Cache.IPRoutes;
            NetworkConnections = Cache.NetworkConnections;
            HostsFile = Cache.HostsFile;
            HostsFileHash = Cache.HostsFileHash;
        }
    }

    public static class MonolithCache
    {
        public static Monolith Monolith { get; set; }

        public static void AssembleCache()
        {
            Monolith = new Monolith();
        }
    }
}