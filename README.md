# p4gpc.dungeonloader

Reloaded-II mod that takes elements of Persona 4 Golden's dungeon system and makes them more easily accessible for modders.<br>

### What it Currently Does<br>

-Loads in the contents of .JSON files over the hardcoded data found in P4G<br>
--JSON files account for dungeon rooms, dungeon templates, dungeon floors.<br>
-Allows for bees.<br>
-Hopefully not crash the game.<br>

### What it Currently Doesn't Do<br>

-Touch any of the minimap code. From my time with [this mod](https://gamebanana.com/mods/386894), I learned that the game does a check with the file containing the minimap tiles<br>
 before it gets to the animated into and will crash unless everything is in order. Just need some more time to figure our what to do with the check and the actual minimap functions.<br>
-Allow for bigger rooms. The game natively hard-caps the biggest types of individual rooms to be 3x3, which is originally used for 2x2 rooms<br>
 and their entrances. Expanding the cap to allow for bigger rooms seems doable.<br>
-Make the map bigger. Not even sure if it's possible, but it would be funny if you could.<br>


### Known Bugs<br>

-For some reason, some dungeons have the loading icon freeze while loading and also appear to take longer to load. This doesn't<br>
 seem to affect anything on a gameplay level, but it is not ideal. Appears to affect all dungeons that follow template 0.<br>

### Laundry List<br>

Order is roughly from top of the list to the bottom, but not necessarily an indicator of how things will be done.<br>
-Account for minimap code.<br>
-Fleas.<br>
-Create tools to enable others to create their own .json files to load in, making the creation of custom dungeons.<br>
-Fix known bugs.<br>
-Get an icon for the mod.<br>


## The Big Explanation Bit<br>

A lot of terms have been thrown around in the above section, but since this has been a solo project for the most part, an explanation of these terms is overdue.<br>

### Dungeon Room<br>

In this context, a dungeon room is the individual tiles that the dungeon piece together on each floor.<br> The game has 14 of them by default, but no one dungeon has
access to all the tiles at once due to template limitations.<br>

List of rooms, categorized by tile size:<br><br>
**1x1 rooms**

<ul>
  <li>Room 1: Hallway<br>
  ![Test](/.github/assets/smap01.png)
  </li>
  <li>Room 3: 90 Degree Turn</li>
  <li>Room 4: Four-Way Stop</li>
  <li>Room 5: Dead End</li>
  <li>Room 6: Entrance</li>
</ul>

### Dungeon Template<br>

In Persona 4 Golden, each field designated as a dungeon is assigned a template to work with. This template tells the game how many tiles that field's .arc files are<br>
expected to collectively hold, as well as which particular tiles are expected to be found there.<br>

List of templates and what their associated dungeons are:<br><br>

**Template 0**: Contains rooms, 1, 2, 3, 4, 5, 6, 7, 9, and 10<br>
**Used by**: Marukyu Striptease, Heaven, Yomotsu Hirasaka<br><br>

**Template 1**: Contains rooms, 1, 2, 3, 4, 5, 6, 7, 8, 11, and 12<br>
**Used by**: Yukiko's Castle, Void Quest<br><br>

**Template 2**: Contains rooms, 1, 2, 3, 4, 5, 6, 8, 13, and 14<br>
**Used by**: Steamy Bathhouse, Secret Laboratory, Magatsu Mandala/Inaba, Hollow Forest<br><br>

### Dungeon Floor<br>

Bees.
