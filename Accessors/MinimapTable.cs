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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Diagnostics;

using p4gpc.dungeonloader.Exceptions;
using p4gpc.dungeonloader.JsonClasses;
using p4gpc.dungeonloader.Configuration;
using System.Reflection;
using Reloaded.Memory.Pointers;
using static System.Formats.Asn1.AsnWriter;
using System.Data.SqlTypes;
using static p4gpc.dungeonloader.Accessors.TemplateTable;

namespace p4gpc.dungeonloader.Accessors
{
    public class MinimapTable : Accessor
    {
        /*
        To do:
            - Determine how minimap updates are made (what enables room to be hidden by doors?)
            - 
         */

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


            /*
             There are two hardcoded tables containing the names of minimap tiles that we need to concern ourselves with.
             One of them only contains the minimap names and is called on startup, used to load in all the minimap tiles into the game.
             Second one has the filepath included with the name, that one is called (as far as I can tell) every frame to render the minimap.

            Both of these tables have a table that actually points to the location of the strings (since their length is variable).
            Majority of all other minimap-related issues involve direct room comparsions, so those are pending to be files under RoomCompares.
             
             Plan for the table is to just have the one table, but have two seperate pointer tables, one pointing to
             the start of the filepath and the other pointing to the start of the filename. That way, we only have 3 tables
             instead of 4 and can perform the same functions. Need to write an algorithm to calculate that out
             
             */

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

            _minimapRevealStack = _memory.Allocate(808);
            _utils.LogDebug($"Location of MinimapRevealStack: {_minimapRevealStack.ToString("X8")}", Config.DebugLevels.TableLocations);


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
                    
                    // TODO: Determine if singleOrientBased should EVER be true. False for all vanilla tiles.
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
             * NOTE:
             * This is intended only to modify how the minimap updates so that it is no longer dependant on a room's ID number to map it out.
             * Currently, it is not working, the main effect the player sees is that the minimap tiles do not render.
             * However, I have ran into at least one case where it appears that an enemy was out-of-bounds and wandered into an area with no collision, crashing the game.
             * This is worrying, since I have no clue why anything changed here would affect enemy spawning. 
             * A realistic explanation is that what I'm changing here currently has inadvertant changes that I'm not aware of as I'm fumbling to get this working
             * Another view would be that this is a bug caused by some other changes this mod makes and that it just rarely surfaced thus far.
             * Worst-case scenario, the code I am replacing also happens to handle enemy spawning and thus needs to account for another layer.
             * 
             * 
             * BE AWARE
             * 
             * Note: After some thinking a few month after this happened, could be due to other shennanigans involving us improperly updating the minimap, so
             * might not be enemy-related at all.
             * 
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

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"push rcx");
            instruction_list.Add($"push rdx");
            instruction_list.Add($"push r8");
            instruction_list.Add($"push r9");

            // Upper and lower offsets, respectively
            instruction_list.Add($"mov r8, rbx");
            instruction_list.Add($"and r8, 0xFF");
            instruction_list.Add($"mov r9, rdi");
            instruction_list.Add($"and r9, 0xFF");

            /*
             Debating on finding these in a more dynamic way, to avoid heartbreak on my end if this game gets an update again.
             */
            // Map base address
            instruction_list.Add($"mov r12, 0x1411AB39C");
            // Minimap reveal table address
            instruction_list.Add($"mov rax, 0x1451EC580");
            instruction_list.Add($"mov r13, [rax]");

            // Stack of nodes to process
            instruction_list.Add($"push rbp");
            instruction_list.Add($"mov rbp, rsp");
            instruction_list.Add($"mov rsp, {_minimapRevealStack+0x800}");

            // Counter for how many nodes need to be checked
            instruction_list.Add($"xor rsi, rsi");
            instruction_list.Add($"add rsi, 1");
            
            // Full offset
            instruction_list.Add($"xor rdi, rdi");
            instruction_list.Add($"add rdi, r9");
            instruction_list.Add($"shl rdi, 4");
            instruction_list.Add($"add rdi, r8");
            instruction_list.Add($"shl rdi, 4");

            // Address of particular tile in offset table
            instruction_list.Add($"mov rcx, r12");
            instruction_list.Add($"add rcx, rdi");

            instruction_list.Add($"mov rbx, [rcx]");
            instruction_list.Add($"and rbx, 0xFF");
            instruction_list.Add($"cmp rbx, 1");
            instruction_list.Add($"jne END_OF_FUNCTION");

            // Further inspection needed
            instruction_list.Add($"label NEED_TO_CHECK");

            // This is where things get REALLY messy; using a breadth-first search algorithm
            // to find every tile with a matching room ID and a corresponding set of bitflags
            // to determine what switches are to be flipped when rendering the minimap

            // Fetch room ID
            instruction_list.Add($"movzx r10, byte [rcx+0x4]");
            instruction_list.Add($"cmp r10, 06");
            instruction_list.Add($"jne LOOP_START");
            //Entrance tile
            instruction_list.Add($"mov rbx, r13");

            instruction_list.Add($"add rbx, r9");
            instruction_list.Add($"add rbx, r9");

            instruction_list.Add($"mov rcx, r8");
            instruction_list.Add($"mov rdx, [rbx]");

            // Need to check to make sure this tile is connected to an already-revealed tile
            instruction_list.Add($"bts dx, cx");
            instruction_list.Add($"mov [rbx], dx");
            instruction_list.Add("jmp END_OF_FUNCTION");


            // instruction_list.Add($"and r10l, 0xFF");


            /*
            -Loop:
                {
                    Check if tile is orientated towards any other tiles
                    --Should alwas be the case
                    See if tile it is adjacent to has the same upper nybble of bitflags and same room ID
                    --If yes, see if the tile's corresponding offset is in the list of found offsets
                    ----If it is, ignore it
                    ----Otherwise, add it to the found list, push the offset to the internal stack, and increment the stack counter
                    Modify the current offset's flags
                    Pop the most recent offset of the internal stack, decrement counter
                    If counter is higher than 0, repeat, otherwise goto END_OF_FUNCTION
                }
            Order of execution for loop might have to be shuffled around, this format could produce an error I think

             */

            instruction_list.Add($"label LOOP_START");

            instruction_list.Add($"movzx r15, byte [rcx+0x1]");
            instruction_list.Add($"and r15, 0xF0");

            instruction_list.Add($"movzx r11, byte [rcx+0xA]");

            // Check for connection on the east side of the room
            instruction_list.Add($"and r11l, 0x0F");
            instruction_list.Add($"mov al, r11l");
            instruction_list.Add($"and al, 0x08");
            instruction_list.Add($"cmp al, 0x00");

            instruction_list.Add($"je CHECK_SOUTH");
            instruction_list.Add($"mov rdx, rcx");
            instruction_list.Add($"add rdx, 0x10");
            // Is tile connected to part of the same chain?
            // Same room ID
            instruction_list.Add($"movzx r14, byte [rdx+0x04]");
            instruction_list.Add($"cmp r14l, r10l");
            instruction_list.Add($"jne CHECK_SOUTH");
            // Same upper nybble
            instruction_list.Add($"movzx r14, byte [rdx+0x01]");
            instruction_list.Add($"and r14, 0xF0");
            // NEW
            instruction_list.Add($"cmp r14l, 0");
            instruction_list.Add($"je FOUND_BEFORE_E");
            instruction_list.Add($"cmp r14l, r15l");
            instruction_list.Add($"jne CHECK_SOUTH");

            instruction_list.Add($"label FOUND_BEFORE_E");
            // NEW
            
            // Has the tile has already been discovered in some previous iteration?
            instruction_list.Add($"mov r14l, [rdx+0x0F]");
            instruction_list.Add($"cmp r14l, 0x01");
            instruction_list.Add($"je CHECK_SOUTH");

            // Adjacent tile matches all parameters, add it to the stack
            instruction_list.Add($"push rdx");
            instruction_list.Add($"add rsi, 1");

            // Check for connection on the south side of the room
            instruction_list.Add($"label CHECK_SOUTH");
            // Does the current entry have any connections on the south of the room
            instruction_list.Add($"mov al, r11l");
            instruction_list.Add($"and al, 0x04");
            instruction_list.Add($"cmp al, 0x00");
            instruction_list.Add($"je CHECK_WEST");
            // Calculate address for tile to the south of the current one
            instruction_list.Add($"mov rdx, rcx");
            instruction_list.Add($"add rdx, 0x100");
            // Is tile connected to part of the same chain?
                // Same room ID
            instruction_list.Add($"mov r14l, [rdx+0x04]");
            instruction_list.Add($"cmp r14l, r10l");
            instruction_list.Add($"jne CHECK_WEST");
            // Same upper nybble
            instruction_list.Add($"movzx r14, byte [rdx+0x01]");
            instruction_list.Add($"and r14, 0xF0");
            instruction_list.Add($"cmp r14l, r15l");

            instruction_list.Add($"cmp r14l, 0");
            // NEW
            instruction_list.Add($"je FOUND_BEFORE_S");
            instruction_list.Add($"cmp r14l, r15l");
            instruction_list.Add($"jne CHECK_WEST");

            instruction_list.Add($"label FOUND_BEFORE_S");

            // Has the tile has already been discovered in some previous iteration?
            instruction_list.Add($"mov r14l, [rdx+0x0F]");
            instruction_list.Add($"cmp r14l, 0x01");
            instruction_list.Add($"je CHECK_WEST");

            // Adjacent tile matches all parameters, add it to the stack
            instruction_list.Add($"push rdx");
            instruction_list.Add($"add rsi, 1");

            // Check for connection on the west side of the room
            instruction_list.Add($"label CHECK_WEST");
            instruction_list.Add($"mov al, r11l");
            instruction_list.Add($"and al, 0x02");
            instruction_list.Add($"cmp al, 0x00");
            instruction_list.Add($"je CHECK_NORTH");
            instruction_list.Add($"mov rdx, rcx");
            instruction_list.Add($"sub rdx, 0x10");
            // Is tile connected to part of the same chain?
                // Same room ID
            instruction_list.Add($"mov r14l, [rdx+0x04]");
            instruction_list.Add($"cmp r14l, r10l");
            instruction_list.Add($"jne CHECK_NORTH");

            // Same upper nybble
            instruction_list.Add($"movzx r14, byte [rdx+0x01]");
            instruction_list.Add($"and r14, 0xF0");
            // NEW
            instruction_list.Add($"cmp r14l, 0");
            instruction_list.Add($"je FOUND_BEFORE_W");
            instruction_list.Add($"cmp r14l, r15l");
            instruction_list.Add($"jne CHECK_NORTH");

            instruction_list.Add($"label FOUND_BEFORE_W");
            // NEW

            // Has the tile has already been discovered in some previous iteration?
            instruction_list.Add($"mov r14l, [rdx+0x0F]");
            instruction_list.Add($"cmp r14l, 0x01");
            instruction_list.Add($"je CHECK_NORTH");

            // Adjacent tile matches all parameters, add it to the stack
            instruction_list.Add($"push rdx");
            instruction_list.Add($"add rsi, 1");

            // Check for connection on the north side of the room
            instruction_list.Add($"label CHECK_NORTH");
            instruction_list.Add($"mov al, r11l");
            instruction_list.Add($"and al, 0x01");
            instruction_list.Add($"cmp al, 0x00");
            instruction_list.Add($"je MARK_CURRENT");
            instruction_list.Add($"mov rdx, rcx");
            instruction_list.Add($"sub rdx, 0x100");
            // Is tile connected to part of the same chain?
                // Same room ID
            instruction_list.Add($"mov r14l, [rdx+0x04]");
            instruction_list.Add($"cmp r14l, r10l");
            instruction_list.Add($"jne MARK_CURRENT");
                // Same upper nybble
            instruction_list.Add($"movzx r14, byte [rdx+0x01]");
            instruction_list.Add($"and r14, 0xF0");
            // NEW
            instruction_list.Add($"cmp r14l, 0");
            instruction_list.Add($"je FOUND_BEFORE_N");
            instruction_list.Add($"cmp r14l, r15l");
            instruction_list.Add($"jne MARK_CURRENT");

            instruction_list.Add($"label FOUND_BEFORE_N");
            //NEW
            // Has the tile has already been discovered in some previous iteration?
            instruction_list.Add($"mov r14l, [rdx+0x0F]");
            instruction_list.Add($"cmp r14l, 0x01");
           instruction_list.Add($"je MARK_CURRENT");

            // Adjacent tile matches all parameters, add it to the stack
            instruction_list.Add($"push rdx");
            instruction_list.Add($"add rsi, 1");

            instruction_list.Add($"label MARK_CURRENT");

            // Update internal minimap table
            instruction_list.Add($"mov r14, r9");
            instruction_list.Add($"add r14, r14");
            instruction_list.Add($"add r14, r13");
            instruction_list.Add($"mov dx, [r14]");
            instruction_list.Add($"and rdx, 0xFFFF");

            // NEW
            // Check to see minimap bit to the right is filled
            instruction_list.Add($"xor rbx, rbx");
            instruction_list.Add($"mov r11, r8");
            instruction_list.Add($"add r11, 1");
            instruction_list.Add($"bts rbx, r11");
            instruction_list.Add($"and bx, dx");
            instruction_list.Add($"cmp bx, 0");
            instruction_list.Add($"jne MARK_TILE");

            // Check to see minimap bit to the left is filled
            instruction_list.Add($"mov r11, r8");
            instruction_list.Add($"sub r11, 1");
            instruction_list.Add($"bts rbx, r11");
            instruction_list.Add($"and bx, dx");
            instruction_list.Add($"cmp bx, 0");
            instruction_list.Add($"jne MARK_TILE");

            // Check to see minimap bit above is filled
            instruction_list.Add($"mov r11, r14");
            instruction_list.Add($"sub r11, 2");
            instruction_list.Add($"xor rax, rax");
            instruction_list.Add($"mov ax, [r11]");
            instruction_list.Add($"bts rbx, r8");
            instruction_list.Add($"and bx, ax");
            instruction_list.Add($"cmp bx, 0");
            instruction_list.Add($"jne MARK_TILE");

            // Check to see minimap bit below is filled
            instruction_list.Add($"mov r11, r14");
            instruction_list.Add($"add r11, 2");
            instruction_list.Add($"xor rax, rax");
            instruction_list.Add($"mov ax, [r11]");
            instruction_list.Add($"bts rbx, r8");
            instruction_list.Add($"and bx, ax");
            instruction_list.Add($"cmp bx, 0");
            instruction_list.Add($"jne MARK_TILE");

            instruction_list.Add($"jmp DO_NOT_MARK");
            // NEW

            instruction_list.Add($"label MARK_TILE");
            // Mark that the current room has been found
            instruction_list.Add($"mov [rcx+0x0F], byte 0x01");
            instruction_list.Add($"bts rdx, r8");
            instruction_list.Add($"mov [r14], dx");

            instruction_list.Add($"label DO_NOT_MARK");

            // Get the next value from the stack and set up its variables accordingly
            instruction_list.Add($"pop rcx");


            instruction_list.Add($"mov rdi, rcx");
            instruction_list.Add($"sub rdi, r12");

            instruction_list.Add($"mov r8, rdi");
            instruction_list.Add($"shr r8, 4");
            instruction_list.Add($"and r8, 0x0F");

            instruction_list.Add($"mov r9, rdi");
            instruction_list.Add($"shr r9, 8");
            instruction_list.Add($"and r9, 0xFF");
            
            instruction_list.Add($"sub rsi, 1");
            instruction_list.Add($"cmp rsi, 0");
            instruction_list.Add($"jne LOOP_START");

            // End of function, undo stack changes
            instruction_list.Add($"label END_OF_FUNCTION");
            instruction_list.Add($"mov rsp, rbp");
            instruction_list.Add($"pop rbp");
            instruction_list.Add($"pop r9");
            instruction_list.Add($"pop r8");
            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rcx");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");
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
    }
}
