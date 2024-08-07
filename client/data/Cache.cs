﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Windows;

namespace specify_client.data;

public static partial class Cache
{
    [NonSerialized]
    public const int AF_INET = 2;    // IP_v4 = System.Net.Sockets.AddressFamily.InterNetwork

    [NonSerialized]
    public const int AF_INET6 = 23; // IP_v6 = System.Net.Sockets.AddressFamily.InterNetworkV6

    public static Dictionary<string, object> Os { get; private set; }
    public static Dictionary<string, object> Cs { get; private set; }
    public static IDictionary SystemVariables { get; private set; }
    public static IDictionary UserVariables { get; private set; }
    public static List<OutputProcess> RunningProcesses { get; private set; }
    public static List<Dictionary<string, object>> Services { get; private set; }
    public static List<InstalledApp> InstalledApps { get; private set; }
    public static List<Dictionary<string, object>> InstalledHotfixes { get; private set; }
    public static List<Dictionary<string, object>> BiosInfo { get; private set; }
    public static List<ScheduledTask> ScheduledTasks { get; private set; }
    public static List<ScheduledTask> WinScheduledTasks { get; private set; }
    public static List<StartupTask> StartupTasks { get; private set; }
    public static List<string> AvList { get; private set; }
    public static List<string> ExclusionPath { get; private set; }
    public static List<string> ExclusionExtension { get; private set; }
    public static List<string> ExclusionProcess { get; private set; }
    public static List<string> ExclusionIpAddresses { get; private set; }
    public static List<string> FwList { get; private set; }
    public static string HostsFile { get; private set; }
    public static string HostsFileHash { get; private set; }
    public static bool? UacEnabled { get; private set; }
    public static int? UacLevel { get; private set; }
    public static List<Dictionary<string, object>> NetAdapters { get; private set; }
    public static List<Dictionary<string, object>> NetAdapters2 { get; private set; }
    public static List<Dictionary<string, object>> IPRoutes { get; private set; }
    public static List<TCPConnection> TCPConnections { get; private set; }
    public static List<Dictionary<string, object>> UDPEndpoints { get; private set; }
    public static bool ReceiveSideScaling { get; private set; }
    public static Dictionary<string, string> AutoTuningLevelLocal { get; private set; }
    public static List<Browser> BrowserExtensions { get; private set; }
    public static string DefaultBrowser { get; private set; }
    public static bool WindowsOld { get; private set; }
    public static string Username => Environment.UserName;
    public static List<string> InstalledLanguagePacks { get; private set; }
    public static string SystemLanguage { get; private set; }

    // all the hardware stuff
    //each item in the list is a stick of ram
    public static List<RamStick> Ram { get; private set; }
    public static bool SMBiosRamInfo { get; private set; }
    public static List<DiskDrive> Disks { get; private set; }
    public static Dictionary<string, object> Cpu { get; private set; }
    public static List<Dictionary<string, object>> Gpu { get; private set; }
    public static Dictionary<string, object> Motherboard { get; private set; }
    public static List<Dictionary<string, object>> AudioDevices { get; private set; }
    public static Dictionary<string, object> Tpm { get; private set; }
    public static List<Dictionary<string, object>> Drivers { get; private set; }
    public static List<Dictionary<string, object>> Devices { get; private set; }
    public static List<TempMeasurement> Temperatures { get; private set; }
    public static List<BatteryData> Batteries { get; private set; }
    public static bool? SecureBootEnabled { get; private set; }
    public static List<IRegistryValue> ChoiceRegistryValues { get; private set; }
    public static int? LastBiosTime { get; private set; }
    public static List<Dictionary<string, object>> PowerProfiles { get; private set; }
    public static List<Dictionary<string, object>> WindowsStorePackages { get; private set; }
    public static List<string> MicroCodes { get; private set; }
    public static int RecentMinidumps { get; private set; }
    public static string DumpZip { get; private set; }
    public static bool? StaticCoreCount { get; private set; }
    public static bool? LimitedMemory { get; private set; }
    public static List<Monitor> MonitorInfo { get; private set; }
    public static List<EdidData> EdidData { get; private set; }
    public static bool? UsernameSpecialCharacters { get; private set; }
    public static int? OneDriveCommercialPathLength { get; private set; }
    public static int? OneDriveCommercialNameLength { get; private set; }

    private static readonly List<string> SystemProcesses = new List<string>()
    {
        "Memory Compression",
        "Registry",
        "System",
        "Idle",
        "Secure System"
    };
    public static Dictionary<string, object> PageFile { get; private set; }

    // The WriteSuccess flags allow Specified to easily ignore incomplete sections, avoiding fatal parsing issues.
    public static bool MainDataWriteSuccess { get; private set; } = false;
    public static bool SystemWriteSuccess { get; private set; } = false;
    public static bool HardwareWriteSuccess { get; private set; } = false;
    public static bool SecurityWriteSuccess { get; private set; } = false;
    public static bool NetworkWriteSuccess { get; private set; } = false;

    // Events/Errors
    public static List<UnexpectedShutdown> UnexpectedShutdowns { get; private set; } // Error 41s
    public static List<MachineCheckException> MachineCheckExceptions { get; private set; }
    public static List<PciWheaError> PciWheaErrors { get; private set; }
    public static List<WheaErrorRecordReadable> WheaErrorRecords { get; private set; }
    public static int UnexpectedShutdownCount { get; private set; }
    public static int MachineCheckExceptionCount { get; private set; }
    public static int PciWheaErrorCount { get; private set; }
    public static int WheaErrorRecordCount { get; private set; }
}