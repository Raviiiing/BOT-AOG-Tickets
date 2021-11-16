using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Timers;

namespace AOG_BOT
{
    class Program
    {
        private ulong log_id = 886041415820410932;
        private ulong plainte_log_id = 887042775030333512;
        private ulong ticketCategoryId = 884872372426014752;
        private ulong plainteCategoryId = 887042577151442984;
        private ulong admin_role_id = 886293186601967617;
        private ulong mod_role_id = 887294367432331285;
        private ulong guild_id = 850182424271912961;
        private string url_site_web = "https://ageofglory.fr/";
        private DiscordSocketClient _client;
        public static void Main(string[] args)
        => new Program().MainAsync().GetAwaiter().GetResult();

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
        private async Task MainAsync()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All,
                AlwaysDownloadUsers = true,
                LogLevel = LogSeverity.Debug,
            }) ;

            _client.Log += Log;

            await _client.LoginAsync(TokenType.Bot, "token");
            await _client.StartAsync();
            _client.Ready += Create_Commands;
            _client.InteractionCreated += Client_InteractionCreated;
            _client.ButtonExecuted += MyButtonHandler;

            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += async (sender, e) => await PingServer(sender, e);
            aTimer.Interval = 60000;
            aTimer.Enabled = true;

            await Task.Delay(-1);
        }
        public async Task UpdateStatus(bool success)
        {
            if(success)
            {
                await _client.SetGameAsync("AOG - Online", url_site_web);
                await _client.SetStatusAsync(UserStatus.Online);
                Console.WriteLine("Ping successful");
            }
            else
            {
                await _client.SetGameAsync("AOG - Offline", url_site_web);
                await _client.SetStatusAsync(UserStatus.DoNotDisturb);
                Console.WriteLine("Ping failed");
            }
        }
        public async Task Create_Commands()
        {
            var guild = _client.GetGuild(guild_id);
            var ticket = new SlashCommandBuilder();
            var ping = new SlashCommandBuilder();

            ticket.WithName("ticket");
            ticket.WithDescription("Creer un Ticket Support");

            ping.WithName("ping");
            ping.WithDescription("Ping to see if AOG server is online");

            try
            {
                await guild.CreateApplicationCommandAsync(ticket.Build());
                await guild.CreateApplicationCommandAsync(ping.Build());
            }
            catch (ApplicationCommandException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Error, Formatting.Indented);
                Console.WriteLine(json);
            }
        }

        private async Task Client_InteractionCreated(SocketInteraction arg)
        {
            if (arg is SocketSlashCommand command)
            {
                ulong user_id = command.User.Id;
                SocketGuild guild = _client.GetGuild(guild_id);
                SocketGuildUser user = guild.GetUser(user_id);
               
                if(user != null)
                {
                    if (command.Data.Name == "ticket" && user.Roles.Contains(guild.GetRole(admin_role_id)))
                    {
                        var msgButton = new ComponentBuilder()
                            .WithButton("Ouvrir un Ticket", customId: "ticket", ButtonStyle.Success, row: 0)
                            .WithButton("Déposer une plainte", customId: "plainte", ButtonStyle.Primary, row: 0);
                        await command.Channel.SendMessageAsync($"Appuyez sur le bouton ouvrir un ticket support !", component: msgButton.Build());
                        await command.RespondAsync("Commande éxécutée avec succés", ephemeral: true);
                    }
                    else if (command.Data.Name == "ticket" && !user.Roles.Contains(guild.GetRole(admin_role_id)))
                        await command.RespondAsync("Vous n'avez pas le droit d'utiliser cette commande", ephemeral: true);
                }
                else
                    await command.RespondAsync($"Erreur, impossible d'avoir l'utilisateur.");
            }
        }
        
        private async Task TicketMSG(ITextChannel textChannel, ulong user_id, ulong log_msg_id)
        {
            
            SocketGuild guild = _client.GetGuild(guild_id);
            SocketGuildUser user = guild.GetUser(user_id);

            var del_button = new ComponentBuilder()
                .WithButton("Supprimer le Ticket", customId: "confirmdelete", ButtonStyle.Danger, row: 0)
                .WithButton("Ouvrir / Fermer le Ticket", customId:"close", ButtonStyle.Primary,row: 0)
                .WithButton("Status Critique", customId: "critical", ButtonStyle.Secondary, row: 0);
            await textChannel.SendMessageAsync($"Ticket créé par: {user.Mention} le {textChannel.CreatedAt.UtcDateTime.AddHours(2)} ID Log = {log_msg_id}", component: del_button.Build());
        }

        public async Task MyButtonHandler(SocketMessageComponent component) 
        {
            SocketGuildUser user = (SocketGuildUser)component.User;
            SocketGuild guild = user.Guild;

            ITextChannel textChannel = guild.GetTextChannel(component.Channel.Id);
            ITextChannel log_channel = guild.GetTextChannel(log_id);

            switch (component.Data.CustomId)
            {
                case "confirmdelete":
                    {
                        if(user.Roles.Contains(guild.GetRole(admin_role_id)))
                        {
                            string content = component.Message.Content;
                            string[] descapres = content.Split('=');

                            ulong log_msg_id = Convert.ToUInt64(descapres[1]);

                            var del_button = new ComponentBuilder()
                                .WithButton("Supprimer", customId: "delete", ButtonStyle.Danger, row: 0)
                                .WithButton("Abandonner", customId: "abort", ButtonStyle.Success, row: 0);
                            await component.Channel.SendMessageAsync($"{user.Mention} Êtes vous sur ? ID Log = {log_msg_id}", component: del_button.Build());
                        }
                        else
                        {
                            await component.RespondAsync("Vous n'êtes pas Administrateur, impossible de faire ça!", ephemeral: true);

                        }
                    }
                    break;
                case "delete":
                    {
                        if (user.Roles.Contains(guild.GetRole(admin_role_id)))
                        {
                            string content = component.Message.Content;
                            string[] descapres = content.Split('=');

                            ulong log_msg_id = Convert.ToUInt64(descapres[1]);

                            await log_channel.DeleteMessageAsync(log_msg_id);
                            await textChannel.DeleteAsync();

                            EmbedBuilder embed = new EmbedBuilder()
                                .WithTitle("Ticket Supprimé")
                                .WithDescription($"Le ticket {textChannel.Name} a été supprimé par {user.Mention}")
                                .WithColor(Color.Red);

                            IUserMessage msg = await log_channel.SendMessageAsync(" ", false, embed.Build());
                        }
                        else
                        {
                            await component.RespondAsync("Vous n'êtes pas Administrateur, impossible de faire ça!", ephemeral: true);
                        }
                    }
                    break;
                case "ticket":
                    {
                        DateTime date = DateTime.UtcNow.AddHours(2);
                        string chanName = component.User.Username + "_" + date + "_ticket";
                        textChannel = await guild.CreateTextChannelAsync(chanName, prop => prop.CategoryId = ticketCategoryId);
                        await component.RespondAsync($"{component.User.Mention} - Ticket créée !", ephemeral:true);
                        ulong log_msg = await LogTicket(textChannel, user);
                        await TicketMSG(textChannel, component.User.Id, log_msg);
                        await AddPerms(textChannel, user, guild);
                    }
                    break;
                case "join":
                    {
                            IReadOnlyCollection<Discord.Embed> embeds = component.Message.Embeds;
                            Embed t = embeds.FirstOrDefault();
                            string desc = t.Description;

                            string[] descapres = desc.Split('-');

                            desc = descapres[1];
                            descapres = desc.Split('#');
                            desc = descapres[1];
                            descapres = desc.Split('>');
                            ulong id = Convert.ToUInt64(descapres[0]);

                            textChannel = guild.GetTextChannel(id);

                            OverwritePermissions overwritePermissions = new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow);
                            await textChannel.AddPermissionOverwriteAsync(user, overwritePermissions);
                            await textChannel.SendMessageAsync($"{component.User.Mention} à rejoint le Ticket");
                            await component.RespondAsync("Permissions ajoutée", ephemeral: true);
                        }
                    break;
                case "close":
                    {
                        string content = component.Message.Content;
                        string[] descapres = content.Split('=');

                        ulong log_msg_id = Convert.ToUInt64(descapres[1]);

                        IMessage log_msg = await log_channel.GetMessageAsync(log_msg_id);
                        Emoji emoji = Emoji.Parse("🔒");

                        if (log_msg.Reactions.ContainsKey(emoji))
                        {
                            //Déjà locked donc faut unlock
                            await log_msg.RemoveAllReactionsForEmoteAsync(emoji);
                            await textChannel.SendMessageAsync($"{DateTime.Now} : {user.Mention} à ré-ouvert le ticket 🔓");
                            //Remettre les perms ici
                            descapres = content.Split('!');
                            string[] temp = descapres[1].Split('>');
                            ulong ticket_creator_id = Convert.ToUInt64(temp[0]);
                            IGuildUser user_creator = await textChannel.GetUserAsync(ticket_creator_id);
                            await AddPerms(textChannel, user_creator, guild);
                        }
                        else
                        {
                            //Lock ici
                            await log_msg.AddReactionAsync(emoji);
                            await textChannel.SendMessageAsync($"{DateTime.Now} : {user.Mention} à fermer le ticket 🔒");
                            descapres = content.Split('!');
                            string[] temp = descapres[1].Split('>');
                            ulong ticket_creator_id = Convert.ToUInt64(temp[0]);
                            IGuildUser user_creator = await textChannel.GetUserAsync(ticket_creator_id);
                            await RemovePerms(textChannel, user_creator, guild);
                        }
                    }
                    break;
                case "critical":
                    {
                        string content = component.Message.Content;
                        string[] descapres = content.Split('=');

                        ulong log_msg_id = Convert.ToUInt64(descapres[1]);

                        IMessage log_msg = await log_channel.GetMessageAsync(log_msg_id);
                        Emoji emoji = Emoji.Parse("⚠️");
                        if (user.Roles.Contains(guild.GetRole(mod_role_id)) || user.Roles.Contains(guild.GetRole(admin_role_id)))
                        {
                            if (log_msg.Reactions.ContainsKey(emoji))
                            {
                                //Passer le statut a normal
                                SocketRole admin_role = guild.GetRole(admin_role_id);

                                await textChannel.SendMessageAsync($"{DateTime.Now} : {user.Mention} à changé le statut à normal ! {admin_role.Mention}☑️");
                                await log_msg.RemoveAllReactionsForEmoteAsync(emoji);
                            }
                            else
                            {
                                //passer le statut a critique
                                SocketRole admin_role = guild.GetRole(admin_role_id);

                                await textChannel.SendMessageAsync($"{DateTime.Now} : {user.Mention} à changé le statut à critique ! {admin_role.Mention}⚠️");
                                await log_msg.AddReactionAsync(emoji);
                            }
                        }
                        else
                            await component.RespondAsync($"Seuls les Modérateurs et Administrateurs ont ces droits!", ephemeral: true);
                    }
                    break;
                case "abort":
                    {
                        if(user.Roles.Contains(guild.GetRole(admin_role_id)))
                        {
                            await textChannel.DeleteMessageAsync(component.Message.Id);
                        }
                        else
                        {
                            await component.RespondAsync("Vous n'avez pas les permissions d'Administrateur, impossible de faire ça!");
                        }
                    }
                    break;
                case "plainte": // Plainte 
                    {
                        DateTime date = DateTime.UtcNow.AddHours(2);
                        string chanName = component.User.Username + "_" + date + "_plainte";
                        textChannel = await guild.CreateTextChannelAsync(chanName, prop => prop.CategoryId = plainteCategoryId);
                        await component.RespondAsync($"{component.User.Mention} - Dépôt de plainte créée !", ephemeral: true);
                        ulong plainte_log_msg = await PlaintesLog(textChannel, user);
                        await PlainteMSG(textChannel, component.User.Id, plainte_log_msg);
                        await AddPerms(textChannel, user, guild);
                    }
                    break;
                case "join_plainte": 
                    {
                        IReadOnlyCollection<Discord.Embed> embeds = component.Message.Embeds;
                        Embed t = embeds.FirstOrDefault();
                        string desc = t.Description;

                        string[] descapres = desc.Split('-');

                        desc = descapres[1];
                        descapres = desc.Split('#');
                        desc = descapres[1];
                        descapres = desc.Split('>');
                        ulong id = Convert.ToUInt64(descapres[0]);
                        textChannel = guild.GetTextChannel(id);
                        OverwritePermissions overwritePermissions = new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow);
                        await textChannel.AddPermissionOverwriteAsync(user, overwritePermissions);
                        await textChannel.SendMessageAsync($"{component.User.Mention} à rejoint le dossier de plainte.");
                        await component.RespondAsync("Permissions ajoutée", ephemeral: true);
                    }
                    break;
                case "confirmdeletePlainte": 
                    {
                        if (user.Roles.Contains(guild.GetRole(admin_role_id)))
                        {
                            ITextChannel plainte_log_channel = guild.GetTextChannel(plainte_log_id);

                            string content = component.Message.Content;
                            string[] descapres = content.Split('=');

                            ulong plainte_log_msg_id = Convert.ToUInt64(descapres[1]);

                            var del_button = new ComponentBuilder()
                                .WithButton("Supprimer", customId: "deletePlainte", ButtonStyle.Danger, row: 0)
                                .WithButton("Abandonner", customId: "abort", ButtonStyle.Success, row: 0);
                            await component.Channel.SendMessageAsync($"{user.Mention} Êtes vous sur ? ID Log = {plainte_log_msg_id}", component: del_button.Build());
                        }
                        else
                        {
                            await component.RespondAsync("Vous n'êtes pas Administrateur, impossible de faire ça!", ephemeral: true);

                        }
                    }
                    break;
                case "closePlainte":
                    {
                        ITextChannel plainte_log_channel = guild.GetTextChannel(plainte_log_id);

                        string content = component.Message.Content;
                        string[] descapres = content.Split('=');

                        ulong plainte_log_msg_id = Convert.ToUInt64(descapres[1]);

                        IMessage plainte_log_msg = await plainte_log_channel.GetMessageAsync(plainte_log_msg_id);
                        Emoji emoji = Emoji.Parse("🔒");

                        if (plainte_log_msg.Reactions.ContainsKey(emoji))
                        {
                            //Déjà locked donc faut unlock
                            await plainte_log_msg.RemoveAllReactionsForEmoteAsync(emoji);
                            await textChannel.SendMessageAsync($"{DateTime.Now} : {user.Mention} à ré-ouvert la plainte 🔓");
                            //Remettre les perms ici
                            descapres = content.Split('!');
                            string[] temp = descapres[1].Split('>');
                            ulong plainte_creator_id = Convert.ToUInt64(temp[0]);
                            IGuildUser user_creator = await textChannel.GetUserAsync(plainte_creator_id);
                            await AddPerms(textChannel, user_creator, guild);
                        }
                        else
                        {
                            //Lock ici
                            await plainte_log_msg.AddReactionAsync(emoji);
                            await textChannel.SendMessageAsync($"{DateTime.Now} : {user.Mention} à fermé la plainte 🔒");
                            descapres = content.Split('!');
                            string[] temp = descapres[1].Split('>');
                            ulong plainte_creator_id = Convert.ToUInt64(temp[0]);
                            IGuildUser user_creator = await textChannel.GetUserAsync(plainte_creator_id);
                            await RemovePerms(textChannel, user_creator, guild);
                        }
                    }
                    break;
                case "criticalPlainte":
                    {
                        ITextChannel plainte_log_channel = guild.GetTextChannel(plainte_log_id);

                        string content = component.Message.Content;
                        string[] descapres = content.Split('=');

                        ulong plainte_log_msg_id = Convert.ToUInt64(descapres[1]);

                        IMessage log_msg = await plainte_log_channel.GetMessageAsync(plainte_log_msg_id);
                        Emoji emoji = Emoji.Parse("⚠️");
                        if(user.Roles.Contains(guild.GetRole(mod_role_id)) || user.Roles.Contains(guild.GetRole(admin_role_id)))
                        {
                            if (log_msg.Reactions.ContainsKey(emoji))
                            {
                                //Passer le statut a normal
                                SocketRole admin_role = guild.GetRole(admin_role_id);

                                await textChannel.SendMessageAsync($"{DateTime.Now} : {user.Mention} à changé le statut à normal ! {admin_role.Mention}☑️");
                                await log_msg.RemoveAllReactionsForEmoteAsync(emoji);
                            }
                            else
                            {
                                //passer le statut a critique
                                SocketRole admin_role = guild.GetRole(admin_role_id);

                                await textChannel.SendMessageAsync($"{DateTime.Now} : {user.Mention} à changé le statut à critique ! {admin_role.Mention}⚠️");
                                await log_msg.AddReactionAsync(emoji);
                            }
                        }
                        else
                            await component.RespondAsync($"Seuls les Modérateurs et Administrateurs ont ces droits!", ephemeral: true);
                    }
                    break;
                case "deletePlainte": 
                    {
                        if (user.Roles.Contains(guild.GetRole(admin_role_id)))
                        {
                            ITextChannel plainte_log_channel = guild.GetTextChannel(plainte_log_id);

                            string content = component.Message.Content;
                            string[] descapres = content.Split('=');

                            ulong plainte_log_msg_id = Convert.ToUInt64(descapres[1]);

                            await plainte_log_channel.DeleteMessageAsync(plainte_log_msg_id);
                            await textChannel.DeleteAsync();

                            EmbedBuilder embed = new EmbedBuilder()
                                .WithTitle("Plainte Supprimé")
                                .WithDescription($"La Plainte {textChannel.Name} a été supprimé par {user.Mention}")
                                .WithColor(Color.Red);

                            IUserMessage msg = await plainte_log_channel.SendMessageAsync(" ", false, embed.Build());
                        }
                        else
                        {
                            await component.RespondAsync("Vous n'êtes pas Administrateur, impossible de faire ça!", ephemeral: true);
                        }
                    }
                    break;

            }
        }

        public async Task<ulong> LogTicket(ITextChannel textChannel, IUser user)
        {
            ITextChannel channelLog = _client.GetGuild(guild_id).GetTextChannel(log_id);
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle("Un nouveau ticket à été créée")
                .WithDescription($"{user.Mention} -> {textChannel.Mention}")
                .WithFooter($"Envoyé le {DateTime.Now}")
                .WithColor(Color.Blue);
            ComponentBuilder button = new ComponentBuilder()
                .WithButton("Rejoindre ce ticket", customId:"join", ButtonStyle.Primary);

            IUserMessage msg = await channelLog.SendMessageAsync(" ", false, embed.Build(),component: button.Build());
            return msg.Id;
        }

        public async Task AddPerms(ITextChannel textChannel, IGuildUser user, IGuild guild)
        {
            OverwritePermissions perms = new OverwritePermissions(viewChannel: PermValue.Deny);
            await textChannel.AddPermissionOverwriteAsync(guild.EveryoneRole, perms);
            OverwritePermissions Userperms = new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow);
            await textChannel.AddPermissionOverwriteAsync(user, Userperms);
        }

        public async Task RemovePerms(ITextChannel textChannel, IGuildUser user, IGuild guild)
        {
            OverwritePermissions Userperms = new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Deny);
            await textChannel.AddPermissionOverwriteAsync(user, Userperms);
        }

        // Partie Plaintes

        public async Task<ulong> PlaintesLog(ITextChannel textChannel, IUser user)
        {
            ITextChannel PlaintechannelLog = _client.GetGuild(guild_id).GetTextChannel(plainte_log_id);
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle("Une nouvelle plainte a été déposé")
                .WithDescription($"{user.Mention} -> {textChannel.Mention}")
                .WithFooter($"Envoyé le {DateTime.Now}")
                .WithColor(Color.Blue);
            ComponentBuilder button = new ComponentBuilder()
                .WithButton("Rejoindre ce dossier", customId: "join_plainte", ButtonStyle.Primary);

            IUserMessage msg = await PlaintechannelLog.SendMessageAsync(" ", false, embed.Build(), component: button.Build());
            return msg.Id;
        }

        private async Task PlainteMSG(ITextChannel textChannel, ulong user_id, ulong log_msg_id)
        {

            SocketGuild guild = _client.GetGuild(guild_id);
            SocketGuildUser user = guild.GetUser(user_id);

            var del_button = new ComponentBuilder()
                .WithButton("Supprimer cette Plainte", customId: "confirmdeletePlainte", ButtonStyle.Danger, row: 0)
                .WithButton("Ouvrir / Fermer la Plainte", customId: "closePlainte", ButtonStyle.Primary, row: 0)
                .WithButton("Status Critique", customId: "criticalPlainte", ButtonStyle.Secondary, row: 0);
            await textChannel.SendMessageAsync($"Ticket creer par: {user.Mention} le {textChannel.CreatedAt.UtcDateTime.AddHours(2)} ID Log = {log_msg_id}", component: del_button.Build());
        }

        // Partie Ping Serveur

        public async Task PingServer(object source, ElapsedEventArgs e)
        {
            string address = "address";
            int port = 25877;
            bool success = false;

            try
            {
                using (var client = new TcpClient(address, port))
                    success = true;
                Console.WriteLine("Ping is ok");
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Error pinging host:'" + address + ":" + port + "'");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.ErrorCode);
                success = false;
            }
            await UpdateStatus(success);
        }

    }
}