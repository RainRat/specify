using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace specify_client.data;

public static partial class Cache
{
    public static async System.Threading.Tasks.Task MakeNetworkData()
    {
        try
        {
            DebugLog.Region region = DebugLog.Region.Networking;
            await DebugLog.StartRegion(region);
            NetAdapters = Utils.GetWmi("Win32_NetworkAdapterConfiguration",
                "Description, DHCPEnabled, DHCPServer, DNSDomain, DNSDomainSuffixSearchOrder, DNSHostName, "
                + "DNSServerSearchOrder, IPEnabled, IPAddress, IPSubnet, DHCPLeaseObtained, DHCPLeaseExpires, "
                + "DefaultIPGateway, MACAddress, InterfaceIndex");
            NetAdapters2 = Utils.GetWmi("MSFT_NetAdapter",
                "InterfaceIndex,InterfaceDescription,ConnectorPresent,InterfaceType,NdisPhysicalMedium",
                @"root\standardcimv2");
            IPRoutes = Utils.GetWmi("Win32_IP4RouteTable",
                "Description, Destination, Mask, NextHop, Metric1, InterfaceIndex");
            await DebugLog.LogEventAsync("Networking WMI Information Retrieved.", region);

            HostsFile = GetHostsFile();
            HostsFileHash = GetHostsFileHash();
            await DebugLog.LogEventAsync("Hosts file retrieved.", region);

            NetworkConnections = GetNetworkConnections();
            await DebugLog.LogEventAsync("NetworkConnections Information retrieved.", region);

            await DebugLog.EndRegion(DebugLog.Region.Networking);
            // Uncomment the block below to run a traceroute to Google's DNS
            /*var NetStats = await GetNetworkRoutes("8.8.8.8", 1000);
            for (int i = 0; i < NetStats.Address.Count; i++)
            {
                Console.WriteLine($"{i}: {NetStats.Address[i]} --- Lat: {NetStats.AverageLatency[i]} --- PL: {NetStats.PacketLoss[i]}");
            }*/
        }
        catch (Exception ex)
        {
            /*await DebugLog.LogEventAsync("UNEXPECTED FATAL EXCEPTION", DebugLog.Region.Networking, DebugLog.EventType.ERROR);
            await DebugLog.LogEventAsync($"{ex}", DebugLog.Region.Networking);
            Environment.Exit(-1);*/
            await DebugLog.LogFatalError($"{ex}", DebugLog.Region.Networking);
        }
    }

    private static IEnumerable<IPAddress> GetTraceroute(string ipAddress, int timeout = 10000, int maxTTL = 30, int bufferSize = 100)
    {
        // Cap off the TTL to not overdo the basal traceroute.
        if (maxTTL > 30)
        {
            maxTTL = 30;
        }

        var buffer = new byte[bufferSize];
        new Random().NextBytes(buffer);

        using var pingTool = new Ping();

        foreach (var i in Enumerable.Range(1, maxTTL))
        {
            var pingOptions = new PingOptions(i, true);
            var reply = pingTool.Send(ipAddress, timeout, buffer, pingOptions);

            if (reply.Status is IPStatus.Success or IPStatus.TtlExpired)
            {
                yield return reply.Address;
            }
            if (reply.Status != IPStatus.TtlExpired && reply.Status != IPStatus.TimedOut)
            {
                break;
            }
        }
    }

    private static List<NetworkConnection> GetNetworkConnections()
    {
        DateTime start = DateTime.Now;
        DebugLog.LogEvent("GetNetworkConnections() Started.", DebugLog.Region.Networking);
        List<NetworkConnection> connectionsList = new();
        var connections = GetAllTCPv4Connections();
        foreach (var connection in connections)
        {
            NetworkConnection conn = new();
            int port = 0;
            port += connection.localPort[0] << 8;
            port += connection.localPort[1];

            int rport = 0;
            rport += connection.remotePort[0] << 8;
            rport += connection.remotePort[1];

            var la = connection.localAddr;

            // [CLEANUP]: This can be simplified with BitConverter
            uint localAddr1 = la % 256;
            la = la / 256;
            uint localAddr2 = la % 256;
            la = la / 256;
            uint localAddr3 = la % 256;
            uint localAddr4 = la / 256;

            var ra = connection.remoteAddr;
            uint remoteAddr1 = ra % 256;
            ra = ra / 256;
            uint remoteAddr2 = ra % 256;
            ra = ra / 256;
            uint remoteAddr3 = ra % 256;
            uint remoteAddr4 = ra / 256;

            conn.LocalIPAddress = $"{localAddr1}.{localAddr2}.{localAddr3}.{localAddr4}";
            conn.LocalPort = port;
            conn.RemoteIPAddress = $"{remoteAddr1}.{remoteAddr2}.{remoteAddr3}.{remoteAddr4}";
            conn.RemotePort = rport;
            conn.OwningPID = connection.owningPid;

            connectionsList.Add(conn);
        }
        var v6connections = GetAllTCPv6Connections();
        foreach (var connection in v6connections)
        {
            NetworkConnection conn = new();
            int port = 0;
            port += connection.localPort[0] << 8;
            port += connection.localPort[1];

            int rport = 0;
            rport += connection.remotePort[0] << 8;
            rport += connection.remotePort[1];

            var la = connection.localAddr;
            var ra = connection.remoteAddr;

            var localAddr = "";
            var remoteAddr = "";
            for (int i = 0; i < la.Length; i++)
            {
                if (i % 2 == 0)
                {
                    if (i != 0)
                    {
                        localAddr += ":";
                    }
                    if (la[i] == 0x00 && la[i + 1] == 0x00)
                    {
                        i++;
                        continue;
                    }
                }
                byte[] annoyingArrayAssignment = new byte[1] { la[i] };
                localAddr += BitConverter.ToString(annoyingArrayAssignment);
            }
            for (int i = 0; i < ra.Length; i++)
            {
                if (i % 2 == 0)
                {
                    if (i != 0)
                    {
                        remoteAddr += ":";
                    }
                    if (ra[i] == 0x00 && ra[i + 1] == 0x00)
                    {
                        i++;
                        continue;
                    }
                }
                byte[] annoyingArrayAssignment = new byte[1] { ra[i] };
                remoteAddr += BitConverter.ToString(annoyingArrayAssignment);
            }

            conn.LocalIPAddress = localAddr;
            conn.LocalPort = port;
            conn.RemoteIPAddress = remoteAddr;
            conn.RemotePort = rport;
            conn.OwningPID = connection.owningPid;

            connectionsList.Add(conn);
        }
        DebugLog.LogEvent($"GetNetworkConnections() completed. Total Runtime {(DateTime.Now - start).TotalMilliseconds}", DebugLog.Region.Networking);
        return connectionsList;
    }

    private static List<Interop.MIB_TCPROW_OWNER_PID> GetAllTCPv4Connections()
    {
        return GetTCPConnections<Interop.MIB_TCPROW_OWNER_PID, Interop.MIB_TCPTABLE_OWNER_PID>(AF_INET);
    }

    private static List<Interop.MIB_TCP6ROW_OWNER_PID> GetAllTCPv6Connections()
    {
        return GetTCPConnections<Interop.MIB_TCP6ROW_OWNER_PID, Interop.MIB_TCP6TABLE_OWNER_PID>(AF_INET6);
    }

    private static List<IPR> GetTCPConnections<IPR, IPT>(int ipVersion)
    {
        IPR[] tableRows;
        int buffSize = 0;
        var dwNumEntriesField = typeof(IPT).GetField("dwNumEntries");

        uint ret;
        IntPtr tcpTablePtr = Marshal.AllocHGlobal(buffSize);

        try
        {
            ret = Interop.GetExtendedTcpTable(tcpTablePtr, ref buffSize, true, ipVersion, Interop.TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL);
            if (ret != 0) return new List<IPR>();

            IPT table = (IPT)Marshal.PtrToStructure(tcpTablePtr, typeof(IPT));
            int rowStructSize = Marshal.SizeOf(typeof(IPR));
            uint numEntries = (uint)dwNumEntriesField.GetValue(table);

            tableRows = new IPR[numEntries];

            IntPtr rowPtr = (IntPtr)((long)tcpTablePtr + 4);
            for (int i = 0; i < numEntries; i++)
            {
                IPR tcpRow = (IPR)Marshal.PtrToStructure(rowPtr, typeof(IPR));
                tableRows[i] = tcpRow;
                rowPtr = (IntPtr)((long)rowPtr + rowStructSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(tcpTablePtr);
        }
        return tableRows != null ? tableRows.ToList() : new List<IPR>();
    }

    private static async System.Threading.Tasks.Task<int> GetLatency(string ipAddress, int timeout, byte[] buffer, PingOptions pingOptions)
    {
        try
        {
            var pingReply = await new Ping().SendPingAsync(ipAddress, timeout, buffer, pingOptions);

            if (pingReply != null)
            {
                if (pingReply.Status != IPStatus.Success)
                {
                    return -1;
                }
                else
                {
                    return (int)pingReply.RoundtripTime;
                }
            }
            else
            {
                return -1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return -2;
        }
    }

    private static async System.Threading.Tasks.Task<(int, double)> GetHostStats(string ipAddress, int timeout = 10000, int pingCount = 100)
    {
        var pingOptions = new PingOptions();

        var data = "meaninglessdatawithalotofletters"; // 32 letters in total.
        var buffer = Encoding.ASCII.GetBytes(data);

        var failedPings = 0;
        var errors = 0;
        var latencySum = 0;
        var statTasks = new List<System.Threading.Tasks.Task<int>>();

        for (int i = 0; i < pingCount; i++)
        {
            var task = System.Threading.Tasks.Task.Run(() => GetLatency(ipAddress, timeout, buffer, pingOptions));
            statTasks.Add(task);
        }
        await System.Threading.Tasks.Task.WhenAll(statTasks);
        foreach (var task in statTasks)
        {
            switch (task.Result)
            {
                case -1:
                    failedPings++;
                    break;

                case -2:
                    errors++;
                    break;

                default:
                    latencySum += task.Result;
                    break;
            }
        }
        var averageLatency = latencySum / pingCount;
        var packetLoss = failedPings / (double)pingCount;
        if (errors > 0)
        {
            Console.WriteLine($"{ipAddress} - ERRORS: {errors}");
        }
        return (averageLatency, packetLoss);
    }

    private static string GetHostsFile()
    {
        var hostsFile = "";
        try
        {
            foreach (var str in File.ReadAllLines(@"C:\Windows\System32\drivers\etc\hosts"))
            {
                hostsFile += $"{str}\n";
            }
        }
        catch (FileNotFoundException)
        {
            Issues.Add("Hosts file not found.");
            return "";
        }
        return hostsFile;
    }

    public static string GetHostsFileHash()
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(@"C:\Windows\System32\drivers\etc\hosts");
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "");
    }

    private static async System.Threading.Tasks.Task<NetworkRoute> GetNetworkRoutes(string ipAddress, int pingCount = 100, int timeout = 10000, int bufferSize = 100)
    {
        var addressList = GetTraceroute(ipAddress, timeout, 30, bufferSize);
        var networkRoute = new NetworkRoute();

        foreach (var address in addressList)
        {
            networkRoute.Address.Add(address.ToString());
            (int Latency, double Loss) hostStats = await GetHostStats(ipAddress, timeout, pingCount);
            networkRoute.AverageLatency.Add(hostStats.Latency);
            networkRoute.PacketLoss.Add(hostStats.Loss);
        }

        return networkRoute;
    }
}