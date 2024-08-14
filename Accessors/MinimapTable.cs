using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.Sources;
using Reloaded.Memory;
using Reloaded.Memory.Sigscan;
using Reloaded.Mod.Interfaces;

using static Reloaded.Hooks.Definitions.X64.FunctionAttribute;

using System;
using System.Collections.Generic;

using p4gpc.dungeonframework.JsonClasses;
using p4gpc.dungeonframework.Configuration;
using System.Diagnostics;

namespace p4gpc.dungeonframework.Accessors
{
    public class MinimapTable : Accessor
    {
    
        private List<DungeonMinimap> _minimaps;
        private nuint _newMinimapTable;

        private nuint _roomHasMultipleTexturesTable;
        private nuint _minimapTextureOffsetTableLookupTable;

        // Okay, THIS cannot go without some sort of note.
        // After all of the textures are checked from smap.bin, another loop goes and sets
        // up a table of 8-byte addresses. There's one for each minimap texture, but I have
        // little to no idea what the devil they are. I'm moving it because the current location appears
        // to run out of space for any additional rooms, and I need to modify this section anyways
        // because it checks from the number of minimap textures, which is normally hardcoded

        private nuint _minimapUnknownPerTextureTable;
        /*
         * Each of the textures have a set of coordinate textures to dictate what part of the image is actually rendered
         * Stored as a pair of 4-btye floatsm with each texture having 2 pairs of texture coordinates (Top-left and bottom-right)
         */
        private nuint _minimapTextureCoordinateTable;
        private nuint _minimapTextureCoordinateLookupTable;
        
        /*
         * Each of the textures also have what I can only think of as a "scale" value for x and y, dictating how many pixels the selected image takes up.
         * For some reason it appears to be half the number of pixels encapsulated by the texture coordinates, unsure why.
         * Stored as a pair of 4-btye floats (x scale and y scale)
         */
        private nuint _minimapTextureScaleTable;
        
        /*
         * One part of one texture has its scale values switched depending on how it is rotated in-game, so this accounts for that and any
         * custom textures that may want to do the same.
         * Stored as a single byte per texture, although since these are just binary values there's probably a better way to store/access them
         */
        private nuint _minimapTextureOrientTable;


        private nuint _minimapRevealStack;

        /*
         * In a given table that accounts for multiple textures, if you put all the variants of stuff together 
         * (Textures for rooms with 1 texture, textures for rooms with multiple textures, dummy textures)
         * You end up with a very wonky indexing for each of the rooms. Using this as a way to keep track of the relative
         * indices for each room
         */
        private nuint _minimapIndexLookupTable;

        /*
         * The minimap-updating logic needs to know something about the size of each room so it can determine if further searching is needed.
         * Thus, a small table is being created where the data can be stored in a more convienient position
         */
        private nuint _minimapRoomSizeTable;

        // Keeps track of offsets when dealing with messy BFS stuff
        private nuint _minimapNameLater;

        private int minimapCounter = 0;


        public MinimapTable(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {
            _minimaps = jsonImporter.GetMinimap();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Minimap hooks established.", Config.DebugLevels.AlertConnections);
        }

        protected override void Initialize()
        {

            List<long> functions;

            String search_string;
            long address;
            long func;
            uint oldAddress;
            int totalMinimapTableSize = 0;
            byte SIB;
            byte prefixExists;
            AccessorRegister regToZero;

            List<long> _roomTables = new List<long>();
            int offset = 0;

            for (int i = 0; i < _minimaps.Count; i++)
            {
                search_string = "field/smap/";
                search_string += _minimaps[i].name;
                totalMinimapTableSize += search_string.Length;
                offset += search_string.Length;
                minimapCounter++;
                if (_minimaps[i].multipleNames)
                {
                    int numOfVariants = _minimaps[i].names.Count;
                    for (int j = 0; j < numOfVariants; j++)
                    {
                        search_string = "field/smap/";
                        search_string += _minimaps[i].names[j];
                        search_string += char.MinValue;
                        totalMinimapTableSize += search_string.Length;
                        offset += search_string.Length+1;
                        minimapCounter++;
                    }
                }
                else
                {
                }
            }
            _newMinimapTable = _memory.Allocate(totalMinimapTableSize);
            _utils.LogDebug($"Location of MinimapPath table: {_newMinimapTable.ToString("X8")}", Config.DebugLevels.TableLocations);

            _newMinimapLookupTable = _memory.Allocate(minimapCounter*16);
            _utils.LogDebug($"Location of MinimapPathLookup table: {_newMinimapLookupTable.ToString("X8")}", Config.DebugLevels.TableLocations);

            _minimapUnknownPerTextureTable = _memory.Allocate(minimapCounter*8);
            _utils.LogDebug($"Location of UnknownPerTextureTable: {_minimapUnknownPerTextureTable.ToString("X8")}", Config.DebugLevels.TableLocations);

            _minimapRevealStack = _memory.Allocate(432);
            _utils.LogDebug($"Location of MinimapRevealStack: {_minimapRevealStack.ToString("X8")}", Config.DebugLevels.TableLocations);

            _minimapRoomSizeTable = _memory.Allocate(_jsonImporter.GetRooms().Count);
            _utils.LogDebug($"Location of MinimapRoomSizeTable: {_minimapRoomSizeTable.ToString("X8")}", Config.DebugLevels.TableLocations);

            _minimapNameLater = _memory.Allocate(20);
            _utils.LogDebug($"Location of MinimapNameLater: {_minimapNameLater.ToString("X8")}", Config.DebugLevels.TableLocations);

            offset = 0;
            foreach (DungeonRoom room in _jsonImporter.GetRooms())
            {
                // Don't need the actual room size, just need to know if we have to search
                if (room.sizeX > 1 || room.sizeY > 1)
                {
                    _memory.SafeWrite((_minimapRoomSizeTable + (nuint)offset), (byte)1);
                }
                else
                {
                    _memory.SafeWrite((_minimapRoomSizeTable + (nuint)offset), (byte)0);
                }
                offset++;
            }

            offset = 0;
            for (int i = 0; i < _minimaps.Count; i++)
            {

                search_string = "field/smap/";
                search_string += _minimaps[i].name;
                search_string += char.MinValue;
                for (int j = 0; j < search_string.Length; j++)
                {
                    _memory.SafeWrite((_newMinimapTable + (nuint)offset + (nuint)j), search_string[j]);
                }
                _memory.SafeWrite((_newMinimapTable + (nuint)offset + (nuint)search_string.Length), 0);
                _roomTables.Add(offset);
                offset += search_string.Length;
                if (_minimaps[i].multipleNames)
                {
                    int numOfVariants = _minimaps[i].names.Count;
                    for (int j = 0; j < numOfVariants; j++)
                    {
                        search_string = "field/smap/";
                        search_string += _minimaps[i].names[j];
                        search_string += char.MinValue;

                        for (int k = 0; k < search_string.Length; k++)
                        {
                            _memory.SafeWrite((_newMinimapTable + (nuint)offset + (nuint)k), search_string[k]);
                        }
                        _memory.SafeWrite((_newMinimapTable + (nuint)offset + (nuint)search_string.Length), 0);
                        _roomTables.Add(offset);
                        offset += search_string.Length;
                    }
                }
                else
                {
                }
            }

            _newMinimapPathLookupTable = _newMinimapLookupTable + (nuint)minimapCounter*8;

            for (int i = 0; i < minimapCounter; i++)
            {
                _memory.SafeWrite((_newMinimapLookupTable + (nuint)i*8), (ulong)(_newMinimapTable + (nuint)_roomTables[i]) + 11);
                // 11 is length of 'field/smap/', don't think it'll ever change, but noting here in case
                _memory.SafeWrite((_newMinimapPathLookupTable + (nuint)i*8), (ulong)(_newMinimapTable + (nuint)_roomTables[i]));
            }


            _minimapIndexLookupTable = _memory.Allocate(_minimaps.Count);
            _utils.LogDebug($"Location of MinimapIndexLookup table: {_minimapIndexLookupTable.ToString("X8")}", Config.DebugLevels.TableLocations);
            
            _roomHasMultipleTexturesTable = _memory.Allocate(_minimaps.Count);
            _utils.LogDebug($"Location of RoomIsMultiTexture table: {_roomHasMultipleTexturesTable.ToString("X8")}", Config.DebugLevels.TableLocations);

            _minimapTextureOffsetTableLookupTable = _memory.Allocate(8*_minimaps.Count);
            _utils.LogDebug($"Location of MinimapTextureOffset table: {_minimapTextureOffsetTableLookupTable.ToString("X8")}", Config.DebugLevels.TableLocations);
            

            Int32 offsetLookup = 0;

            for (int i = 0; i < _minimaps.Count; i++)
            {
                _memory.SafeWrite(_minimapTextureOffsetTableLookupTable + (nuint)i*4, (Int32)offsetLookup*8);

                if (_minimaps[i].multipleNames)
                {
                    _memory.SafeWrite(_roomHasMultipleTexturesTable + (nuint)i, (byte)1);
                    for (int j = 0; j < _minimaps[i].names.Count; j++)
                    {
                        offsetLookup++;
                    }
                }
                else
                {
                    _memory.SafeWrite(_roomHasMultipleTexturesTable + (nuint)i, (byte)0);
                }
                offsetLookup++;

            }

            offsetLookup = 0;

            // 2 pair of 4-byte float coordinates per texture
            _minimapTextureCoordinateTable = _memory.Allocate(minimapCounter*4*4*4);
            _utils.LogDebug($"Location of MinimapTexCoordTable: {_minimapTextureCoordinateTable.ToString("X8")}", Config.DebugLevels.TableLocations);
            // 1 pair of 4-byte float per texture 
            _minimapTextureScaleTable = _memory.Allocate(minimapCounter*4*2);
            _utils.LogDebug($"Location of MinimapScaleTable: {_minimapTextureScaleTable.ToString("X8")}", Config.DebugLevels.TableLocations);
            // 1 byte per texture
            _minimapTextureOrientTable = _memory.Allocate(minimapCounter);
            _utils.LogDebug($"Location of MinimapOrientTable: {_minimapTextureOrientTable.ToString("X8")}", Config.DebugLevels.TableLocations);

            offset = 0;
            for (int i = 0; i < _minimaps.Count; i++)
            {
                _memory.SafeWrite(_minimapIndexLookupTable + (nuint)i, (byte)offsetLookup);

                if (_minimaps[i].multipleNames)
                {
                    for (int j = 0; j < _minimaps[i].names.Count; j++)
                    {
                        _memory.SafeWrite((_minimapTextureCoordinateTable+(nuint)(i+offset)*16), _minimaps[i].texCoordMulti[j][0][0]);
                        _memory.SafeWrite((_minimapTextureCoordinateTable+(nuint)(i+offset)*16+4), _minimaps[i].texCoordMulti[j][0][1]);
                        _memory.SafeWrite((_minimapTextureCoordinateTable+(nuint)(i+offset)*16+8), _minimaps[i].texCoordMulti[j][1][0]);
                        _memory.SafeWrite((_minimapTextureCoordinateTable+(nuint)(i+offset)*16+12), _minimaps[i].texCoordMulti[j][1][1]);

                        _memory.SafeWrite((_minimapTextureScaleTable+(nuint)(i+offset)*8), _minimaps[i].texScaleMulti[j][0]);
                        _memory.SafeWrite((_minimapTextureScaleTable+(nuint)(i+offset)*8+4), _minimaps[i].texScaleMulti[j][1]);
                        if (_minimaps[i].multiOrientBased[j])
                        {
                            _memory.SafeWrite((_minimapTextureOrientTable+(nuint)(i+offset)), (byte)1);
                        }
                        else
                        {
                            _memory.SafeWrite((_minimapTextureOrientTable+(nuint)(i+offset)), (byte)0);
                        }
                        offset++;
                        offsetLookup++;
                    }
                    // Decrement to counterbalance the natural increment of i
                    offset--;
                }   
                else
                {

                    _memory.SafeWrite((_minimapTextureCoordinateTable+(nuint)(i+offset)*16), _minimaps[i].texCoordSingle[0][0]);
                    _memory.SafeWrite((_minimapTextureCoordinateTable+(nuint)(i+offset)*16+4), _minimaps[i].texCoordSingle[0][1]);
                    _memory.SafeWrite((_minimapTextureCoordinateTable+(nuint)(i+offset)*16+8), _minimaps[i].texCoordSingle[1][0]);
                    _memory.SafeWrite((_minimapTextureCoordinateTable+(nuint)(i+offset)*16+12), _minimaps[i].texCoordSingle[1][1]);

                    _memory.SafeWrite((_minimapTextureScaleTable+(nuint)(i+offset)*8), _minimaps[i].texScaleSingle[0]);
                    _memory.SafeWrite((_minimapTextureScaleTable+(nuint)(i+offset)*8+4), _minimaps[i].texScaleSingle[1]);
                    
                    // SingleOrientBased is necessary for hypothetical tiles with uneven x/y values, like a 2x3 tile
                    if (_minimaps[i].singleOrientBased)
                    {
                        _memory.SafeWrite((_minimapTextureOrientTable+(nuint)(i+offset)), (byte)1);
                    }
                    else
                    {
                        _memory.SafeWrite((_minimapTextureOrientTable+(nuint)(i+offset)), (byte)0);
                    }
                    offsetLookup++;
                }
            }

            search_string = "48 8D 3D ?? ?? ?? ?? 48 8B F3 48 2B F7 48 8D 2D ?? ?? ?? ?? 0F 1F 00";
            func = _utils.SigScan(search_string, $"StartupMinimapSearch");
            ReplaceStartupSearch(func, 23);
            _utils.LogDebug($"Location of [{search_string}]: {func.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            search_string = "4C 8D 35 91 CB DB 04 89 54 24 70 48 8D 7B 20 4C 2B F3 8D 6E 1E";
            func = _utils.SigScan(search_string, $"StartupMinimapCapSwap");
            StartupMinimapCapSwap(func, search_string);
            _utils.LogDebug($"Location of [{search_string}]: {func.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            search_string = "3C 09 0F 82 ?? ?? ?? ?? 0F B6 4B 09 80 F9 01 75 33 0F B6 C0 83 C0 F7 83 F8 05 0F 87 ?? ?? ?? ??";
            func = _utils.SigScan(search_string, $"ReplaceMinimapPathSearch");
            _memory.SafeRead((nuint)(func + 28), out offset);
            ReplaceMinimapPathSearch(func, offset, search_string);
            _utils.LogDebug($"Location of [{search_string}]: {func.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            search_string = "40 80 FF 09 0F 82 ?? ?? ?? ?? 40 0F B6 ?? 83 C0 F7 83 F8 05 0F 87 ?? ?? ?? ??";
            func = _utils.SigScan(search_string, $"ReplaceMinimapTextureMapping");
            _memory.Read((nuint)(func+22), out offset);
            ReplaceMinimapTextureMapping(func, offset, search_string);
            _utils.LogDebug($"Location of [{search_string}]: {func.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            search_string = "4C 8D 15 1D 19 4E 00 44 8B CD";
            func = _utils.SigScan(search_string, $"ReplaceMinimapPathListLoadIn");
            ReplacePathListLoadIn(func, search_string);
            _utils.LogDebug($"Location of [{search_string}]: {func.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            search_string = "49 83 C2 08 4C 63 D8 41 83 F9 1E";
            func = _utils.SigScan(search_string, $"ReplaceMinimapPathListSizeCheck");
            ReplacePathListSizeCheck(func, search_string);
            _utils.LogDebug($"Location of [{search_string}]: {func.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            search_string = "4E 8B 84 DF 90 C4 1E 05 48 8D 7B 40";
            func = _utils.SigScan(search_string, $"ReplaceMinimapUnknownTableLookup");
            ReplaceMinimapUnknownTableLookup(func, search_string);
            _utils.LogDebug($"Location of [{search_string}]: {func.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            search_string = "45 32 FF 48 89 7C 24 20";
            func = _utils.SigScan(search_string, $"ReplaceMinimapUpdateFunction");
            ReplaceMinimapUpdateFunction(func, search_string);
            _utils.LogDebug($"Location of [{search_string}]: {func.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            search_string = "0F B6 F3 44 8B FE 40 0F B6 C7 49 C1 E7 04 49 03 C7";
            func = _utils.SigScan(search_string, $"ReplaceMinimapPositionCheck");
            ReplaceMinimapPositionCheck(func, search_string);
            _utils.LogDebug($"Location of [{search_string}]: {func.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            // search_string = "BA A8 01 00 00 8B 0D 70 9A C6 FD";
            // func = _utils.SigScan(search_string, $"ResetMinimapInitialUpdataeCheck");
            // ResetMinimapInitialUpdataeCheck(func, search_string);
            // _utils.LogDebug($"Location of [{search_string}]: {func.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
        }

        void ReplaceStartupSearch(Int64 functionAddress, int length)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");


            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"mov rdi, {_newMinimapLookupTable}");
            instruction_list.Add($"mov rsi, rbx");
            instruction_list.Add($"sub rsi, rdi");
            instruction_list.Add($"mov rbp, {_newMinimapPathLookupTable}");

            /*
             * This is too scuffed for my liking, but sacrifices must be made.
             * 
             * All of the data from ENCOUNT.TBL is loaded in AFTER the hooks are initially established, so I can't replace
             * the address with my own in the traditional fashion. However, I CAN do it during runtime, and this particular
             * function will be executed ONCE after the addresses are loaded in.
             */
            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");

            // Not fond of hardcoding, but I also don't want to drag in a C# function for this code, so
            // I'll take my lumps here.
            instruction_list.Add($"mov rax, 0x140EC0900");
            instruction_list.Add($"mov rbx, {EncountTables._enemyEncountersAddress}");
            instruction_list.Add($"mov [rax], rbx");

            instruction_list.Add($"mov rax, 0x140EC0930");
            instruction_list.Add($"mov rbx, {EncountTables._floorEncountersAddress}");
            instruction_list.Add($"mov [rax], rbx");

            instruction_list.Add($"mov rax, 0x140EC0938");
            instruction_list.Add($"mov rbx, {EncountTables._lootTablesAddress}");
            instruction_list.Add($"mov [rax], rbx");

            instruction_list.Add($"mov rax, 0x140EC0950");
            instruction_list.Add($"mov rbx, {FloorTable._newFloorObjectTable}");
            instruction_list.Add($"mov [rax], rbx");

            /*
             Heads up to future me or anyone poking around here, these are all part of a table of addresses that are loaded in at
             start of runtime, so looking at what they point to could be handy
             */
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, length).Activate());
        }
        void StartupMinimapCapSwap(Int64 functionAddress, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"mov r14, {_minimapUnknownPerTextureTable}");
            instruction_list.Add($"mov [rsp+0x70], edx");
            instruction_list.Add($"mov rdi, rbx");
            instruction_list.Add($"add rdi, 0x20");
            instruction_list.Add($"sub r14, rbx");
            instruction_list.Add($"mov ebp, {minimapCounter}");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }
        void ReplaceMinimapPathSearch(Int64 functionAddress, int jump_offset, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            Int64 jump_point = functionAddress + 32 + jump_offset - 5;
            
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"mov cl, [rbx + 0x9]");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"push rax");
            instruction_list.Add($"sub rax, 1");
            instruction_list.Add($"xor rbx, rbx");
            instruction_list.Add($"mov bl, [{_roomHasMultipleTexturesTable} + rax]");
            instruction_list.Add($"mov edx, [{_minimapTextureOffsetTableLookupTable} + rax*4]");

            instruction_list.Add($"add cl, bl");
            instruction_list.Add($"pop rax");
            instruction_list.Add($"pop rbx");

            // Going to have to manually search out minimap textures here

            instruction_list.Add($"and rcx, 0xFF");
            instruction_list.Add($"sub rcx, 1");
            instruction_list.Add($"add rdx, {_newMinimapPathLookupTable}");

            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, [rdx + rcx*8]");
            instruction_list.Add($"mov rdx, rax");
            instruction_list.Add($"pop rax");
            instruction_list.Add($"lea rcx, [rsp + 0x40]");


            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {jump_point}");
            instruction_list.Add($"mov [rsp+8], rax");
            instruction_list.Add($"pop rax");
            instruction_list.Add($"ret");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        void ReplaceMinimapTextureMapping(Int64 functionAddress, int jump_offset, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            Int64 jump_point = functionAddress + 26 + jump_offset;
            /*
             Input:
                rax - Some value used specifically when dealing with room 2, has something to do with how part of the room is orientated
                    
                rbx - address for room ID
                    == Unsure of a good name, but all the mapping details are written to here
                rdx - room sub-ID
                    == This is the value that dictates which part of a room texture is used for multi-piece rooms like rooms 9-13
                rdi - room ID

                rcx is replaced during the original operations
             */
            
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"push rdi");
            instruction_list.Add($"push rsi");
            instruction_list.Add($"push rdx");

            instruction_list.Add($"sub rdi, 1");
            instruction_list.Add($"xor rcx, rcx");
            instruction_list.Add($"mov cl, [{_minimapIndexLookupTable} + rdi]");
            // Account for the potential room sub-ID
            instruction_list.Add($"add rcx, rdx");
            // But then we get off by 1, so we adjust
            instruction_list.Add($"sub rcx, 1");
            instruction_list.Add($"mov rsi, rcx");
            instruction_list.Add($"imul cx, 0x10");


            instruction_list.Add($"mov edi, [{_minimapTextureCoordinateTable} + rcx]");
            instruction_list.Add($"mov [rbx+0x28], edi");
            instruction_list.Add($"add rcx, 4");

            instruction_list.Add($"mov edi, [{_minimapTextureCoordinateTable} + rcx]");
            instruction_list.Add($"mov [rbx+0x2C], edi");
            instruction_list.Add($"add rcx, 4");

            instruction_list.Add($"mov edi, [{_minimapTextureCoordinateTable} + rcx]");
            instruction_list.Add($"mov [rbx+0x20], edi");
            instruction_list.Add($"add rcx, 4");

            instruction_list.Add($"mov edi, [{_minimapTextureCoordinateTable} + rcx]");
            instruction_list.Add($"mov [rbx+0x24], edi");


            instruction_list.Add($"mov rcx, rsi");
            instruction_list.Add($"imul rcx, 0x8");

            // Next section gets a bit weird, something regarding how a room is oriented on the map and
            // how that changes where scale values get placed, but only part of room 2 and rooms 11/12
            //uses it. Worth experimenting with.
            instruction_list.Add($"mov dil, [{_minimapTextureOrientTable} + rsi]");
            instruction_list.Add($"cmp dil, 1");

            instruction_list.Add($"jne defaultScale");
            instruction_list.Add($"mov [rbx+0x000000A2], dl");
            instruction_list.Add($"and dx, 1");
            instruction_list.Add($"cmp dx, 1");
            instruction_list.Add($"je defaultScale");

            instruction_list.Add($"mov edi, [{_minimapTextureScaleTable} + rcx]");
            instruction_list.Add($"mov [rbx+0x34], edi");
            instruction_list.Add($"add rcx, 4");
            instruction_list.Add($"mov edi, [{_minimapTextureScaleTable} + rcx]");
            instruction_list.Add($"mov [rbx+0x30], edi");
            instruction_list.Add($"jmp endOfFunc");

            instruction_list.Add($"label defaultScale");
            // Gotta check orientation for scale reasons

            instruction_list.Add($"mov edi, [{_minimapTextureScaleTable} + rcx]");
            instruction_list.Add($"mov [rbx+0x30], edi");
            instruction_list.Add($"add rcx, 4");

            instruction_list.Add($"mov edi, [{_minimapTextureScaleTable} + rcx]");
            instruction_list.Add($"mov [rbx+0x34], edi");
            instruction_list.Add($"jmp endOfFunc");

            instruction_list.Add($"label endOfFunc");

            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rsi");
            instruction_list.Add($"pop rdi");

            // Setup our return address because jumping there is shonky
            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {jump_point}");
            instruction_list.Add($"mov [rsp+8], rax");
            instruction_list.Add($"pop rax");
            instruction_list.Add($"ret");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        void ReplacePathListLoadIn(Int64 functionAddress, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"mov r10, {_newMinimapPathLookupTable}");
            instruction_list.Add($"mov r9, rbp");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }
        void ReplacePathListSizeCheck(Int64 functionAddress, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"add r10, 0x8");
            instruction_list.Add($"mov r11, rax");
            instruction_list.Add($"cmp r9d, {minimapCounter}");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        void ReplaceMinimapUnknownTableLookup(Int64 functionAddress, string pattern)
        {
            // 4E 8B 84 DF 90 C4 1E 05 48 8D 7B 40
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"mov r8, [{_minimapUnknownPerTextureTable} + r11*0x8]");
            instruction_list.Add($"mov rdi, rbx");
            instruction_list.Add($"add rdi, 0x40");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        void ReplaceMinimapUpdateFunction(Int64 functionAddress, string pattern)
        {
            /*
             * TODO:
             * Rework so we can get 1 tile ahead, current iteration is jank with how it reveals the minimap. But it does appear to work. so points on that!
             */
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov [{_lastUsedAddress}], rax");
            instruction_list.Add($"pop rax");

            // RBX - lower offset
            // RDI - upper offset
            // RAX - MapRAM address
            // RCX - Minimap Reveal Address
            // RDX - StackBase ( For comparisons )
            // RSI - Direction Stack(?)
            // RSP - BFS Stack
            // R# - Temporary variables
            AccessorRegister LowerOffset = AccessorRegister.rbx;
            AccessorRegister UpperOffset = AccessorRegister.rdi;
            AccessorRegister MapRam = AccessorRegister.rax;
            AccessorRegister MinimapRevealTable = AccessorRegister.rcx;
            AccessorRegister StackBase = AccessorRegister.rdx;
            AccessorRegister StackBFS = AccessorRegister.rsp;
            AccessorRegister StackCardinal = AccessorRegister.rsi;
            AccessorRegister AddressTempA = AccessorRegister.r8;
            AccessorRegister VariableTempA = AccessorRegister.r9;
            AccessorRegister AddressTempB = AccessorRegister.r10;
            AccessorRegister VariableTempB = AccessorRegister.r11;
            AccessorRegister AddressTempC = AccessorRegister.r12;
            AccessorRegister VariableTempC = AccessorRegister.r13;
            AccessorRegister AddressTempD = AccessorRegister.r12;
            AccessorRegister VariableTempD = AccessorRegister.r15;

            instruction_list.Add($"push {UpperOffset}");
            instruction_list.Add($"push {LowerOffset}");
            instruction_list.Add($"push {MapRam}");
            instruction_list.Add($"push {MinimapRevealTable}");
            instruction_list.Add($"push {StackBFS}");
            instruction_list.Add($"push {StackCardinal}");
            instruction_list.Add($"push {AddressTempA}");
            instruction_list.Add($"push {VariableTempA}");
            instruction_list.Add($"push {AddressTempB}");
            instruction_list.Add($"push {VariableTempB}");
            instruction_list.Add($"push {AddressTempC}");
            instruction_list.Add($"push {VariableTempC}");
            instruction_list.Add($"push {AddressTempD}");
            instruction_list.Add($"push {VariableTempD}");

            instruction_list.Add($"and {UpperOffset}, 0xFF");
            instruction_list.Add($"and {LowerOffset}, 0xFF");

            // Debating on finding these in a more dynamic way, to avoid heartbreak on my end if this game gets an update again.
            // Map base address
            instruction_list.Add($"mov {MapRam}, 0x1411AB39C");
            // Minimap reveal table address
            instruction_list.Add($"mov {MinimapRevealTable}, 0x1451EC580");
            instruction_list.Add($"mov {MinimapRevealTable}, [{MinimapRevealTable}]");
            // Prepare stacks
            instruction_list.Add($"push rbp");
            instruction_list.Add($"mov rbp, {StackBFS}");
            instruction_list.Add($"mov {StackBFS}, {_minimapRevealStack+400}");
            instruction_list.Add($"mov {StackCardinal}, {_minimapRevealStack+432}");

            // Check to make sure this tile is valid
            instruction_list.Add($"mov {AddressTempA}, {UpperOffset}");
            instruction_list.Add($"shl {AddressTempA}, 4");
            instruction_list.Add($"add {AddressTempA}, {LowerOffset}");
            instruction_list.Add($"shl {AddressTempA}, 4");
            instruction_list.Add($"add {AddressTempA}, {MapRam}");
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}]");
            instruction_list.Add($"cmp {VariableTempA}, 1");
            instruction_list.Add($"jne EOF");
            // Getting some door-related data
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempA}+0x4]");
            instruction_list.Add($"mov {VariableTempB}, 0");
            // Getting some tile-related data
            instruction_list.Add($"movzx {VariableTempC}, byte [{AddressTempA}+0x1]");
            instruction_list.Add($"and {VariableTempC}, 0xF0");

            // Pushing directionally-adjectent tiles to 'stack'

            // North
            instruction_list.Add($"sub {AddressTempA}, 0x100");
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0x4]");
            instruction_list.Add($"and {VariableTempA}, 0xFF");
            instruction_list.Add($"cmp {VariableTempA}, 0");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempA}");

            // See if the adjacent tile is part of the same room
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempD}");
            instruction_list.Add($"jne STORE_NORTH");
            instruction_list.Add($"mov {AddressTempC}, {_minimapRoomSizeTable}");
            instruction_list.Add($"add {AddressTempC}, {VariableTempA}");
            instruction_list.Add($"movzx {AddressTempB}, byte [{AddressTempC}]");
            // If value here is 0, then it's a 1x1 tile
            instruction_list.Add($"cmp {AddressTempB}, 0");
            instruction_list.Add($"je STORE_NORTH");

            // Is it a seperate section of the room?
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0x1]");
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempC}");
            instruction_list.Add($"je STORE_NORTH");

            // Does the other tile specifically connect to this one
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0xA]");
            instruction_list.Add($"test {VariableTempA}, 4");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempB}");
            instruction_list.Add($"je STORE_NORTH");

            // Door pointing here, is it open?
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0xB]");
            instruction_list.Add($"and {VariableTempA}, 0xF0");
            instruction_list.Add($"cmp {VariableTempA}, 0");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempA}");
            instruction_list.Add($"label STORE_NORTH");
            instruction_list.Add($"mov [{StackCardinal}], {AddressTempA}");

            // West
            instruction_list.Add($"mov {AddressTempA}, {UpperOffset}");
            instruction_list.Add($"shl {AddressTempA}, 4");
            instruction_list.Add($"add {AddressTempA}, {LowerOffset}");
            instruction_list.Add($"shl {AddressTempA}, 4");
            instruction_list.Add($"add {AddressTempA}, {MapRam}");
            instruction_list.Add($"sub {AddressTempA}, 0x10");

            // Check if room is invalid
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0x4]");
            instruction_list.Add($"and {VariableTempA}, 0xFF");
            instruction_list.Add($"cmp {VariableTempA}, 0");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempA}");
            instruction_list.Add($"je STORE_WEST");

            // See if the adjacent tile is part of the same room
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempD}");
            instruction_list.Add($"jne STORE_WEST");
            instruction_list.Add($"mov {AddressTempC}, {_minimapRoomSizeTable}");
            instruction_list.Add($"add {AddressTempC}, {VariableTempA}");
            instruction_list.Add($"movzx {AddressTempB}, byte [{AddressTempC}]");
            // If value here is 0, then it's a 1x1 tile
            instruction_list.Add($"cmp {AddressTempB}, 0");
            instruction_list.Add($"je STORE_WEST");

            // Is it a seperate section of the room?
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0x1]");
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempC}");
            instruction_list.Add($"je STORE_WEST");

            // Does the other tile specifically connect to this one
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0xA]");
            instruction_list.Add($"test {VariableTempA}, 8");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempB}");
            instruction_list.Add($"je STORE_WEST");

            // Door pointing here, is it open?
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0xB]");
            instruction_list.Add($"and {VariableTempA}, 0xF0");
            instruction_list.Add($"cmp {VariableTempA}, 0");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempA}");
            instruction_list.Add($"label STORE_WEST");
            instruction_list.Add($"mov [{StackCardinal}-0x8], {AddressTempA}");

            // South
            instruction_list.Add($"mov {AddressTempA}, {UpperOffset}");
            instruction_list.Add($"shl {AddressTempA}, 4");
            instruction_list.Add($"add {AddressTempA}, {LowerOffset}");
            instruction_list.Add($"shl {AddressTempA}, 4");
            instruction_list.Add($"add {AddressTempA}, {MapRam}");
            instruction_list.Add($"add {AddressTempA}, 0x100");

            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0x4]");
            instruction_list.Add($"and {VariableTempA}, 0xFF");
            instruction_list.Add($"cmp {VariableTempA}, 0");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempA}");
            // See if the adjacent tile is part of the same room
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempD}");
            instruction_list.Add($"jne STORE_SOUTH");
            instruction_list.Add($"mov {AddressTempC}, {_minimapRoomSizeTable}");
            instruction_list.Add($"add {AddressTempC}, {VariableTempA}");
            instruction_list.Add($"movzx {AddressTempB}, byte [{AddressTempC}]");
            // If value here is 0, then it's a 1x1 tile
            instruction_list.Add($"cmp {AddressTempB}, 0");
            instruction_list.Add($"je STORE_SOUTH");


            // Is it a seperate section of the room?
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0x1]");
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempC}");
            instruction_list.Add($"je STORE_SOUTH");

            // Does the other tile specifically connect to this one
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0xA]");
            instruction_list.Add($"test {VariableTempA}, 1");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempB}");
            instruction_list.Add($"je STORE_SOUTH");

            // Door pointing here, is it open?
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0xB]");
            instruction_list.Add($"and {VariableTempA}, 0xF0");
            instruction_list.Add($"cmp {VariableTempA}, 0");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempA}");
            instruction_list.Add($"label STORE_SOUTH");
            instruction_list.Add($"mov [{StackCardinal}-0x10], {AddressTempA}");


            // East
            instruction_list.Add($"mov {AddressTempA}, {UpperOffset}");
            instruction_list.Add($"shl {AddressTempA}, 4");
            instruction_list.Add($"add {AddressTempA}, {LowerOffset}");
            instruction_list.Add($"shl {AddressTempA}, 4");
            instruction_list.Add($"add {AddressTempA}, {MapRam}");
            instruction_list.Add($"add {AddressTempA}, 0x10");

            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0x4]");
            instruction_list.Add($"and {VariableTempA}, 0xFF");
            instruction_list.Add($"cmp {VariableTempA}, 0");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempA}");
            // See if the adjacent tile is part of the same room
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempD}");
            instruction_list.Add($"jne STORE_EAST");

            instruction_list.Add($"mov {AddressTempC}, {_minimapRoomSizeTable}");
            instruction_list.Add($"add {AddressTempC}, {VariableTempA}");
            instruction_list.Add($"movzx {AddressTempB}, byte [{AddressTempC}]");
            // If value here is 0, then it's a 1x1 tile
            instruction_list.Add($"cmp {AddressTempB}, 0");
            instruction_list.Add($"je STORE_EAST");

            // Is it a seperate section of the room?
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0x1]");
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempC}");
            instruction_list.Add($"je STORE_EAST");

            // Does the other tile specifically connect to this one
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0xA]");
            instruction_list.Add($"test {VariableTempA}, 2");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempB}");
            instruction_list.Add($"je STORE_EAST");

            // Door pointing here, is it open?
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0xB]");
            instruction_list.Add($"and {VariableTempA}, 0xF0");
            instruction_list.Add($"cmp {VariableTempA}, 0");
            instruction_list.Add($"cmove {AddressTempA}, {VariableTempA}");
            instruction_list.Add($"label STORE_EAST");
            instruction_list.Add($"mov [{StackCardinal}-0x18], {AddressTempA}");

            // And now we have our current address
            instruction_list.Add($"mov {AddressTempA}, {UpperOffset}");
            instruction_list.Add($"shl {AddressTempA}, 4");
            instruction_list.Add($"add {AddressTempA}, {LowerOffset}");
            instruction_list.Add($"shl {AddressTempA}, 4");
            instruction_list.Add($"add {AddressTempA}, {MapRam}");

            // Mark current tile as found
            instruction_list.Add($"mov {AddressTempB}, {MinimapRevealTable}");
            instruction_list.Add($"add {AddressTempB}, {UpperOffset}");
            instruction_list.Add($"add {AddressTempB}, {UpperOffset}");
            instruction_list.Add($"mov {VariableTempA}, {LowerOffset}");
            instruction_list.Add($"mov {VariableTempB}, [{AddressTempB}]");
            instruction_list.Add($"bts {VariableTempB}, {VariableTempA}");
            instruction_list.Add($"mov [{AddressTempB}], {VariableTempB}");
            instruction_list.Add($"jmp ROOM_CHECK");

            // Mark current tile as found
            // Seperate block due to some weirdness involving keeping the offsets in play
            instruction_list.Add($"label TILE_FOUND_START");
            instruction_list.Add($"mov {AddressTempB}, {MinimapRevealTable}");
            instruction_list.Add($"add {AddressTempB}, {UpperOffset}");
            instruction_list.Add($"add {AddressTempB}, {UpperOffset}");
            instruction_list.Add($"mov {VariableTempA}, {LowerOffset}");
            instruction_list.Add($"mov {VariableTempB}, [{AddressTempB}]");
            instruction_list.Add($"bts {VariableTempB}, {VariableTempA}");
            instruction_list.Add($"mov [{AddressTempB}], {VariableTempB}");
            instruction_list.Add($"mov {UpperOffset}, {VariableTempC}");
            instruction_list.Add($"mov {LowerOffset}, {VariableTempD}");

            instruction_list.Add($"label ROOM_CHECK");

            // Check if multi-tile room or not
            instruction_list.Add($"movzx {VariableTempA}, byte [{AddressTempA}+0x4]");
            instruction_list.Add($"mov {AddressTempC}, {_minimapRoomSizeTable}");
            instruction_list.Add($"add {AddressTempC}, {VariableTempA}");
            instruction_list.Add($"movzx {VariableTempC}, byte [{AddressTempC}]");
            // If value here is 0, then it's a 1x1 tile, we can start checking the surrounding tiles
            instruction_list.Add($"cmp {VariableTempC}, 0");
            instruction_list.Add($"je NEXT_CARDINAL_TILE");


            // If multi-tiled, beign searching for other parts that may need to be revealed
            instruction_list.Add($"label BFS_START");
            instruction_list.Add($"mov {VariableTempB}, {AddressTempA}");
            instruction_list.Add($"sub {VariableTempB}, {MapRam}");
            instruction_list.Add($"shr {VariableTempB}, 4");
            instruction_list.Add($"mov {VariableTempC}, {VariableTempB}");
            instruction_list.Add($"and {VariableTempC}, 0xF");
            instruction_list.Add($"shr {VariableTempB}, 4");

            instruction_list.Add($"add {VariableTempB}, {VariableTempB}");
            instruction_list.Add($"add {VariableTempB}, {MinimapRevealTable}");
            instruction_list.Add($"mov {VariableTempD}, [{VariableTempB}]");
            instruction_list.Add($"bts {VariableTempD}, {VariableTempC}");
            instruction_list.Add($"mov [{VariableTempB}], {VariableTempD}");

            instruction_list.Add($"movzx {VariableTempB}, byte [{AddressTempA}+0x1]");
            instruction_list.Add($"movzx {VariableTempC}, byte [{AddressTempA}+0xA]");
            instruction_list.Add($"and {VariableTempB}, 0xF0");

            // See if there's an east connection
            instruction_list.Add($"test {VariableTempC}, 0x8");
            instruction_list.Add($"je CHECK_SOUTH");
            // Connected on east side, is it connected to a tile of the same ID?
            instruction_list.Add($"mov {AddressTempD}, {AddressTempA}");
            instruction_list.Add($"add {AddressTempD}, 0x10");
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x4]");
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempD}");
            instruction_list.Add($"jne CHECK_SOUTH");
            // Same upper nybble?
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x1]");
            instruction_list.Add($"and {VariableTempD}, 0xF0");
            instruction_list.Add($"cmp {VariableTempD}, {VariableTempB}");
            instruction_list.Add($"jne CHECK_SOUTH");

            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0xA]");
            instruction_list.Add($"test {VariableTempD}, 4");
            instruction_list.Add($"je CHECK_SOUTH");

            // Has the tile has already been discovered in some previous iteration?
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x0F]");
            instruction_list.Add($"cmp {VariableTempD}, 0x01");
            instruction_list.Add($"je CHECK_SOUTH");

            // Adjacent tile matches all parameters, add it to the stack
            instruction_list.Add($"push {AddressTempD}");
            instruction_list.Add($"mov [{AddressTempD}+0x0F], byte 0x01");


            instruction_list.Add($"label CHECK_SOUTH");
            // See if there's an south connection
            instruction_list.Add($"test {VariableTempC}, 0x4");
            instruction_list.Add($"je CHECK_WEST");
            // Connected on south side, is it connected to a tile of the same ID?
            instruction_list.Add($"mov {AddressTempD}, {AddressTempA}");
            instruction_list.Add($"add {AddressTempD}, 0x100");
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x4]");
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempD}");
            instruction_list.Add($"jne CHECK_WEST");
            // Same upper nybble?
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x1]");
            instruction_list.Add($"and {VariableTempD}, 0xF0");
            instruction_list.Add($"cmp {VariableTempD}, {VariableTempB}");
            instruction_list.Add($"jne CHECK_WEST");

            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0xA]");
            instruction_list.Add($"test {VariableTempD}, 1");
            instruction_list.Add($"je CHECK_WEST");

            // Has it been marked as found
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x0F]");
            instruction_list.Add($"cmp {VariableTempD}, 0x01");
            instruction_list.Add($"je CHECK_WEST");

            // Adjacent tile matches all parameters, add it to the stack
            instruction_list.Add($"push {AddressTempD}");
            instruction_list.Add($"mov [{AddressTempD}+0x0F], byte 0x01");


            instruction_list.Add($"label CHECK_WEST");
            // See if there's an west connection
            instruction_list.Add($"test {VariableTempC}, 0x2");
            instruction_list.Add($"je CHECK_NORTH");
            // Connected on west side, is it connected to a tile of the same ID?
            instruction_list.Add($"mov {AddressTempD}, {AddressTempA}");
            instruction_list.Add($"sub {AddressTempD}, 0x10");
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x4]");
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempD}");
            instruction_list.Add($"jne CHECK_NORTH");
            // Same upper nybble?
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x1]");
            instruction_list.Add($"and {VariableTempD}, 0xF0");
            instruction_list.Add($"cmp {VariableTempD}, {VariableTempB}");
            instruction_list.Add($"jne CHECK_NORTH");

            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0xA]");
            instruction_list.Add($"test {VariableTempD}, 8");
            instruction_list.Add($"je CHECK_NORTH");

            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x0F]");
            instruction_list.Add($"cmp {VariableTempD}, 0x01");
            instruction_list.Add($"je CHECK_NORTH");

            // Adjacent tile matches all parameters, add it to the stack
            instruction_list.Add($"push {AddressTempD}");
            instruction_list.Add($"mov [{AddressTempD}+0x0F], byte 0x01");

            instruction_list.Add($"label CHECK_NORTH");
            // See if there's an north connection
            instruction_list.Add($"test {VariableTempC}, 0x1");
            instruction_list.Add($"je BFS_END");
            // Connected on north side, is it connected to a tile of the same ID?
            instruction_list.Add($"mov {AddressTempD}, {AddressTempA}");
            instruction_list.Add($"sub {AddressTempD}, 0x100");
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x4]");
            instruction_list.Add($"cmp {VariableTempA}, {VariableTempD}");
            instruction_list.Add($"jne BFS_END");
            // Same upper nybble?
            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x1]");
            instruction_list.Add($"and {VariableTempD}, 0xF0");
            instruction_list.Add($"cmp {VariableTempD}, {VariableTempB}");
            instruction_list.Add($"jne BFS_END");

            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0xA]");
            instruction_list.Add($"test {VariableTempD}, 4");
            instruction_list.Add($"je BFS_END");

            instruction_list.Add($"movzx {VariableTempD}, byte [{AddressTempD}+0x0F]");
            instruction_list.Add($"cmp {VariableTempD}, 0x01");
            instruction_list.Add($"je BFS_END");

            // Adjacent tile matches all parameters, add it to the stack
            instruction_list.Add($"push {AddressTempD}");
            instruction_list.Add($"mov [{AddressTempD}+0x0F], byte 0x01");

            // Need to see if anything's left in the stack, and if so, handle that next
            instruction_list.Add($"label BFS_END");
            instruction_list.Add($"mov {AddressTempB}, {_minimapRevealStack+400}");
            instruction_list.Add($"cmp {StackBFS}, {AddressTempB}");
            instruction_list.Add($"jae NEXT_CARDINAL_TILE");
            instruction_list.Add($"pop {AddressTempA}");
            instruction_list.Add($"jmp BFS_START");


            // Look for the next cardinal tile to check, if one exists
            instruction_list.Add($"label NEXT_CARDINAL_TILE");
            
            // East
            instruction_list.Add($"mov {AddressTempC}, [{StackCardinal}-0x18]");
            instruction_list.Add($"cmp {AddressTempC}, 0");
            instruction_list.Add($"je SOUTH_CARDINAL_CHECK");
            instruction_list.Add($"mov {AddressTempA}, {StackCardinal}");
            instruction_list.Add($"sub {AddressTempA}, 0x18");
            instruction_list.Add($"mov {AddressTempB}, 0");
            instruction_list.Add($"mov [{AddressTempA}], {AddressTempB}");
            instruction_list.Add($"mov {AddressTempA}, {AddressTempC}");

            instruction_list.Add($"mov {VariableTempC}, {UpperOffset}");
            instruction_list.Add($"mov {VariableTempD}, {LowerOffset}");
            instruction_list.Add($"add {LowerOffset}, 1");
            instruction_list.Add($"jmp TILE_FOUND_START");


            // South
            instruction_list.Add($"label SOUTH_CARDINAL_CHECK");
            instruction_list.Add($"mov {AddressTempC}, [{StackCardinal}-0x10]");
            instruction_list.Add($"cmp {AddressTempC}, 0");
            instruction_list.Add($"je WEST_CARDINAL_CHECK");
            instruction_list.Add($"mov {AddressTempA}, {StackCardinal}");
            instruction_list.Add($"sub {AddressTempA}, 0x10");
            instruction_list.Add($"mov {AddressTempB}, 0");
            instruction_list.Add($"mov [{AddressTempA}], {AddressTempB}");
            instruction_list.Add($"mov {AddressTempA}, {AddressTempC}");

            instruction_list.Add($"mov {VariableTempC}, {UpperOffset}");
            instruction_list.Add($"mov {VariableTempD}, {LowerOffset}");
            instruction_list.Add($"add {UpperOffset}, 1");
            instruction_list.Add($"jmp TILE_FOUND_START");

            // West
            instruction_list.Add($"label WEST_CARDINAL_CHECK");
            instruction_list.Add($"mov {AddressTempC}, [{StackCardinal}-0x8]");
            instruction_list.Add($"cmp {AddressTempC}, 0");
            instruction_list.Add($"je NORTH_CARDINAL_CHECK");
            instruction_list.Add($"mov {AddressTempA}, {StackCardinal}");
            instruction_list.Add($"sub {AddressTempA}, 0x8");
            instruction_list.Add($"mov {AddressTempB}, 0");
            instruction_list.Add($"mov [{AddressTempA}], {AddressTempB}");
            instruction_list.Add($"mov {AddressTempA}, {AddressTempC}");

            instruction_list.Add($"mov {VariableTempC}, {UpperOffset}");
            instruction_list.Add($"mov {VariableTempD}, {LowerOffset}");
            instruction_list.Add($"sub {LowerOffset}, 1");
            instruction_list.Add($"jmp TILE_FOUND_START");

            // North
            instruction_list.Add($"label NORTH_CARDINAL_CHECK");
            instruction_list.Add($"mov {AddressTempC}, [{StackCardinal}]");
            instruction_list.Add($"cmp {AddressTempC}, 0");
            instruction_list.Add($"je EOF");
            instruction_list.Add($"mov {AddressTempB}, 0");
            instruction_list.Add($"mov [{StackCardinal}], {AddressTempB}");
            instruction_list.Add($"mov {AddressTempA}, {AddressTempC}");

            instruction_list.Add($"mov {VariableTempC}, {UpperOffset}");
            instruction_list.Add($"mov {VariableTempD}, {LowerOffset}");
            instruction_list.Add($"sub {UpperOffset}, 1");
            instruction_list.Add($"jmp TILE_FOUND_START");

            instruction_list.Add($"label EOF");
            instruction_list.Add($"mov rsp, rbp");

            instruction_list.Add($"pop rbp");
            instruction_list.Add($"pop {VariableTempD}");
            instruction_list.Add($"pop {AddressTempD}");
            instruction_list.Add($"pop {VariableTempC}");
            instruction_list.Add($"pop {AddressTempC}");
            instruction_list.Add($"pop {VariableTempB}");
            instruction_list.Add($"pop {AddressTempB}");
            instruction_list.Add($"pop {VariableTempA}");
            instruction_list.Add($"pop {AddressTempA}");
            instruction_list.Add($"pop {StackCardinal}");
            instruction_list.Add($"pop {StackBFS}");
            instruction_list.Add($"pop {MinimapRevealTable}");
            instruction_list.Add($"pop {MapRam}");
            instruction_list.Add($"pop {LowerOffset}");
            instruction_list.Add($"pop {UpperOffset}");

            instruction_list.Add($"add rsp, 0x30");
            instruction_list.Add($"pop r15");
            instruction_list.Add($"pop r14");
            instruction_list.Add($"pop r13");
            instruction_list.Add($"pop r12");
            instruction_list.Add($"pop rdi");
            instruction_list.Add($"pop rsi");
            instruction_list.Add($"pop rbp");
            instruction_list.Add($"ret");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        
        void ReplaceMinimapPositionCheck(Int64 functionAddress, string pattern)
        {
            // There is a set of checks for positioning on the minimap that, with the updated minimap function above,
            // resulted in unexpected behavior. This function is meant to comment out all the troublemakers
            List<string> instruction_list = new List<string>();
            Int64 jump_point = _utils.SigScan("8B 45 14 45 8B E5 F2 0F 10 75 0C F3 0F 5C F7", $"ReplaceMinimapPositionCheck");
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {jump_point}");
            instruction_list.Add($"mov [rsp+8], rax");
            instruction_list.Add($"pop rax");
            instruction_list.Add($"ret");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        void ResetMinimapInitialUpdataeCheck(Int64 functionAddress, string pattern)
        {
            /*
             This is something that the modified MinimapUpdateFunction requires in order to account for the first tile of each floor.
             Since it is a depth-first search based on the existance of a valid room, the first tile encountered must be marked valid
             so it can be referenced for surrounding tiles
             */
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");
            instruction_list.Add($"push rax");
            // instruction_list.Add($"mov rax, {_minimapUpdateInitCheck}");
            instruction_list.Add($"mov [rax], byte 0x00");
            instruction_list.Add($"pop rax");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.ExecuteFirst, _utils.GetPatternLength(pattern)).Activate());
        }
    }
}
