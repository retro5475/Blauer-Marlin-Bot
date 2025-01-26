﻿using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.Timers;
using System.Collections.Generic;
using Timer = System.Timers.Timer;
using Serilog;
using System.Reactive;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Discord.Commands;
using System.Reflection.Metadata;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using log4net.Plugin;

class Program
{
    private static List<Assembly> _loadedPlugins = new List<Assembly>();
    private static DiscordSocketClient _client;
    private static SocketTextChannel _channel;
    private static CommandService _commands;
    private static IServiceProvider _services;
    private static Timer _timer;
    private static IUserMessage _currentMessage;
    private static Dictionary<string, bool> _regionPingStatus;

    private static readonly string ConfigFilePath = "files/regionPingStatus.json";

    private static readonly List<RegionInfo> _regions = new List<RegionInfo>
    {
        new RegionInfo
        {
            Name = "USA",
            Servers = new List<ServerInfo>
            {
                new ServerInfo { Name = "Aether: LOGIN", IP = "204.2.29.80" }
            }
        },
        new RegionInfo
        {
            Name = "Europe",
            Servers = new List<ServerInfo>
            {
                new ServerInfo { Name = "🌼Chaos: LOGIN", IP = "80.239.145.6" }, //neolobby06.ffxiv.com > 80.239.145.6
                new ServerInfo { Name = "🌸Light: LOGIN", IP = "80.239.145.7" }, // neolobby07.ffxiv.com > 80.239.145.7
                new ServerInfo { Name = "🌸Light: Alpha", IP = "80.239.145.91" },
                new ServerInfo { Name = "🌸Light: Lich", IP = "80.239.145.92" },
                new ServerInfo { Name = "🌸Light: Odin", IP = "80.239.145.93" },
                new ServerInfo { Name = "🌸Light: Phönx", IP = "80.239.145.94" },
                new ServerInfo { Name = "🌸Light: Raiden", IP = "80.239.145.95" },
                new ServerInfo { Name = "🌸Light: Shiva", IP = "80.239.145.96" },
                new ServerInfo { Name = "🌸Light: Twin", IP = "80.239.145.97" }
                //new ServerInfo { Name = "🌸Light:   Zodi", IP = "80.239.145.90" }


            }
        },
        new RegionInfo
        {
            Name = "Japan",
            Servers = new List<ServerInfo>
            {
                //new ServerInfo { Name = "Elemental Lobby", IP = "119.252.37.61" },
                //new ServerInfo { Name = "Elemental Lobby", IP = "119.252.37.62" },
                //new ServerInfo { Name = "Elemental Lobby", IP = "119.252.37.63" },
                //new ServerInfo { Name = "Elemental Lobby", IP = "119.252.37.64" },
                //new ServerInfo { Name = "Elemental Lobby", IP = "119.252.37.65" },
                //new ServerInfo { Name = "Elemental Lobby", IP = "119.252.37.66" },
                //new ServerInfo { Name = "Elemental Lobby", IP = "119.252.37.67" },
                //new ServerInfo { Name = "Elemental Lobby", IP = "119.252.37.68" },
                //new ServerInfo { Name = "Elemental Lobby", IP = "119.252.37.69" },
                //new ServerInfo { Name = "ELEM: LOGIN", IP = "119.252.37.70" }
            }
        }
    };

    static async Task Main(string[] args)
    {
        _client = new DiscordSocketClient();
        _commands = new CommandService();
        CheckAndCreateDirectories();
        

        // Initialize Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        
        try
        {
            Log.Information("Bot starting...");
            await StartBotAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An unhandled exception occurred during startup.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    

    private static async Task StartBotAsync()
    {
        try
        {
            Log.Information("Starting the bot...");

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged
            });

            _client.Log += logMessage =>
            {
                Log.Information($"Discord Log: {logMessage}");
                return Task.CompletedTask;
            };

            _client.Ready += ReadyAsync;
            _client.SlashCommandExecuted += HandleSlashCommandAsync;

            await _client.SetStatusAsync(UserStatus.DoNotDisturb);
            await _client.SetGameAsync("N/A");

            var token = "MTMzMTcxMjU2MDY4NDIwODIzOQ.G4ydyX.jirNnSH_G6cxyubz6uXFLa6gncuKdYGp6HXDBk"; 
            await _client.LoginAsync(TokenType.Bot, token);
            await PluginLoader.LoadAndExecutePluginsAsync(_client);

            await _client.StartAsync();

            _timer = new Timer(60000); // 1 Minute
            _timer.Elapsed += async (sender, e) => await PingServers();
            _timer.Start();

            Log.Information("Bot started. Waiting for commands...");
            await Task.Delay(-1);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred during bot startup.");
        }
    }

    private static async Task ReadyAsync()
    {
        try
        {
            Log.Information("Bot is ready!");
            LoadRegionPingStatus();
            await _client.SetGameAsync("FFXIV Server Status");
            Log.Information("Registering commands...");

            await commands.RegisterSlashCommands(_client);
            var channelId = ulong.Parse("1331663013719048243");
            _channel = (SocketTextChannel)_client.GetChannel(channelId);
            _currentMessage = await _channel.SendMessageAsync(embed: CreateEmbed("Initializing server status..."));
            Log.Information("Ready event handled.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling ReadyAsync.");
        }
    }

    private static Embed CreateEmbed(string description, Color? color = null)
    {
        Log.Information("Creating embed with description: {Description}", description);
        return new EmbedBuilder()
            .WithTitle("FFXIV Server Status")
            .WithDescription(description)
            .WithColor(color ?? Color.Blue)
            .WithTimestamp(DateTimeOffset.Now)
            .Build();
    }

    private static async Task PingServers()
    {
        try
        {
            Log.Information("Pinging servers...");

            var embed = new EmbedBuilder()
                .WithTitle("FFXIV Server Status")
                .WithColor(Color.Blue)
                .WithImageUrl("https://lds-img.finalfantasyxiv.com/h/e/2a9GxMb6zta1aHsi8u-Pw9zByc.jpg")
                .WithTimestamp(DateTimeOffset.Now);

            Dictionary<string, bool> regionStatuses = new();
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    regionStatuses = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new Dictionary<string, bool>();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error reading the region status configuration file.");
                }
            }
            else
            {
                Log.Warning($"Config file {ConfigFilePath} not found. Defaulting to active for all regions.");
            }

            // Schleife über alle Regionen
            foreach (var region in _regions)
            {
                // Region prüfen, ob sie in der Datei aktiv ist
                if (!regionStatuses.TryGetValue(region.Name.ToLower(), out var isActive) || !isActive)
                {
                    Log.Information($"Skipping region {region.Name} (inactive).");
                    continue;
                }

                string table = "```\nServer         | Ping (ms) | Loss | Status\n" +
                               " --------------|-----------|------|-------\n";

                foreach (var server in region.Servers)
                {
                    string status, responseTime;
                    string statusEmoji;
                    string packetLoss = "0%";  // Default packet loss

                    int successfulPings = 0;
                    int totalPings = 5;  // Number of pings to test for packet loss

                    try
                    {
                        for (int i = 0; i < totalPings; i++)
                        {
                            using var ping = new Ping();
                            var reply = await ping.SendPingAsync(server.IP);

                            if (reply.Status == IPStatus.Success)
                            {
                                successfulPings++;
                            }
                        }

                        // Calculate packet loss as percentage
                        int loss = totalPings - successfulPings;
                        packetLoss = (loss > 0) ? $"{(loss * 100 / totalPings)}%" : "0%";

                        // Ping last time for roundtrip time
                        using var lastPing = new Ping();
                        var finalReply = await lastPing.SendPingAsync(server.IP);

                        if (finalReply.Status == IPStatus.Success)
                        {
                            status = "Online";
                            responseTime = finalReply.RoundtripTime.ToString();
                            statusEmoji = "🟢"; // Green circle for online
                        }
                        else
                        {
                            status = "Offline";
                            responseTime = "N/A";
                            statusEmoji = "🔴"; // Red circle for offline
                        }
                    }
                    catch (Exception ex)
                    {
                        status = "Error";
                        responseTime = "N/A";
                        statusEmoji = "⚪"; // White circle for error
                        Log.Error(ex, $"Error pinging server {server.Name} ({server.IP}).");
                    }

                    table += $"{server.Name.PadRight(15)}| {responseTime.PadLeft(5)} ms  | {packetLoss.PadLeft(5)}| {status} {statusEmoji}\n";
                }

                table += "```";
                embed.AddField(region.Name, table, false);
            }

            if (_currentMessage != null)
            {
                Log.Information("Modifying current message with new embed.");
                await _currentMessage.ModifyAsync(msg => msg.Embed = embed.Build());
            }
            else
            {
                Log.Information("Sending new message with embed.");
                _currentMessage = await _channel.SendMessageAsync(embed: embed.Build());
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error pinging servers.");
        }
    }



    static void CheckAndCreateDirectories()
    {
        if (!Directory.Exists("files"))
            Directory.CreateDirectory("files");
        if (!Directory.Exists("plugins"))
            Directory.CreateDirectory("plugins");

    }


    public class PluginLoader
    {
        private static readonly List<Assembly> _loadedPlugins = new List<Assembly>();

        public static async Task LoadAndExecutePluginsAsync(DiscordSocketClient _client)
        {
            _loadedPlugins.Clear();

            var pluginFiles = Directory.GetFiles("plugins", "*.cs"); // Alle .cs-Dateien im Ordner "plugins"
            foreach (var file in pluginFiles)
            {
                try
                {
                    Console.WriteLine($"Loading plugin: {Path.GetFileName(file)}");

                    // Plugin-Code einlesen
                    var code = File.ReadAllText(file);

                    // ScriptOptions konfigurieren
                    var scriptOptions = ScriptOptions.Default
                        .WithReferences(AppDomain.CurrentDomain.GetAssemblies())
                        .WithImports(
                            "System",
                            "System.IO",
                            "System.Linq",
                            "System.Collections.Generic",
                            "Discord",
                            "Discord.WebSocket",
                            "Discord.Commands",
                            "System.Threading.Tasks"
                        );

                    // Script erstellen und kompilieren
                    var script = CSharpScript.Create(code, scriptOptions);
                    var compilation = script.GetCompilation();

                    using var ms = new MemoryStream();
                    var result = compilation.Emit(ms); // Code kompilieren und in MemoryStream speichern

                    if (result.Success)
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        var assembly = Assembly.Load(ms.ToArray()); // Assembly aus Stream laden

                        _loadedPlugins.Add(assembly); // Geladene Assembly zur Liste hinzufügen
                        Console.WriteLine($"Loaded plugin: {Path.GetFileName(file)}");
                    }
                    else
                    {
                        Console.WriteLine($"Error compiling plugin {file}: {string.Join(", ", result.Diagnostics)}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading plugin {file}: {ex.Message}");
                }
            }

            // Starte die Plugins in einem separaten Task
            _ = Task.Run(async () =>
            {
                foreach (var plugin in _loadedPlugins)
                {
                    await ExecutePluginAsync(plugin, _client);
                }
            });
        }

        private static async Task ExecutePluginAsync(Assembly assembly, DiscordSocketClient _client)
        {
            try
            {
                // Alle Typen in der Assembly durchsuchena
                foreach (var type in assembly.GetTypes())
                {
                    if (type.GetMethod("RunAsync") != null)
                    {
                        // Hier wird die Instanz mit dem richtigen Konstruktor erstellt und der Client übergeben
                        var constructor = type.GetConstructor(new[] { typeof(DiscordSocketClient) });

                        if (constructor != null)
                        {
                            // Instanz mit dem Konstruktor erstellen
                            var instance = constructor.Invoke(new object[] { _client });

                            var method = type.GetMethod("RunAsync");

                            if (method != null)
                            {
                                // Plugin in einem separaten Task ausführen, um Blockierungen zu verhindern
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        Console.WriteLine($"Executing plugin: {type.Name}");
                                        var task = method.Invoke(instance, null) as Task;

                                        if (task != null)
                                        {
                                            await task; // Ausführung der Methode abwarten
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error executing plugin {type.Name}: {ex.Message}");
                                    }
                                });
                            }
                        }
                        else
                        {
                            Console.WriteLine($"No valid constructor found for {type.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing plugin: {ex.Message}");
            }
        }
    }





    private static async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        try
        {
            Log.Information("Handling slash command: {CommandName}", command.CommandName);

            switch (command.CommandName)
            {
                case "help":
                    Log.Information("User requested help.");
                    await command.RespondAsync(embed: CreateEmbed("Available commands:\n\n" +
                        "`/europe` - Toggles Europe server ping.\n" +
                        "`/usa` - Toggles North America server ping.\n" +
                        "`/japan` - Toggles Japan server ping.\n" +
                        "`/status` - Displays the current ping status.\n" +
                        "`/reload` - Reloads server status.\n" +
                        "`/ban` - Bans a user.\n" +
                        "`/unban` - Unbans a user.\n" +
                        "`/kick` - Kicks a user.\n" +
                        "`/mute` - Mutes a user.\n" +
                        "`/unmute` - Unmutes a user.\n" +
                        "`/warn` - Warns a user.\n" +
                        "`/clearwarns` - Clears a user's warnings.\n" +
                        "`/userinfo` - Displays user information.\n" +
                        "`/setnickname` - Sets a user's nickname.\n" +
                        "`/lockdown` - Locks the channel temporarily.\n" +
                        "`/unlock` - Unlocks the channel.\n" +
                        "`/addrole` - Adds a role to a user.\n" +
                        "`/removerole` - Removes a role from a user.\n" +
                        "`/mutechannel` - Mutes a channel.\n" +
                        "`/unmutechannel` - Unmutes a channel.\n" +
                        "`/slowmode` - Sets a slow mode on a channel.\n" +
                        "`/announce` - Sends an announcement.\n" +
                        "`/setprefix` - Sets a custom prefix.\n" +
                        "`/clear` - Clears messages in a channel.\n" +
                        "`/purge` - Purges messages older than a certain time.\n" +
                        "`/filter` - Set up a word filter.\n" +
                        "`/showwarns` - Shows warnings for a user.\n" +
                        "`/clearallwarns` - Clears all warnings from the server.\n" +
                        "`/banlist` - Displays the ban list.\n" +
                        "`/tempmute` - Temporarily mutes a user.\n" +
                        "`/tempban` - Temporarily bans a user.\n" +
                        "`/tempkick` - Temporarily kicks a user.\n" +
                        "`/setwelcome` - Sets a welcome message.\n" +
                        "`/setgoodbye` - Sets a goodbye message.\n" +
                        "`/toggleprefix` - Toggles the prefix feature.\n" +
                        "`/addreaction` - Adds a reaction to a message.\n" +
                        "`/clearreactions` - Clears reactions from a message.\n" +
                        "`/suspend` - Suspends a user from chatting.\n" +
                        "`/rejoin` - Allows a suspended user to rejoin the chat.\n" +
                        "`/serverinfo` - Displays server information.\n" +
                        "`/poll` - Creates a poll.\n" +
                        "`/setautomod` - Sets up an automated moderation system.\n" +
                        "`/cleanchannels` - Deletes unused channels.\n" +
                        "`/setlogchannel` - Sets a channel for logs.\n" +
                        "`/purgeuser` - Purges messages from a specific user.\n", Color.Green));
                    break;

                case "status":
                    Log.Information("Status command executed.");
                    string statusMessage = "Current Region Status:\n\n";
                    foreach (var region in _regions)
                    {
                        var isActive = _regionPingStatus.GetValueOrDefault(region.Name.ToLower(), false);
                        statusMessage += $"{region.Name}: {(isActive ? "Active" : "Inactive")}\n";
                    }
                    await command.RespondAsync(embed: CreateEmbed(statusMessage, Color.Blue));
                    break;

                case "reload":
                    Log.Information("Reloading server status...");
                    await PingServers();
                    await command.RespondAsync("Server status reloaded.", ephemeral: true);
                    break;

                case "europe":
                case "usa":
                case "japan":
                    Log.Information($"{command.CommandName} command executed.");
                    var regionName = command.CommandName;
                    if (_regionPingStatus.ContainsKey(regionName))
                    {
                        _regionPingStatus[regionName] = !_regionPingStatus[regionName];
                        SaveRegionPingStatus();
                        var status = _regionPingStatus[regionName] ? "enabled" : "disabled";
                        await command.RespondAsync(embed: CreateEmbed($"Ping for {regionName} {status}.", _regionPingStatus[regionName] ? Color.Green : Color.Red), ephemeral: true);
                    }
                    break;

                case "shutdown":
                    Log.Information("Shutting down the bot...");
                    await command.RespondAsync("Shutting down...", ephemeral: true);
                    Environment.Exit(0);
                    break;

                case "ban":
                    var banUser = (SocketUser)command.Data.Options.First().Value;
                    var banReason = command.Data.Options.Count > 1 ? command.Data.Options.ElementAt(1).Value.ToString() : "No reason provided";
                    await BanUserAsync(banUser, banReason);
                    await command.RespondAsync($"User {banUser.Username} has been banned for: {banReason}", ephemeral: true);
                    break;

                //TODO

                //case "unban":
                //    var unbanUser = (string)command.Data.Options.First().Value;
                //    await UnbanUserAsync(unbanUser);
                //    await command.RespondAsync($"User {unbanUser} has been unbanned.", ephemeral: true);
                //    break;

                case "kick":
                    var kickUser = (SocketUser)command.Data.Options.First().Value;
                    await KickUserAsync(kickUser);
                    await command.RespondAsync($"User {kickUser.Username} has been kicked.", ephemeral: true);
                    break;

                case "mute":
                    var muteUser = (SocketUser)command.Data.Options.First().Value;
                    await MuteUserAsync(muteUser);
                    await command.RespondAsync($"User {muteUser.Username} has been muted.", ephemeral: true);
                    break;

                case "unmute":
                    var unmuteUser = (SocketUser)command.Data.Options.First().Value;
                    await UnmuteUserAsync(unmuteUser);
                    await command.RespondAsync($"User {unmuteUser.Username} has been unmuted.", ephemeral: true);
                    break;

case "warn":
    var warnUser = (SocketUser)command.Data.Options.First().Value;
    var warnMessage = command.Data.Options.Count > 1 ? command.Data.Options.ElementAt(1).Value.ToString() : "No reason provided";
    
    // Warn the user and notify them via DM
    await WarnUserAsync(warnUser, warnMessage);

    // Send a DM to the user with the warning message
    try
    {
        // Use GetOrCreateDMChannelAsync() directly on the SocketUser
        var dmChannel = await warnUser.CreateDMChannelAsync(); // api10 doesnt grab the channel, it just does it lol
        await dmChannel.SendMessageAsync($"You have been warned: {warnMessage}");
    }
    catch (Exception ex)
    {
        Log.Information($"Could not send DM to {warnUser.Username}: {ex.Message}");
    }

    // Respond in the command channel
    await command.RespondAsync($"User {warnUser.Username} has been warned for: {warnMessage}", ephemeral: true);
    break;



                case "clearwarns":
                    var clearWarnUser = (SocketUser)command.Data.Options.First().Value;
                    await ClearWarningsAsync(clearWarnUser);
                    await command.RespondAsync($"Warnings for {clearWarnUser.Username} have been cleared.", ephemeral: true);
                    break;

case "setnickname":
    try
    {
        // Ensure options are provided
        if (command.Data.Options == null || !command.Data.Options.Any())
        {
            await command.RespondAsync("No user or nickname provided. Please specify a user and a nickname.", ephemeral: true);
            return;
        }

        // Validate and retrieve user and nickname options
        var nicknameUserOption = command.Data.Options.FirstOrDefault(o => o.Name == "user");
        var nicknameOption = command.Data.Options.FirstOrDefault(o => o.Name == "nickname");

        if (nicknameUserOption == null || nicknameOption == null)
        {
            await command.RespondAsync("Invalid arguments. Please provide both a user and a nickname.", ephemeral: true);
            return;
        }

        var nicknameUser = nicknameUserOption.Value as SocketGuildUser;
        var nickname = nicknameOption.Value?.ToString();

        if (nicknameUser == null)
        {
            await command.RespondAsync("User not found in this guild. Please ensure the user exists.", ephemeral: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(nickname))
        {
            await command.RespondAsync("Please provide a valid nickname.", ephemeral: true);
            return;
        }

        // Set nickname
        await nicknameUser.ModifyAsync(u => u.Nickname = nickname);
        await command.RespondAsync($"User {nicknameUser.Username}'s nickname has been set to {nickname}.", ephemeral: true);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error occurred while setting nickname.");
        await command.RespondAsync("Failed to set the nickname. Ensure the bot has the necessary permissions.", ephemeral: true);
    }
    break;



                case "lockdown":
                    await LockdownChannelAsync(command);
                    await command.RespondAsync("Channel has been locked down.", ephemeral: true);
                    break;

                case "unlock":
                    await UnlockChannelAsync(command);
                    await command.RespondAsync("Channel has been unlocked.", ephemeral: true);
                    break;

                case "addrole":
                    var addRoleUser = (SocketUser)command.Data.Options.First().Value;
                    var addRole = (SocketRole)command.Data.Options.ElementAt(1).Value;
                    await AddRoleToUserAsync(addRoleUser, addRole);
                    await command.RespondAsync($"Role {addRole.Name} has been added to {addRoleUser.Username}.", ephemeral: true);
                    break;

                case "removerole":
                    var removeRoleUser = (SocketUser)command.Data.Options.First().Value;
                    var removeRole = (SocketRole)command.Data.Options.ElementAt(1).Value;
                    await RemoveRoleFromUserAsync(removeRoleUser, removeRole);
                    await command.RespondAsync($"Role {removeRole.Name} has been removed from {removeRoleUser.Username}.", ephemeral: true);
                    break;

                case "mutechannel":
                    await MuteChannelAsync();
                    await command.RespondAsync("The channel has been muted.", ephemeral: true);
                    break;

                case "unmutechannel":
                    await UnmuteChannelAsync();
                    await command.RespondAsync("The channel has been unmuted.", ephemeral: true);
                    break;

                case "slowmode":
                    var slowModeDuration = (int)command.Data.Options.First().Value;
                    await SetSlowModeAsync(command, slowModeDuration);
                    await command.RespondAsync($"Slow mode has been set for {slowModeDuration} seconds.", ephemeral: true);
                    break;

                case "clear":
                    var clearCount = (int)command.Data.Options.First().Value;
                    await ClearMessagesAsync(command, clearCount);
                    await command.RespondAsync($"Cleared {clearCount} messages.", ephemeral: true);
                    break;

                default:
                    await command.RespondAsync("Unknown command.", ephemeral: true);
                    Log.Warning($"Unknown command {command.CommandName} invoked.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while handling command.");
            await command.RespondAsync("An error occurred while processing your request.", ephemeral: true);
        }
    }

    //TODO Own Class

    private static async Task LockdownChannelAsync(SocketSlashCommand command)
    {
        var channel = (ITextChannel)command.Channel;
        await channel.AddPermissionOverwriteAsync(command.User, new OverwritePermissions(sendMessages: PermValue.Deny));
    }

    private static async Task UnlockChannelAsync(SocketSlashCommand command)
    {
        var channel = (ITextChannel)command.Channel;
        await channel.AddPermissionOverwriteAsync(command.User, new OverwritePermissions(sendMessages: PermValue.Allow));
    }

    private static async Task SetSlowModeAsync(SocketSlashCommand command, int duration)
    {
        await (command.Channel as ITextChannel).ModifyAsync(properties => properties.SlowModeInterval = duration);
    }

    private static async Task ClearMessagesAsync(SocketSlashCommand command, int count)
    {
        var messages = await (command.Channel as ITextChannel).GetMessagesAsync(count).FlattenAsync();
        await (command.Channel as ITextChannel).DeleteMessagesAsync(messages);
    }

    private static Embed CreateEmbed(string description, Color color)
    {
        var embed = new EmbedBuilder()
            .WithDescription(description)
            .WithColor(color)
            .Build();

        return embed;
    }

    //private static async Task PingServers()
    //{
    //    // Logic to ping servers (e.g., sending ping requests and updating status)
    //    Log.Information("Pinged all servers.");
    //}


    private static async Task BanUserAsync(SocketUser user, string reason)
    {
        var guildUser = user as SocketGuildUser;
        if (guildUser != null)
        {
            await guildUser.BanAsync(reason: reason);
            Log.Information($"User {user.Username} banned for: {reason}");
        }
    }

    //TODO Fix async

    //private static async Task UnbanUserAsync(string username)
    //{
    //    var bannedUsers = await command.Guild.GetBansAsync();
    //    var ban = bannedUsers.FirstOrDefault(b => b.User.Username == username);

    //    if (ban.User != null)
    //    {
    //        await command.Guild.RemoveBanAsync(ban.User);
    //        Log.Information($"User {ban.User.Username} unbanned.");
    //    }
    //}

    private static async Task KickUserAsync(SocketUser user)
    {
        var guildUser = user as SocketGuildUser;
        if (guildUser != null)
        {
            await guildUser.KickAsync();
            Log.Information($"User {user.Username} kicked.");
        }
    }

    private static async Task MuteUserAsync(SocketUser user)
    {
        var guildUser = user as SocketGuildUser;
        if (guildUser != null)
        {
            await guildUser.AddRoleAsync(guildUser.Guild.Roles.First(r => r.Name == "Muted"));
            Log.Information($"User {user.Username} muted.");
        }
    }

    private static async Task UnmuteUserAsync(SocketUser user)
    {
        var guildUser = user as SocketGuildUser;
        if (guildUser != null)
        {
            await guildUser.RemoveRoleAsync(guildUser.Guild.Roles.First(r => r.Name == "Muted"));
            Log.Information($"User {user.Username} unmuted.");
        }
    }

    private static async Task WarnUserAsync(SocketUser user, string message)
    {
        //TODO Save in DB
        Log.Information($"User {user.Username} warned for: {message}");
    }

    private static async Task ClearWarningsAsync(SocketUser user)
    {
        //TODO Save in DB
        Log.Information($"Warnings for user {user.Username} cleared.");
    }

    private static async Task SetNicknameAsync(SocketUser user, string nickname)
    {
        var guildUser = user as SocketGuildUser;
        if (guildUser != null)
        {
            await guildUser.ModifyAsync(properties => properties.Nickname = nickname);
            Log.Information($"Nickname for {user.Username} set to {nickname}.");
        }
    }

    private static async Task AddRoleToUserAsync(SocketUser user, SocketRole role)
    {
        var guildUser = user as SocketGuildUser;
        if (guildUser != null)
        {
            await guildUser.AddRoleAsync(role);
            Log.Information($"Role {role.Name} added to {user.Username}.");
        }
    }

    private static async Task RemoveRoleFromUserAsync(SocketUser user, SocketRole role)
    {
        var guildUser = user as SocketGuildUser;
        if (guildUser != null)
        {
            await guildUser.RemoveRoleAsync(role);
            Log.Information($"Role {role.Name} removed from {user.Username}.");
        }
    }

    private static async Task MuteChannelAsync()
    {

        //TODO LOGIC
        Log.Information("Channel muted.");
    }

    private static async Task UnmuteChannelAsync()
    {
        //TODO LOGIC
        Log.Information("Channel unmuted.");
    }




    private static void LoadRegionPingStatus()
    {
        try
        {
            Log.Information("Loading region ping status...");
            if (!File.Exists(ConfigFilePath))
            {
                _regionPingStatus = _regions.ToDictionary(r => r.Name.ToLower(), _ => true);
                SaveRegionPingStatus();
                Log.Information("No configuration file found, initializing default ping status.");
            }
            else
            {
                var json = File.ReadAllText(ConfigFilePath);
                _regionPingStatus = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                Log.Information("Loaded region ping status from file.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading region ping status.");
            _regionPingStatus = _regions.ToDictionary(r => r.Name.ToLower(), _ => true);
        }
    }

    private static void SaveRegionPingStatus()
    {
        try
        {
            Log.Information("Saving region ping status...");
            var json = JsonSerializer.Serialize(_regionPingStatus, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
            Log.Information("Region ping status saved.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving region ping status.");
        }
    }
}

public class ServerInfo
{
    public string? Name { get; set; }
    public string? IP { get; set; }
}

public class RegionInfo
{
    public string? Name { get; set; }
    public List<ServerInfo> Servers { get; set; } = new List<ServerInfo>();
}
