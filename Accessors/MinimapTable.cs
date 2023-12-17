﻿using Reloaded.Hooks;
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
            -Figure search targets for minimap name load-in
            --Need to update the list of name that load in
            -Figure out what accesses minimap address table Minimap Tile Property Accessor Table
            --Re-allocate to sepearate space to ensure there's enough entries for all new rooms

         */

        private List<DungeonMinimap> _minimaps;
        private nuint _newMinimapTable;
        // private nuint _newMinimapLookupTable;
        private nuint _roomHasMultipleTexturesTable;
        private nuint _minimapTextureOffsetTableLookupTable;
        // Okay, THIS cannot go without some sort of note.
        // After all of the textures are checked from smap.bin, another loop goes and sets
        // up a table of 8-byte addresses. There's one for each minimap texture, but I have
        // little to no idea what the devil they are. I'm moving it because the current location appears
        // to run out of space for any additional rooms, and I need to modify this section anyways
        // because it checks from the number of minimap textures, which is normally hardcoded
        private nuint _minimapUnknownPerTextureTable;
        private int minimapCounter = 0;



        public MinimapTable(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {
            _minimaps = jsonImporter.GetMinimap();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Minimap hooks established.");
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
            _newMinimapLookupTable = _memory.Allocate(minimapCounter*16);
            _minimapUnknownPerTextureTable = _memory.Allocate(minimapCounter*8);

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


            _roomHasMultipleTexturesTable = _memory.Allocate(_minimaps.Count);
            _minimapTextureOffsetTableLookupTable = _memory.Allocate(8*_minimaps.Count);
            Int32 offsetLookup = 0;

            for (int i = 0; i < _minimaps.Count; i++)
            {
                _memory.SafeWrite(_minimapTextureOffsetTableLookupTable + (nuint)i*4, (Int32)offsetLookup);

                if (_minimaps[i].multipleNames)
                {
                    _memory.SafeWrite(_roomHasMultipleTexturesTable + (nuint)i, (byte)1);
                    for (int j = 0; j < _minimaps[i].names.Count; j++)
                    {
                        offsetLookup += 8;
                    }
                }
                else
                {
                    _memory.SafeWrite(_roomHasMultipleTexturesTable + (nuint)i, (byte)0);
                }
                offsetLookup += 8;

            }

            // "48 8D 3D ?? ?? ?? ?? 48 8B F3 48 2B F7 48 8D 2D ?? ?? ?? ?? 0F 1F 00"
            // This is the search target for the code that touches the startup table

            func = _utils.SigScan("48 8D 3D ?? ?? ?? ?? 48 8B F3 48 2B F7 48 8D 2D ?? ?? ?? ?? 0F 1F 00", $"StartupMinimapSearch");
            ReplaceStartupSearch(func, 23);
            _utils.LogDebug($"Location: {func.ToString("X8")}", 3);

            // 4C 8D 35 91 CB DB 04 89 54 24 70 48 8D 7B 20 4C 2B F3 8D 6E 1E
            search_string = "4C 8D 35 91 CB DB 04 89 54 24 70 48 8D 7B 20 4C 2B F3 8D 6E 1E";
            func = _utils.SigScan(search_string, $"StartupMinimapCapSwap");
            StartupMinimapCapSwap(func, search_string);
            _utils.LogDebug($"Location: {func.ToString("X8")}", 3);

            search_string = "3C 09 0F 82 ?? ?? ?? ?? 0F B6 4B 09 80 F9 01 75 33 0F B6 C0 83 C0 F7 83 F8 05 0F 87 ?? ?? ?? ??";
            func = _utils.SigScan(search_string, $"ReplaceMinimapPathSearch");
            _memory.SafeRead((nuint)(func + 28), out offset);
            ReplaceMinimapPathSearch(func, offset, search_string);
            _utils.LogDebug($"Location: {func.ToString("X8")}", 3);


            // 4C 8D 15 1D 19 4E 00 44 8B CD
            search_string = "4C 8D 15 1D 19 4E 00 44 8B CD";
            func = _utils.SigScan(search_string, $"ReplaceMinimapPathListLoadIn");
            ReplacePathListLoadIn(func, search_string);
            _utils.LogDebug($"Location: {func.ToString("X8")}", 3);


            // 49 83 C2 08 4C 63 D8 41 83 F9 1E
            search_string = "49 83 C2 08 4C 63 D8 41 83 F9 1E";
            func = _utils.SigScan(search_string, $"ReplaceMinimapPathListSizeCheck");
            ReplacePathListSizeCheck(func, search_string);
            _utils.LogDebug($"Location: {func.ToString("X8")}", 3);

            search_string = "4E 8B 84 DF 90 C4 1E 05 48 8D 7B 40";
            func = _utils.SigScan(search_string, $"ReplaceMinimapUnknownTableLookup");
            ReplaceMinimapUnknownTableLookup(func, search_string);
            _utils.LogDebug($"Location: {func.ToString("X8")}", 3);
        }

            void ReplaceStartupSearch(Int64 functionAddress, int length)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");
            instruction_list.Add($"mov rdi, {_newMinimapLookupTable}");
            instruction_list.Add($"mov rsi, rbx");
            instruction_list.Add($"sub rsi, rdi");
            instruction_list.Add($"mov rbp, {_newMinimapPathLookupTable}");
            // instruction_list.Add($"");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, length).Activate());
        }

        void StartupMinimapCapSwap(Int64 functionAddress, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");

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
            /*
                private nuint _roomHasMultipleTexturesTable;
                private nuint _minimapeTextureOffsetTable;
                private nuint _minimapTextureNameTable;
                private nuint _minimapUpdateJumpTable;
                private nuint _minimapeTextureOffsetTableLookupTable;
             */

            instruction_list.Add($"use64");
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

        void ReplacePathListLoadIn(Int64 functionAddress, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");
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
            instruction_list.Add($"mov r8, [{_minimapUnknownPerTextureTable} + r11*0x8]");
            instruction_list.Add($"mov rdi, rbx");
            instruction_list.Add($"add rdi, 0x40");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
    }
}
