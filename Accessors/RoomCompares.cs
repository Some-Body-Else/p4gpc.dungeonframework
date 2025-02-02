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

using p4gpc.dungeonframework.Exceptions;
using p4gpc.dungeonframework.JsonClasses;
using p4gpc.dungeonframework.Configuration;
using System.Reflection;
using Reloaded.Memory.Pointers;

namespace p4gpc.dungeonframework.Accessors
{
    public class RoomCompares : Accessor
    {
        /*
        To do:
            - Make sure we get all the room # comparisons.
            - Maybe merge with other files, since this as a name is quite nebulous
         */

        private List<DungeonRoom> _rooms;
        private nuint _roomSizeTable;

        // This should be temporary, the hypothetical plan is to entirely
        // overhaul the logic for minimap updating to be more flexible.
        // This is essentially a quick-fix to get things moving
        private nuint _minimapUpdateJumpTable;


        public RoomCompares(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)
        {
            _rooms = jsonImporter.GetRooms();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Room compare hooks established.", Config.DebugLevels.AlertConnections);
        }

        protected override void Initialize()
        {
            List<long> functions;
            String search_string;
            long address;
            long function;
            long jump_target;


            _roomSizeTable = _memory.Allocate(_rooms.Count*2);
            for (int i = 0; i < _rooms.Count; i++)
            {
                _memory.SafeWrite((_roomSizeTable + (nuint)i*2), _rooms[i].sizeX);
                _memory.SafeWrite((_roomSizeTable + (nuint)i*2+1), _rooms[i].sizeY);
            }



            // Search for jump target
            search_string = "44 88 6C 24 38 45 0F B6 C5 40 88 6C 24 30 40 0F B6 D5";
            jump_target = _utils.SigScan(search_string, $"RoomCompareJumpTo");
            search_string = "41 80 F9 09 0F 82 ?? ?? ?? ?? 48 8D ?? ?? 48 03 ?? 45 0F B6 94 C3 ?? ?? ?? ?? 41 0F B6 ?? 83 C0 F7 83 F8 05 0F 87 ?? ?? ?? ??";
            function = _utils.SigScan(search_string, $"RoomCompareA");

            //LogOpcodeRunsB(function, search_string);
            ReplaceMinimapTileImagePrep(function, jump_target, search_string);
            _utils.LogDebug($"Replaced code [{search_string}] at: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

             
            search_string = "80 F9 09 72 51 0F B6 ?? 4C 8D 1D ?? ?? ?? ?? 83 C0 F7 83 F8 05 0F 87 ?? ?? ?? ??";
            function = _utils.SigScan(search_string, $"RoomCompareB");
            _memory.Read((nuint)(function+4), out jump_target);
            jump_target &= 0xFF;
            ReplaceStartupSearchB(function, (int)jump_target, search_string);
            _utils.LogDebug($"Replaced code [{search_string}] at: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

        }
        void ReplaceMinimapTileImagePrep(Int64 functionAddress, Int64 jump_point, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            Int64 func_call_addr = 0x140432C70;

            // To do, refactor so that we aren't just adding the constant size of the
            // instructions and instead 
            // Int64 jump_point2 = functionAddress + 36 + jump_offset2;
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"push rbx");
            instruction_list.Add($"push r9");
            instruction_list.Add($"and r9, 0xFF");
            instruction_list.Add($"xor rbx, rbx");
            instruction_list.Add($"mov bl, [{_roomSizeTable} + r9]");

            // Check to see if the room has multiple images on the minimap (doors)
            instruction_list.Add($"cmp {AccessorRegister.rbx}, 1");
            instruction_list.Add($"je multi_image");

            // Single image, room will have everything revealed at once
            instruction_list.Add($"pop r9");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {jump_point}");
            instruction_list.Add($"mov [rsp+8], rax");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"ret");

            instruction_list.Add($"label multi_image");
            instruction_list.Add($"pop r9");
            instruction_list.Add($"pop rbx");
            // Multi-image, have to load each piece in one at a time

            // Think we have some steps to put here first, but can't remember what.
            instruction_list.Add($"lea rax, [rcx+rsi]");
            instruction_list.Add($"add rax, rax");
            instruction_list.Add($"imul rax, rax, 8");

            instruction_list.Add($"add rax, 0x011AB3A0");
            instruction_list.Add($"mov r10l, byte [r11+rax]");

            // Set a counter to 1 for the part counting
            instruction_list.Add($"mov [rsp+0x20], byte 0x01");

            instruction_list.Add($"label loop_start");
            // Everything from here down will need to be checked to see if it assembles properly
            instruction_list.Add($"movzx rax, byte [r14]");
            instruction_list.Add($"movzx rsi, byte r13b");
            instruction_list.Add($"mov [rsp+0x38], byte r13b");
            instruction_list.Add($"movzx r9, byte r10b");
            instruction_list.Add($"mov [rsp+0x30], byte r10b");
            instruction_list.Add($"mov rcx, r15");
            instruction_list.Add($"mov [rsp+0x28], byte al");
;

            // Get the offset coordinates
            // CHANGE_HERE
            instruction_list.Add($"mov edi, 0x0");
            instruction_list.Add($"mov ebx, 0x0");

            instruction_list.Add($"lea rdx, [rdi + rbp]");
            instruction_list.Add($"lea r8, [rbx + rsi]");



            // Our VERY MESSY function call, which I hate but can't think of an alternative for
            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            // Target call address
            instruction_list.Add($"mov rax, {func_call_addr}");
            instruction_list.Add($"mov [rsp+8], rax");
            // Target return address
            // Hate the way we're doing this, but can't think of an alternative
            instruction_list.Add($"lea rax,[rip + 0x7]");
            instruction_list.Add($"mov [rsp+16], rax");
            instruction_list.Add($"pop rax");
            instruction_list.Add($"ret");

            instruction_list.Add($"label ret_addr");

            // Post-call stuff
            instruction_list.Add($"lea rcx, [rbx + r13]");
            instruction_list.Add($"mov r8, rcx");
            instruction_list.Add($"lea rcx, [rdi + rbp]");
            instruction_list.Add($"mov rdx, rcx");
            instruction_list.Add($"mov rcx, [rsp+0x40]");
            instruction_list.Add($"shl r8, 4");
            instruction_list.Add($"add r8, rdx");
            instruction_list.Add($"mov [rcx+r8*8+0x208], rax");


            // Store the part # (NOT ALWAYS 1)
            instruction_list.Add($"mov dil, byte [rsp+0x20]");
            instruction_list.Add($"add dil, 1");
            instruction_list.Add($"mov [rsp+0x20], byte dil");


            // Loop condition check
            instruction_list.Add($"and rdi, 0xFF");
            // Set this up as the proper value
            instruction_list.Add($"mov rbx, 0xFF");
            instruction_list.Add($"cmp rbx, rdi");
            instruction_list.Add($"jne loop_start");

            // end of loop

            instruction_list.Add($"mov rsi, [rsp+0x158]");
            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {jump_point}");
            instruction_list.Add($"mov [rsp+8], rax");
            instruction_list.Add($"pop rax");
            instruction_list.Add($"ret");

            /*


            */

            /*
            Old code, when we checcked to see if room size was 3 instead 

            instruction_list.Add($"push r9");
            instruction_list.Add($"and r9, 0xFF");
            instruction_list.Add($"mov rax, r9");
            instruction_list.Add($"pop r9");
            instruction_list.Add($"add rax, -9");
            instruction_list.Add($"pop rbx");
             
             */

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }


        void ReplaceStartupSearchB(Int64 functionAddress, int jump_offset, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            Int64 jump_point = functionAddress + 5 + jump_offset;
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax"); ;

            instruction_list.Add($"push rbx");
            instruction_list.Add($"push rcx");
            instruction_list.Add($"and rcx, 0xFF");
            instruction_list.Add($"add rcx, rcx");
            instruction_list.Add($"xor rbx, rbx");
            instruction_list.Add($"mov bl, [{_roomSizeTable} + rcx]");

            instruction_list.Add($"cmp {AccessorRegister.rbx}, 3");
            instruction_list.Add($"je next_point");


            instruction_list.Add($"pop rcx");
            instruction_list.Add($"pop rbx");

            // This opcode is proving problematic
            // instruction_list.Add($"push {jump_point}");
            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {jump_point}");
            instruction_list.Add($"mov [rsp+8], rax");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"ret");
            instruction_list.Add($"label next_point");

            instruction_list.Add($"pop rcx");
            instruction_list.Add($"mov al, cl");
            instruction_list.Add($"and rax, 0xFF");
            instruction_list.Add($"add eax, -9");

                instruction_list.Add($"pop rbx");
                _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
            }


        }
}
