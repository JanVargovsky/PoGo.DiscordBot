# Discord Bot for players of the Pokemon Go
[![Docker](https://img.shields.io/docker/cloud/build/janvargovsky/pogo-discordbot.svg)](https://hub.docker.com/r/janvargovsky/pogo-discordbot)

Discord BOT focused on internal communication within players of the Pokemon Go

- Set team & level as role
- Plan a raid or ex-raid
  - Set raid boss, location and scheduled time. React with 👍 that you will attend and raid poll message will refresh itself
  - In case that amount of players is too high, it will split up players per team so you can create multiple raid lobbies for extra balls.
  - Do you have two devices or somebody is not on the Discord yet? You can add 1️⃣, 2️⃣ etc.
  ![raid poll preview](https://i.imgur.com/ML4WbgT.png)
- Scheduled raid polls (MewTwo, Community Days etc.)
- Statistics about players
- Gym GPS locations
- Raid boss counters
- Just type **!help** and you will see all the commands/features

# How to run
Currently the bot is operating in multiple Czech cities using only one server. The bot's requirements are so low that it's waste to manage dedicated server only for bot.

From monitoring the bot uses less than 100MB of RAM and 0.001% of CPU in average.

## Join us
Contact me on Discord (Pako#3904) and I can make the bot join your server, all you have to do is setup a simple JSON like this:
```json
{
    "Name": "Frydek - Mistek",
    "Id": 343037316752998400,
    "Channels": [
        {
            "From": "ex-raid-diskuze",
            "To": "ex-raid-ankety",
            "ScheduledRaids": true
        },
        {
            "From": "*",
            "To": "raid-ankety"
        }
    ]
}
```


Note: this is minimalistic config, there is a lot more!

## Host yourself
### Prerequisites
- .NET Core SDK 2.1 ([download here](https://www.microsoft.com/net/download/windows))
- created bot with a token and joined in a guild (Discord server)

### Setup
1. Clone repository
2. Set environment variable *PoGoEnvironment* with environment type (e.g. Development or Production)
3. Configure configuration.\<environment\>.json  
4. dotnet run

Basic structure of the configuration.\<environment\>.json
```json
{
  "Token": "TOKEN HERE [1]",
  "Guilds": [
    {
      "Name": "Your Server's name [2]",
      "Id": 123456789 [3],
      "Channels": [
        {
          "From": "*",
          "To": "name of the destination channel for polls [4]",
          "ScheduledRaids": true
        }
      ]
    }
  ]
}
```
\* is required.  
1\* - Bot's token for auth.  
2 - Just an alias for the logging purpose. Bot can run on multiple servers at once.  
3\* - Id of your Guild (Discord server).  
4\* - Name of the destination channel where the bot will write a raid polls.

Notes: In case you have multiple channels, the order is from the top to the bottom, so first matched will win and therefore place "*" as the last one.

# Future development
The BOT supports Czech language only (for end users). In case that you are interested translations might be added.
