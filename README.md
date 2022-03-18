# Description

Got only a few friends that you want to play Barotrauma with, but the Bots make game less tolerable? **Well worry no more! Kinda ...**
Multiplayer crew manager is a modification that makes Multiplayer Campaign play something more similar to the Singleplayer one, in terms that:
Directly controllable bot crew-mates
Crew information panel now handles clicks on crew members and requests control from the server ***(Full installation needed, see "Client-side lua installation")***
Server commands that allows more in-depth control of campaign (especially for server admins), just type in chat `mcm` or `mcm help`
Can user `mcm release` to spectate, or for admins `mcm release <client_id>` to free characters from AFK players

## To keep in mind

**ALWAYS** keep your campaign files backed up
Be careful with new respawning mechanic, when doing a level transition
Campaign most probably cannot be converted back, without manual editing *(xml file)*, or in-game shenanigans
Every bot, new hire and player is saved into `_CharacterData.xml`
Respawns are turned off by default, to turn them on use: `mcm respawn set true`

# Client-side lua installation

Install **[Lua For Barotrauma](https://steamcommunity.com/workshop/filedetails/?id=2559634234)** mod
In the setting menu select LuaForBarotrauma as the content package, on top of the mod menu.
Host in-game server **twice\*** (it will give the error the first time)
While in the hosting menu open the console window *(most commonly **`F3`**)*
Type in or copy, then execute the console command: `install_cl_lua`
Restart the game

# Checkout my other mods
**[My Barotrauma workshop](https://steamcommunity.com/id/oiltanker-dk/myworkshopfiles/?appid=602960)**
Want to message me more directly? Message me on my **[discord](https://discord.gg/HkPNqnkDdF)**