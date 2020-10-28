using System;
using System.Collections.Generic;
using System.Linq;
using Discord;

namespace PoGo.DiscordBot.Dto
{
    public enum RaidType
    {
        Normal, // Today - within a few hours
        Scheduled,
    }

    public class RaidInfoDto
    {
        public IUserMessage Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public string BossName { get; set; }
        public string Location { get; set; }
        public DateTime DateTime { get; set; }
        public IDictionary<ulong, PlayerDto> Players { get; set; } // <userId, PlayerDto>
        public IDictionary<ulong, PlayerDto> RemotePlayers { get; set; } // <userId, PlayerDto>
        public IDictionary<ulong, PlayerDto> InvitedPlayers { get; set; } // <userId, PlayerDto>
        public List<(ulong UserId, int Count)> ExtraPlayers { get; set; }
        public bool IsExpired => DateTime < DateTime.UtcNow;
        public RaidType RaidType { get; set; }

        public RaidInfoDto(RaidType raidType)
        {
            RaidType = raidType;
            CreatedAt = DateTime.UtcNow;
            Players = new Dictionary<ulong, PlayerDto>();
            RemotePlayers = new Dictionary<ulong, PlayerDto>();
            InvitedPlayers = new Dictionary<ulong, PlayerDto>();
            ExtraPlayers = new List<(ulong UserId, int Count)>();
        }
    }

    public static class RaidInfoDtoExtensions
    {
        public static HashSet<PlayerDto> GetAllPlayers(this RaidInfoDto raid)
            => raid.Players.Values
                .Concat(raid.RemotePlayers.Values)
                .Concat(raid.InvitedPlayers.Values)
                .ToHashSet();
    }
}
