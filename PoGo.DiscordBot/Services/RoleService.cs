﻿using Discord.WebSocket;
using System.Linq;

namespace PoGo.DiscordBot.Services
{
    public class RoleService
    {
        public SocketRole GetRoleByName(SocketGuild guild, string name) => guild.Roles.FirstOrDefault(t => t.Name == name);
    }
}