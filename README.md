Steam workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=2775613786

# Description

Got only a few friends that you want to play Barotrauma with, but the Bots make game less tolerable? **Well worry no more! Kinda ...**

Multiplayer crew manager is a modification that makes Multiplayer Campaign play something more similar to the Singleplayer one, in terms that:

* Directly controllable bot crew-mates (all crew members are AI controlled bots, that can be possessed by client player)
* Crew information panel now also used to switch between crew characters by clicking on them ***(Full installation needed, see [Client-side lua installation](#Client-side-lua-installation))***
* Server ***CHAT*** commands that allows more in-depth control of campaign (especially for server admins), just type in chat `mcm` or `mcm help`
* Can user `mcm release` to spectate, or for admins `mcm release <client_id>` to free characters from AFK players

## To keep in mind

* **To run this mod**, as every other LuaCs mod, you must select **LuaForBarotrauma** server executable in '**Host Server**' menu.
* **ALWAYS** keep your campaign files backed up
* Be careful with new respawning mechanic, when doing a level transition
* Campaign most probably cannot be converted back, without manual editing *(xml file)*, or in-game shenanigans
* Every bot, new hire and player is saved into `_CharacterData.xml`
* Respawns are turned off by default, to turn them on use: `mcm respawn set true`
* Respawn **delay** and **time** refers to timeout on first person dead and time to arrive to main sub on respawn shuttle.

# Client-side lua installation

* Install **[Lua For Barotrauma](https://steamcommunity.com/sharedfiles/filedetails/?id=2559634234)** mod
* In the settings mod menu enable **LuaForBarotrauma**.
* In multiplayer select '**Host Server**', select **LuaForBarotrauma** executable.
* While in the hosting menu open the console window *(most commonly **`F3`**)*
* Type in or copy, then execute the console command: `install_cl_lua`
* Restart the game

# Checkout my other mods

**[My Barotrauma workshop](https://steamcommunity.com/id/oiltanker-dk/myworkshopfiles/?appid=602960)**

Want to message me more directly? Message me on my **[discord](https://discord.gg/HkPNqnkDdF)**

- - - -

Steam 创意工坊：https://steamcommunity.com/sharedfiles/filedetails/?id=2775613786

# 介绍

**也许你只有很少的朋友**愿意和你一起玩这个游戏，但人工智障控制的船员让你难以忍受？那么这个模组可以满足你的要求。

这个模组以类似单人模式的方式控制你多人战役模式的船员：

* 人工智障会接管所有不被玩家控制的船员。
* 你不需要再担心你的朋友掉线或者是你控制其他角色时，原本的角色灵魂出窍而死。以及没有在存档前回到自己的角色导致角色消失。
* 可以通过直接点击船员面板中的船员来切换控制的角色。***（需要完整安装，请见 [客户端 Lua 安装](#客户端-Lua-安装)）***
* 添加更深入的控制战役的指令（一部分需要服务器管理员权限）。只需要在 `聊天框` 输入 `mcm` 或者是 `mcm help` 指令。

## 指令

* `mcm` [函数] [参数]
  * mcm 函数语法
  * `ID 参数` 分为 `mcm list` 查询的`角色 ID` 与 `mcm clientlist` 指令查询的`玩家 ID`。两者均为查询时显示在***第一列的数字***
  * `[方括号]` 内的内容为***可选项***
  * `<尖括号>` 内的内容为***必选项***

* `mcm [help]` — 显示 mcm 函数帮助。（help是可选的）
* `mcm list` — 列出所有可控制角色及其 ***ID*** 信息。
* `mcm control <角色ID>` — 控制你输入的 ***ID*** 所属的角色。
* `mcm release [角色ID]` — 释放你当前控制的角色，或者是输入一个 ***ID*** 释放其所属的角色。（后者需要管理员权限才能使用）

## 管理员指令

* `mcm delete <角色ID>` — 删除你输入的 ***ID*** 所属的角色。（包括其身上所有物品）
* `mcm clientlist` — 列出所有玩家及其 ***ID*** 信息。
* `mcm spawn <玩家ID>` — 以你输入的 ***ID*** 所属玩家的预设生成一个角色。
* `mcm client autospawn <true/false>` — 打开或关闭为***新连接的玩家***生成一个其***预设角色***的功能

* `mcm respawn` — 列出`重生设置`
* `mcm respawn set <true/false>` — 打开或关闭***重生功能***。
* `mcm respawn penalty <true/false>` — 打开或关闭***重生惩罚***。
* `mcm respawn delay <整数/秒>` — 重生前的***等待时间***
* `mcm respawn time <整数/秒>` — 提供给***复活飞船与潜艇对接的时间***

## 注意事项

* **要启用这个模组**，首先你要在游戏设置中启用。然后在**创建服务器**时**服务器可执行文件**选择 **Lua For Barotrauma - DedicatedServer**。
* **永远**注意备份你的战役存档。
* 如果你想要从一个进行中的战役存档中卸载这个模组，那么你需要手动修改存档的 .xml 文件，并且还可能需要修改一些其他的东西，我不能确定，后果自负。
* 每个 AI 控制的船员与玩家的角色都会被保存在 `_CharacterData.xml`当中。
* 默认情况下，重生功能是被关闭的。如果要打开，请使用：`mcm respawn set true`

# 客户端 Lua 安装

* 订阅并启用 **[Lua For Barotrauma](https://steamcommunity.com/sharedfiles/filedetails/?id=2559634234)** 模组。
* 在多人游戏中选择“**`创建服务器`**”，服务器可执行文件选择“**`Lua For Barotrauma - DedicatedServer`**”。
* 在服务器大厅，打开控制台（默认按键为 **`F3`**）。
* 输入或复制这条指令并执行：**`install_cl_lua`**。
* 重启游戏。

# 查看我的其他模组

**[我的潜渊症创意工坊](https://steamcommunity.com/id/oiltanker-dk/myworkshopfiles/?appid=602960)**

想更直接的跟我交流吗？请在我的 **[Discord](=https://discord.gg/HkPNqnkDdF)** 留言
