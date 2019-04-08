﻿using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using PoGo.DiscordBot.Services;
using System;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules.Preconditions
{
    public class RaidChannelPreconditionAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var raidChannelService = services.GetService<RaidChannelService>();

            if (raidChannelService.IsKnown(context.Guild.Id, context.Channel.Id))
                return Task.FromResult(PreconditionResult.FromSuccess());

            return Task.FromResult(PreconditionResult.FromError("This channel is not configured as input"));
        }
    }
}