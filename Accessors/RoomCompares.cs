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

namespace p4gpc.dungeonloader.Accessors
{
    public class RoomCompares : Accessor
    {
        /*
        To do:
            -Find comparisons
            -Figure out what each comparison checks for
            -Write appropriate functions to replace each comparison
            -Replace comparisons

            -Note comparisons that might need expansion later, like the ones for minimap tile scaling
         */

        private List<DungeonRoom> _rooms;
        private nuint _roomSizeTable;
        private nuint _debugInfoAddress;
        // This should be temporary, the hypothetical plan is to entirely
        // overhaul the logic for minimap updating to be more flexible.
        // This is essentially a quick-fix to get things moving
        private nuint _minimapUpdateJumpTable;

        private IReverseWrapper<DebugLogFunc> _debugLogWrapper;
        private string _debugLogCallMnemonic;

        public RoomCompares(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)
        {
            _rooms = jsonImporter.GetRooms();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Room compare hooks established.");
        }

        protected override void Initialize()
        {
            List<long> functions;
            String search_string;
            long address;
            long function;
            int jump_offset;
            int jump_offset2;

            _debugInfoAddress = _memory.Allocate(8);
            _utils.LogDebug($"DebugAddress (RoomCompares) : {_debugInfoAddress.ToString("X8")}", 1);

            _roomSizeTable = _memory.Allocate(_rooms.Count*2);
            for (int i = 0; i < _rooms.Count; i++)
            {
                _memory.SafeWrite((_roomSizeTable + (nuint)i*2), _rooms[i].sizeX);
                _memory.SafeWrite((_roomSizeTable + (nuint)i*2+1), _rooms[i].sizeY);
            }

            


            // Needs addresses for 1x1, 2x2, and 3x3
            _minimapUpdateJumpTable = _memory.Allocate(24);
            _utils.LogDebug($"MinimapUpdateJumpTable at: {_minimapUpdateJumpTable.ToString("X8")}", 3);
            address = _utils.SigScan("48 8B 05 3D C7 DB 04 42 0F B7 0C 58 4A 8D 14 58", "ReplaceMinimapUpdateCheck_1x1");
            _memory.SafeWrite((_minimapUpdateJumpTable), address);

            address= _utils.SigScan("4C 8B 0D 16 C7 DB 04 48 8D 35 8F 01 BD FF", "ReplaceMinimapUpdateCheck_2x2");
            _memory.SafeWrite((_minimapUpdateJumpTable + 8), address);

            address = _utils.SigScan("0F B6 C0 83 C0 F7 83 F8 05 0F 87 E5 04 00 00", "ReplaceMinimapUpdateCheck_3x3");
            _memory.SafeWrite((_minimapUpdateJumpTable + 16), address);


            search_string = "41 80 F9 09 0F 82 ?? ?? ?? ?? 48 8D ?? ?? 48 03 ?? 45 0F B6 94 C3 ?? ?? ?? ?? 41 0F B6 ?? 83 C0 F7 83 F8 05 0F 87 ?? ?? ?? ??";
            function = _utils.SigScan(search_string, $"RoomCompareA");
            _memory.Read((nuint)(function+6), out jump_offset);
            //LogOpcodeRunsB(function, search_string);
            ReplaceStartupSearchA(function, jump_offset, search_string);
            _utils.LogDebug($"Location: {function.ToString("X8")}", 3);

            search_string = "40 80 FF 09 0F 82 ?? ?? ?? ?? 40 0F B6 ?? 83 C0 F7 83 F8 05 0F 87 ?? ?? ?? ??";
            function = _utils.SigScan(search_string, $"RoomCompareB");
            _memory.Read((nuint)(function+6), out jump_offset);
            //LogOpcodeRunsC(function, search_string);
            ReplaceStartupSearchB(function, jump_offset, search_string);
            _utils.LogDebug($"Location: {function.ToString("X8")}", 3);

            search_string = "80 F9 09 72 51 0F B6 ?? 4C 8D 1D ?? ?? ?? ?? 83 C0 F7 83 F8 05 0F 87 ?? ?? ?? ??";
            function = _utils.SigScan(search_string, $"RoomCompareC");
            _memory.Read((nuint)(function+4), out jump_offset);
            jump_offset &= 0xFF;
            //LogOpcodeRunsD(function, search_string);
            ReplaceStartupSearchC(function, jump_offset, search_string);
            _utils.LogDebug($"Location: {function.ToString("X8")}", 3);

            search_string = "83 C0 F7 83 F8 05 0F 87 ?? ?? ?? ??";
            function = _utils.SigScan(search_string, $"RoomCompareGeneral");
            _memory.Read((nuint)(function+8), out jump_offset);
            // _memory.Read((nuint)(function+17), out jump_offset2);
            //LogOpcodeRunsA(function, search_string);
            ReplaceStartupSearch(function, jump_offset, search_string);
            _utils.LogDebug($"Location: {function.ToString("X8")}", 3);

            // Attempting a more generic search gets us int
            search_string = "3C 06 0F 87 ?? ?? ?? ?? 3C 02 0F 85 ?? ?? ?? ??";
            function = _utils.SigScan(search_string, $"RoomCompareGeneral");
            ReplaceMinimapUpdateCheck(function, search_string);
            _utils.LogDebug($"Location: {function.ToString("X8")}", 3);


        }

        void LogOpcodeRunsA(Int64 functionAddress, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();

            instruction_list.Add($"use64");

            instruction_list.Add($"cmp {AccessorRegister.rax}, 0xF");
            instruction_list.Add($"je continue");


            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {_debugInfoAddress}");
            instruction_list.Add($"mov [rax], dword {functionAddress & 0xFFFFFFFF}");
            instruction_list.Add($"pop rax");


            instruction_list.Add($"label continue");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.ExecuteFirst, _utils.GetPatternLength(pattern)).Activate());

        }
        void LogOpcodeRunsB(Int64 functionAddress, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();

            instruction_list.Add($"use64");

            instruction_list.Add($"cmp {AccessorRegister.r9}, 0xF");
            instruction_list.Add($"je continue");


            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {_debugInfoAddress}");
            instruction_list.Add($"mov [rax], dword {functionAddress & 0xFFFFFFFF}");
            instruction_list.Add($"pop rax");


            instruction_list.Add($"label continue");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.ExecuteFirst, _utils.GetPatternLength(pattern)).Activate());

        }

        void LogOpcodeRunsC(Int64 functionAddress, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();

            instruction_list.Add($"use64");

            instruction_list.Add($"cmp {AccessorRegister.rdi}, 0xF");
            instruction_list.Add($"je continue");


            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {_debugInfoAddress}");
            instruction_list.Add($"mov [rax], dword {functionAddress & 0xFFFFFFFF}");
            instruction_list.Add($"pop rax");


            instruction_list.Add($"label continue");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.ExecuteFirst, _utils.GetPatternLength(pattern)).Activate());

        }

        void LogOpcodeRunsD(Int64 functionAddress, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();

            instruction_list.Add($"use64");

            instruction_list.Add($"cmp {AccessorRegister.rcx}, 0xF");
            instruction_list.Add($"je continue");


            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {_debugInfoAddress}");
            instruction_list.Add($"mov [rax], dword {functionAddress & 0xFFFFFFFF}");
            instruction_list.Add($"pop rax");


            instruction_list.Add($"label continue");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.ExecuteFirst, _utils.GetPatternLength(pattern)).Activate());

        }

        void ReplaceMinimapUpdateCheck(Int64 functionAddress, string pattern)
        {
            /*
             * 3C 06 0F 87 ?? ?? ?? ?? 3C 02 0F 85 ?? ?? ?? ??
                
                Definitely look into this function further down the line, this is definitely something
                that will need to be changed for more unique rooms down the line


                Jump to for 1x1: 48 8B 05 3D C7 DB 04 42 0F B7 0C 58 4A 8D 14 58
                Jump to for 2x2 (2): Just return normally
                Jump to for 2x2 (7/8): 4C 8B 0D 16 C7 DB 04 48 8D 35 8F 01 BD FF
                Jump to for 3x3: 0F B6 C0 83 C0 F7 83 F8 05 0F 87 E5 04 00 00

                3x3 will need heavy modifications down the line, this will still be using 
             */
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();

            instruction_list.Add($"use64");

            instruction_list.Add("cmp rax, 0xF");
            instruction_list.Add("jne noDebug");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"{_debugLogCallMnemonic}");
            instruction_list.Add($"pop rax");
            instruction_list.Add("label noDebug");

            instruction_list.Add($"cmp {AccessorRegister.rax}, 2");
            instruction_list.Add($"je continue");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");

            instruction_list.Add($"xor rbx, rbx");
            instruction_list.Add($"and rax, 0xFF");
            instruction_list.Add($"add rax, rax");
            instruction_list.Add($"mov bl, [{_roomSizeTable} + rax]");

            instruction_list.Add($"sub rbx, 1");
            instruction_list.Add($"imul rbx, rbx, 8");
            // Temporary check, will want flexibility down the line

            instruction_list.Add($"mov rax, [{_minimapUpdateJumpTable} + rbx]");
            instruction_list.Add($"mov rbx, [rsp+8]");
            instruction_list.Add($"mov [rsp+8], rax");
            instruction_list.Add($"mov rax, rbx");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"ret");



            instruction_list.Add($"label continue");
            // room 2 is handled by just going back to the line of thought of
            // the segment we replaced

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        
        void ReplaceStartupSearch(Int64 functionAddress, int jump_offset, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            Int64 jump_point = functionAddress + _utils.GetPatternLength(pattern) + jump_offset;
            instruction_list.Add($"use64");
            // So far I've only seen this with EAX/RAX, but may have to change if something new is found or
            // if a hypothetical update breaks this trend
            // instruction_list.Add($"add rax");

            /*
            instruction_list.Add("cmp rax, 0xF");
            instruction_list.Add("jne noDebug");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"{_debugLogCallMnemonic}");
            instruction_list.Add($"pop rax");
            instruction_list.Add("label noDebug");
             */

            instruction_list.Add($"push rbx");
            instruction_list.Add($"push rax");

            instruction_list.Add($"xor rbx, rbx");
            instruction_list.Add($"and rax, 0xFF");
            instruction_list.Add($"add rax, rax");
            instruction_list.Add($"mov bl, [{_roomSizeTable} + rax]");

            instruction_list.Add($"cmp {AccessorRegister.rbx}, 3");
            instruction_list.Add($"je continue");

            instruction_list.Add($"pop rax");
            instruction_list.Add($"pop rbx");

            // This opcode is proving problematic
            // instruction_list.Add($"push {jump_point}");
            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {jump_point}");
            instruction_list.Add($"mov [rsp+8], rax");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"ret");
            instruction_list.Add($"label continue");

            /*
                Feel an explanation for this instruction in particular is warranted, especially since
                this will ideally become unnecessary at some point down the line. With some of the
                replaced instructions, the non-negative value obtained from the hardcoded comparison
                is used to grab an address from a table. I currently do not know what this particular
                line of code does, so I can't rework the condition checks just yet. As a compromise,
                until I have everything roughly up and running, our 3x3 rooms will continue to have
                the value subtracted so we don't break the calculations, however this does prevent us
                from adding 3x3 (and presumably larger) rooms at the moment.
             */

            instruction_list.Add($"pop rax");
            instruction_list.Add($"add rax, -9");

            instruction_list.Add($"pop rbx");

            // instruction_list.Add($"");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        void ReplaceStartupSearchA(Int64 functionAddress, int jump_offset, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            // To do, refactor so that we aren't just adding the constant size of the
            // instructions and instead 
            Int64 jump_point = functionAddress + 10 + jump_offset;
            // Int64 jump_point2 = functionAddress + 36 + jump_offset2;
            instruction_list.Add($"use64");


            /*
            instruction_list.Add("cmp r9, 0xF");
            instruction_list.Add("jne noDebug");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"{_debugLogCallMnemonic}");
            instruction_list.Add($"pop rax");
            instruction_list.Add("label noDebug");
             */

            // So far I've only seen this with EAX/RAX, but may have to change if something new is found or
            // if a hypothetical update breaks this trend
            // instruction_list.Add($"add rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"push r9");
            instruction_list.Add($"and r9, 0xFF");
            instruction_list.Add($"add r9, r9");
            instruction_list.Add($"xor rbx, rbx");
            instruction_list.Add($"mov bl, [{_roomSizeTable} + r9]");

            instruction_list.Add($"cmp {AccessorRegister.rbx}, 3");
            instruction_list.Add($"je next_point");

            instruction_list.Add($"pop r9");
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

            instruction_list.Add($"pop r9");

            instruction_list.Add($"lea rax, [rcx+rsi]");
            instruction_list.Add($"add rax, rax");
            instruction_list.Add($"imul rax, rax, 8");
            
            instruction_list.Add($"add rax, 0x011AB3A0");
            instruction_list.Add($"mov r10l, byte [r11+rax]");
            instruction_list.Add($"push r9");
            instruction_list.Add($"and r9, 0xFF");
            instruction_list.Add($"mov rax, r9");
            instruction_list.Add($"pop r9");

            /*
                Feel an explanation for this instruction in particular is warranted, especially since
                this will ideally become unnecessary at some point down the line. With some of the
                replaced instructions, the non-negative value obtained from the hardcoded comparison
                is used to grab an address from a table. I currently do not know what this particular
                line of code does, so I can't rework the condition checks just yet. As a compromise,
                until I have everything roughly up and running, our 3x3 rooms will continue to have
                the value subtracted so we don't break the calculations, however this does prevent us
                from adding 3x3 (and presumably larger) rooms at the moment.
             */
            instruction_list.Add($"add rax, -9");

            instruction_list.Add($"pop rbx");

            // instruction_list.Add($"");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        void ReplaceStartupSearchB(Int64 functionAddress, int jump_offset, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            Int64 jump_point = functionAddress + 10 + jump_offset;
            instruction_list.Add($"use64");


            /*
            instruction_list.Add("cmp rdi, 0xF");
            instruction_list.Add("jne noDebug");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"{_debugLogCallMnemonic}");
            instruction_list.Add($"pop rax");
            instruction_list.Add("label noDebug");
             */

            instruction_list.Add($"push rbx");
            instruction_list.Add($"push rdi");
            instruction_list.Add($"and rdi, 0xFF");
            instruction_list.Add($"add rdi, rdi");
            instruction_list.Add($"xor rbx, rbx");
            instruction_list.Add($"mov bl, [{_roomSizeTable} + rdi]");

            instruction_list.Add($"cmp {AccessorRegister.rbx}, 3");
            instruction_list.Add($"je next_point");


            instruction_list.Add($"pop rdi");
            instruction_list.Add($"pop rbx");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {jump_point}");
            instruction_list.Add($"mov [rsp+8], rax");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"ret");
            instruction_list.Add($"label next_point");

            instruction_list.Add($"pop rdi");
            instruction_list.Add($"mov al, dil");
            instruction_list.Add($"and rax, 0xFF");


            /*
                Feel an explanation for this instruction in particular is warranted, especially since
                this will ideally become unnecessary at some point down the line. With some of the
                replaced instructions, the non-negative value obtained from the hardcoded comparison
                is used to grab an address from a table. I currently do not know what this particular
                line of code does, so I can't rework the condition checks just yet. As a compromise,
                until I have everything roughly up and running, our 3x3 rooms will continue to have
                the value subtracted so we don't break the calculations, however this does prevent us
                from adding 3x3 (and presumably larger) rooms at the moment.
             */
            instruction_list.Add($"add eax, -9");

            instruction_list.Add($"pop rbx");

            // instruction_list.Add($"");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        void ReplaceStartupSearchC(Int64 functionAddress, int jump_offset, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            Int64 jump_point = functionAddress + 5 + jump_offset;
            instruction_list.Add($"use64");

            /*
             
            instruction_list.Add("cmp rcx, 0xF");
            instruction_list.Add("jne noDebug");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"{_debugLogCallMnemonic}");
            instruction_list.Add($"pop rax");
            instruction_list.Add("label noDebug");
             */

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
            instruction_list.Add($"mov r11, 0x140000000");

            /*
                Feel an explanation for this instruction in particular is warranted, especially since
                this will ideally become unnecessary at some point down the line. With some of the
                replaced instructions, the non-negative value obtained from the hardcoded comparison
                is used to grab an address from a table. I currently do not know what this particular
                line of code does, so I can't rework the condition checks just yet. As a compromise,
                until I have everything roughly up and running, our 3x3 rooms will continue to have
                the value subtracted so we don't break the calculations, however this does prevent us
                from adding 3x3 (and presumably larger) rooms at the moment.
             */
            instruction_list.Add($"add eax, -9");

            instruction_list.Add($"pop rbx");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        void ReplaceStartupSearchD(Int64 functionAddress, int jump_offset, int jump_offset2, string pattern)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            Int64 jump_point = functionAddress + 8 + jump_offset;
            // Second, local jump point
            Int64 jump_point2 = functionAddress + 17 + jump_offset2;
            instruction_list.Add($"use64");

            /*
            instruction_list.Add("cmp rax, 0xF");
            instruction_list.Add("jne noDebug");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"{_debugLogCallMnemonic}");
            instruction_list.Add($"pop rax");
            instruction_list.Add("label noDebug");
             */

            instruction_list.Add($"push rbx");
            instruction_list.Add($"push rax");
            instruction_list.Add($"and rax, 0xFF");
            instruction_list.Add($"add rax, rax");
            instruction_list.Add($"xor rbx, rbx");
            instruction_list.Add($"mov bl, [{_roomSizeTable} + rax]");

            instruction_list.Add($"cmp {AccessorRegister.rbx}, 3");
            instruction_list.Add($"je next_point");


            instruction_list.Add($"pop rax");
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

            instruction_list.Add($"pop rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"mov cl, byte [rbx+0x9]");
            instruction_list.Add($"and rcx, 0xFF");
            instruction_list.Add($"cmp cl, 1");
            instruction_list.Add($"je next_point2");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {jump_point2}");
            instruction_list.Add($"mov [rsp-8], rax");
            instruction_list.Add($"pop rax");


            instruction_list.Add($"label next_point2");
            instruction_list.Add($"movzx eax, al");
            /*
                Feel an explanation for this instruction in particular is warranted, especially since
                this will ideally become unnecessary at some point down the line. With some of the
                replaced instructions, the non-negative value obtained from the hardcoded comparison
                is used to grab an address from a table. I currently do not know what this particular
                line of code does, so I can't rework the condition checks just yet. As a compromise,
                until I have everything roughly up and running, our 3x3 rooms will continue to have
                the value subtracted so we don't break the calculations, however this does prevent us
                from adding 3x3 (and presumably larger) rooms at the moment.
             */
            instruction_list.Add($"add eax, -9");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void DebugLog(Int64 address)
        {
            _utils.LogDebug($"ACCESSED FUNCTION: {address.ToString("X8")}", 1);
        }
        [Function(Register.rax, Register.rax, false)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DebugLogFunc(Int64 rax);
    }

}
