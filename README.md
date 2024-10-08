# P4G Dungeon Framework

Reloaded-II mod that takes elements of Persona 4 Golden's dungeon system and makes them more easily accessible for modders.<br>

**This is currently in beta, there may be bugs I am unaware of with unknown consequences. While in my time testing this I have only experienced game crashes, I cannot guarentee what any user of this mod may experience. Use at your own risk.**

## Table of Contents
- [Mod Details](#mod-details)
    + [What it Currently Does](#what-it-currently-does)
    + [What it Currently Does Not Do](#what-it-currently-does-not-do)
    + [Known Bugs](#known-bugs)
    + [Laundry List](#laundry-list)
- [Screenshots](#screenshots)
- [Usage](#usage)
    + [All Users](#all-users)
    + [Modders](#modders)
- [The Big Explanation Bit](#the-big-explanation-bit)
  + [Dungeon Room](#dungeon-room)
  + [Dungeon Template](#dungeon-template)
  + [Dungeon Floor](#dungeon-floor)
  + [Minimap](#minimap)
  + [Field Compares](#field-compares)
  + [Room Compares](#room-compares)
- [Thanks](#thanks)

## Mod Details

### What it Currently Does

- Loads in the contents of .JSON files over the hardcoded data found in P4G<br>
  - JSON files account for dungeon rooms, dungeon templates, dungeon floors.<br>
- Allows for the addition of more dungeon floors to the game.<br>
- Allows for the addition of custom dungeon templates to the game.<br>
- Tie dungeon floor names to the .json, allowing them to be easily modified.<br>
- Allows for the addition of custom dungeon rooms to the game.<br>
- Allows for custom fields to be more easily added to the game.<br>

### What it Currently Does Not Do

- Allow for bigger rooms. The game natively hard-caps the biggest types of individual rooms to be 3x3, which is originally used for 2x2 rooms and their entrances. Expanding the cap to allow for bigger rooms seems doable, but need to figure out how.<br>
- Make the map bigger. Not even sure if it's possible, but it would be funny if you could.<br>


### Known Bugs

-No currently known bugs

### Laundry List

Order is roughly from top of the list to the bottom, but not necessarily an indicator of how things will be done.<br>
- Figure if expanding the room size cap is possible <br>
- Get an icon for the mod.<br>
- Check to see if map expansion is plausible.<br>

# Screenshots
![Custom Room](https://github.com/Some-Body-Else/p4gpc.dungeonframework/assets/86819277/eed658f8-d9af-49b9-b593-fcb9d7a6b652) <br>
![Another Angle](https://github.com/Some-Body-Else/p4gpc.dungeonframework/assets/86819277/11195aca-2219-4bcb-a890-8d5ccb10a770) <br>
![Go Big Mode](https://github.com/Some-Body-Else/p4gpc.dungeonframework/assets/86819277/310383ec-6c89-46aa-9e5f-319fafe4d920) <br>


## Usage


### All Users
This section is essentially for the future, if I'm being honest. While defining custom rooms/floors/templates in the .jsons does work and DungeonFramework does support loading files from a "dungeonframework" folder in the directory of Persona 4 Golden, the purpose of this release is to put it in the public to see if the build as it stands interrupts any part of a regular Persona 4 Golden playthrough. If you do insist on trying to implement custom dungeon stuff at this time, I am on the [Persona modding Discord](https://discord.gg/naoto) if you have any questions or insights.<br><br>

That being said, there is one configuration options I wish to make note of: if the warning/error text about loading the default files is something that annoys you, it can be suppressed via an option in the Reloaded-II config.


### Modders
The Dungeon Framework expects to load in a set of .json files with the following names:
- dungeon_floors.json
- dungeon_minimap.json
- dungeon_rooms.json
- dungeon_template_dict.json
- dungeon_templates.json<br>

When it comes to loading custom .json files, you must create a folder named "dungeonframework" in your Persona 4 Golden executable's directory and place them in there. <br>

## The Big Explanation Bit

A lot of terms have been thrown around in the above section, but since this has been a solo project for the most part, an explanation of these terms is overdue.<br>

### Dungeon Room

In this context, a dungeon room is the individual tiles that the dungeon piece together on each floor.<br> The game has 14 of them by default, but no one dungeon has
access to all the tiles at once due to template limitations.<br>

List of rooms, categorized by tile size:<br><br>
**1x1 rooms**<br>

* Room 1: Hallway<br>![smap01](https://user-images.githubusercontent.com/86819277/188293311-c8320632-5174-4d5d-8c11-071a4d195b95.png)
* Room 3: 90&#176; Turn<br>![smap03](https://user-images.githubusercontent.com/86819277/188293707-8763e703-e64e-407e-87ad-6dc4966b75f3.png)
* Room 4: Four-Way Stop<br>![smap04](https://user-images.githubusercontent.com/86819277/188293709-e500c527-f531-44a0-ad03-c4489326b401.png)

* Room 5: Dead End<br>![smap05](https://user-images.githubusercontent.com/86819277/188293720-2fb8e188-788f-4980-afc1-0038b5a6207d.png)

* Room 6: Entrance<br>![smap06](https://user-images.githubusercontent.com/86819277/188293712-9589b69b-b3f0-4cb5-b154-c5fb315b6bdb.png)


**2x2 rooms**<br>
* Room 2: 90&#176; Fork with Door <br>![smap02](https://user-images.githubusercontent.com/86819277/188293803-afd85511-d8e6-4a77-bfba-d07119fc0fdf.png)

* Room 7: Diagonal Hallway <br>![smap07](https://user-images.githubusercontent.com/86819277/188293811-0669d62e-1422-4576-99e5-f3bbeb779c3a.png)

* Room 8: Flipped Diagonal Hallway<br>![smap08](https://user-images.githubusercontent.com/86819277/188293807-ebf39547-f313-45d6-b7a1-a8103fa458d7.png)


**3x3 rooms**<br>
* Room 9: 2x2 room as dead end<br>![smap09](https://user-images.githubusercontent.com/86819277/188293814-f1bd5927-82e7-4f99-8202-f1e85c00fbe1.png)

* Room 10: 2x2 exit as dead end<br>![smap10](https://user-images.githubusercontent.com/86819277/188293817-52b4a73e-2058-4f40-b323-2874cbda03ef.png)

* Room 11: 2x2 room as side room<br>![smap11](https://user-images.githubusercontent.com/86819277/188293824-8155ef0b-20b5-495e-b7d5-a8299c9148ab.png)

* Room 12: 2x2 exit as side room<br>![smap12](https://user-images.githubusercontent.com/86819277/188293825-ab2eff32-6bbe-4c71-92c7-4674618e6bc2.png)

* Room 13: 2x2 room with 2 connections<br>![smap13](https://user-images.githubusercontent.com/86819277/188293826-f91828b1-370e-470f-bfec-4f71bd2f1d4c.png)

* Room 14: 2x2 exit with 2 connections<br>![smap14](https://user-images.githubusercontent.com/86819277/188293831-c5368fd3-8105-4e71-9bb6-3c06171f3cf5.png)

Of special note is that there is no overlap at all with the 3x3 rooms, presumably to prevent multiple floor exits from generating on a single floor.

### Dungeon Template

In Persona 4 Golden, each field designated as a dungeon is assigned a template to work with. This template tells the game how many tiles that field's .arc files are 
expected to collectively hold, as well as which particular tiles are expected to be found there.<br>

List of templates and what their associated dungeons are:<br><br>

**Template 0**: Contains rooms 1, 2, 3, 4, 5, 6, 7, 9, and 10<br>
**Used by**: Marukyu Striptease, Heaven<br><br>

**Template 1**: Contains rooms 1, 2, 3, 4, 5, 6, 7, 8, 11, and 12<br>
**Used by**: Yukiko's Castle, Void Quest, Yomotsu Hirasakata<br><br>

**Template 2**: Contains rooms 1, 2, 3, 4, 5, 6, 8, 13, and 14<br>
**Used by**: Steamy Bathhouse, Secret Laboratory, Magatsu Mandala/Inaba, Hollow Forest<br><br>

In addition to this, each template has two sizes to work with, with the first size being used for earlier floors and the second being used for later floors. I'm not sure what triggers the switch between the two, but in all vanilla cases it results in the introduction of room 4 into the list of rooms to generate with.

### Dungeon Floor
A dungeon floor mostly refers to an entry in the file **dungeon.bin**, located at init_free.bin/field/table/dungeon.bin. It is a list of fields and properties to be leveraged when they are loaded, including which dungeon script to use and some parameters that are known to be used in dungeon generation, but whose direct purpose is currently unknown.<br><br>
The only portion of floor data not directly linked to dungeon.bin is the name of the floor, which is normally hardcoded into the game's executable.


### Minimap
Much of the minimap still remains unknown for the moment, but what is known is where the textures are (**init_free.bin/field/smap.bin**) and how portions of the texture are selected for rendering. The game loads all of the textures in at launch and then internally keeps tabs on their information through a table. 

### Field Compares
Previously unmentioned because there's no real good place to bring it up, but Persona 4 Golden checks field type based on their ID.<br>
If a field's ID is:<br>
- Field 1 is a special case, it is the overworld map.<br>
- Below 20, fields are considered overworld fields.<br>
- Below 40, fields are static dungeon fields (mostly the same as overworld fields, but keeping the distinction because these fields sometimes use the in-dungeon battle models for the player).<br> 
- Between 40 and 59, it is considered a randomly-generated floor, used for most dungeon floors.<br> 
- Between 60 and 79, it is considered a pregenerated floor, used for any dungeon floor with a miniboss encounter.<br> 
- Between 80 and 199, admittedly unsure since there is one field I've seen in that range (100) and I'm not quite sure what it is.<br>
- 200 and above is a battle field for the various encounters in the game.<br><br>

The mod now covers most of these comparisons. The few exceptions that I found are not for ranges, but rather to check for flags set for individual fields. These flags will be accounted for at a later time.

### Room Compares
Most of these are found in regards to the minimap functions, there are times where the game makes decisions based on the ID of the room. The functions most of these are found around seem to be transformation functions, which makes sense considering the data the game has at hand during execution at that time lacks some potentially critical information, such as the size of the room.

## Thanks

Just want to give a thank you to the members of the [Persona modding Discord](https://discord.gg/naoto) for lending a hand whenever I had a question. Thanks goes out in particular to Pixelgun, Tekka, pioziomgames, and rudiger, who helped me find the answers to my questions when I got stuck in the dark.<br><br>
Also want to give a special thanks to AnimatedSwine37, whose Reloaded-II mods were essential for me to get a grasp on how to write a Reloaded-II mod myself and who helped with the fork of Aemulus.
