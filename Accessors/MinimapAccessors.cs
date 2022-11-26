using p4gpc.dungeonloader.Configuration;
using p4gpc.dungeonloader.Exceptions;
using p4gpc.dungeonloader.JsonClasses;

using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Memory.Sources;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;
using System.Net;
using System.Reflection;

namespace p4gpc.dungeonloader.Accessors
{
    public class MinimapAccessors
    {

        /// <summary>
        /// Currently an unused structure, but being kept around for the moment in case map size expansion is in the cards.
        /// Probably not even close to the final form of the struct, but at least has the bare minimum for what is needed.
        /// </summary>
        /*
        private struct mapByte
        {
            public byte tileID;
            public bool isFound;
        }

        private mapByte[,] internalMinimap = new mapByte[24, 16];
        */
        

        private IReloadedHooks? _hooks;
        private Utilities? _utils;
        private IMemory _memory;
        private Config _configuration;
        private JsonImporter _jsonImporter;
        private List<IReverseWrapper> _reverseWrapperList;
        private List<IAsmHook> _functionHookList;
        private List<DungeonMinimap> _minimap;
        private List<String> _minimap_names;
        private List<String> _commands;
        private List<int> _minimapImageInfoAddresses;
        private bool discrepencyNoted;
        private nuint mapDataTable;

        public MinimapAccessors(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)
        {
            _hooks = hooks;
            _utils = utils;
            _memory = memory;
            _configuration = config;
            _jsonImporter = jsonImporter;
            _reverseWrapperList = new List<IReverseWrapper>();
            _functionHookList = new List<IAsmHook>();
            _minimap = _jsonImporter.GetMinimap();
            _minimap_names = new List<String>();
            _commands = new List<String>();
            _minimapImageInfoAddresses = new List<int>();

            for (int i = 0; i < _minimap.Count(); i++)
            {
                /*
                 Time to explain some shennanigans here, in the default game state, each multi-texture room
                 has a texture that shows the whole tile as a single piece. These are unused, but are still loaded
                 messed with on startup. As such, I'm including them for the moment until some more modifications
                 are made, so even multi-texture room minimaps will have a valid 'name' variable
                 */
                _minimap_names.Add(_minimap[i].name);
                if (_minimap[i].multipleNames)
                {
                    for (int j = 0; j < _minimap[i].names.Count(); j++)
                    {
                        if (!_minimap_names.Contains(_minimap[i].names[j]))
                            _minimap_names.Add(_minimap[i].names[j]);
                    }
                }
            }
            discrepencyNoted = false;

            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());
            _utils.Log("Minimap-adjacent hooks established.");
        }

        private void Initialize()
        {
            //Debugger.Launch();
            
            List<long> vanillaTableUsage;
            long address;
            List<String> functions = _jsonImporter.GetMinimapFunctions();

            IReverseWrapper<GetTileMapIndexStartFunction> reverseWrapperGetTileMapIndexStart = _hooks.CreateReverseWrapper<GetTileMapIndexStartFunction>(GetTileMapIndexStart);
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetTileMapIndexStart, out reverseWrapperGetTileMapIndexStart)}");
            _reverseWrapperList.Add(reverseWrapperGetTileMapIndexStart);


            IReverseWrapper<GetMinimapImageAddressFunction> reverseWrapperGetMinimapImageAddress = _hooks.CreateReverseWrapper<GetMinimapImageAddressFunction>(GetMinimapImageAddress);
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetMinimapImageAddress, out reverseWrapperGetMinimapImageAddress)}");
            _reverseWrapperList.Add(reverseWrapperGetMinimapImageAddress);


            IReverseWrapper<GetMinimapInfoAddressFunction> reverseWrapperGetMinimapInfoAddress = _hooks.CreateReverseWrapper<GetMinimapInfoAddressFunction>(GetMinimapInfoAddress);
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetMinimapInfoAddress, out reverseWrapperGetMinimapInfoAddress)}");
            _reverseWrapperList.Add(reverseWrapperGetMinimapInfoAddress);


            mapDataTable = _memory.Allocate(4 * _minimap_names.Count());
            _utils.LogDebug($"mapDataTable address: {mapDataTable.ToString("X8")}");

            address = _utils.SigScan(functions[0], "TableStep1");
            TableStep1((int)address, functions[0]);
            _utils.LogDebug($"TableStep1 address: {address.ToString("X8")}");

            address = _utils.SigScan(functions[1], "TableStep2");
            TableStep2((int)address, functions[1]);
            _utils.LogDebug($"TableStep2 address: {address.ToString("X8")}");

            vanillaTableUsage = _utils.SigScan_FindAll("40 A8 E0 04", "vanillaTable");
            foreach (long location in vanillaTableUsage)
            {
                _memory.SafeWrite(location, mapDataTable);
            }

            address = _utils.SigScan(functions[2], "TableEndComapre");
            TableEndComapre((int)address, functions[2]);
            _utils.LogDebug($"TableEndComapre address: {address.ToString("X8")}");
            

            address = _utils.SigScan(functions[3], "MinimapTileCheck_PreLoop");
            MinimapTileCheck_PreLoop((int)address, functions[3]);
            _utils.LogDebug($"MinimapTileCheck_PreLoop address: {address.ToString("X8")}");

            address = _utils.SigScan(functions[4], "MinimapTileCheck_GetTileAddress");
            MinimapTileCheck_GetTileAddress((int)address, functions[4]);
            _utils.LogDebug($"MinimapTileCheck_GetTileAddress address: {address.ToString("X8")}");

            address = _utils.SigScan(functions[5], "MinimapTileCheck_LoopCheck");
            MinimapTileCheck_LoopCheck((int)address, functions[5]);
            _utils.LogDebug($"MinimapTileCheck_LoopCheck address: {address.ToString("X8")}");


            // Changes here result in minimap screwups, like the blue square
            // or overlapping tiles. On the list to figure out why.
            // We know that there is a function call for all of these when getting some info, only thing that
            // comes to mind that could be causing an issue.
            address = _utils.SigScan(functions[6], "SetupMinimapPathLoad");
            SetupMinimapPathLoad((int)address, functions[6]);
            _utils.LogDebug($"SetupMinimapPathLoad address: {address.ToString("X8")}");
        }

        private void TableStep1(int functionAddress, string pattern)
        {
            // Code to replace:
            // 83 F8 0D 77 62 FF 24 85 40 B4 66 00

            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push ebx");
            instruction_list.Add(_commands[0]);
            instruction_list.Add($"mov eax, ebx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"jmp {_utils.AccountForBaseAddress(0x24A11F3)}");


            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }

        
        private void TableStep2(int functionAddress, string pattern)
        {
            // Code to replace:
            // 83 F8 0D 77 55 FF 24 85 B4 C2 76 00

            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push ebx");
            instruction_list.Add(_commands[0]);
            instruction_list.Add($"mov eax, ebx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"ret");


            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }

        private void TableEndComapre(int functionAddress, string pattern)
        {
            // Code to replace:
            // 83 C3 04 83 C7 04 81 FB B8 A8 E0 04 (83 C3 04 83 C7 04 81 FB ?? ?? ?? ??)

            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"add ebx, 04");
            instruction_list.Add($"add edi, 04");
            instruction_list.Add($"cmp ebx, {mapDataTable + (nuint)(4*_minimap_names.Count())}");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }

        private void MinimapTileCheck_PreLoop(int functionAddress, string pattern)
        {
            // Code to replace:
            // BF 48 0A 96 00 8D 5E 10
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"mov edi, 0");
            instruction_list.Add($"lea ebx, [esi+16]");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }
        private void MinimapTileCheck_GetTileAddress(int functionAddress, string pattern)
        {
            // Code to replace:
            // 8B 0E 8B 17 6A 00 8B 49 04 E8 9F C7 92 DA
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"mov ecx, [esi]");
            instruction_list.Add($"mov edx, edi");
            instruction_list.Add($"push 0");
            instruction_list.Add($"mov ecx, [ecx+4]");
            instruction_list.Add(_commands[1]);
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }
        private void MinimapTileCheck_LoopCheck(int functionAddress, string pattern)
        {
            // Code to replace:
            // 83 C7 04 89 03 83 C4 20 8D 5B 04 81 FF C0 0A 96 00
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"add edi, 1");
            instruction_list.Add($"mov [ebx], eax");
            instruction_list.Add($"add esp, 32");
            instruction_list.Add($"lea ebx, [ebx+4]");
            instruction_list.Add($"cmp edi, {_minimap_names.Count()}");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }
        private void SetupMinimapPathLoad(int functionAddress, string pattern)
        {

            // Code to replace:
            // 8A 43 04 3C 09 0F 82 AD 00 00 00

            List<string> instruction_list = new List<string>();
            int funcSize = (int)_utils.SigScan("8B 14 BD ?? ?? ?? ?? 8D 73 3C F3 0F 10 43 24", "minimap_func_end")-functionAddress;
            instruction_list.Add($"use32");
            instruction_list.Add($"push edi");
            instruction_list.Add($"push ebx");

            instruction_list.Add(_commands[2]);
            instruction_list.Add($"mov edi, edx");
            instruction_list.Add($"pop ebx");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, funcSize).Activate());

        }

        // Command 0
        private int GetTileMapIndexStart(int eax)
        {
            int ebx;
            String tileName = "smap" + eax.ToString().PadLeft(2, '0') + ".tmx";
            int index = _minimap_names.IndexOf(tileName);
            if (index == -1)
            {
                throw new MinimapStringOutOfRangeException(tileName, _utils);
            }
            _memory.SafeRead((mapDataTable + (nuint) (index * 4) ), out ebx);
            return ebx;
        }

        // Command 1
        public int GetMinimapImageAddress(int ecx, int edx)
        {
            int fileCount;
            byte[] byteArray;
            String fileName = "";
            int fileSize;
            _memory.SafeRead((nuint)ecx, out fileCount);
            if (fileCount != _minimap_names.Count() && _configuration.noteSizeDiscrepency && discrepencyNoted)
            {
                _utils.LogWarning($"Size discrepency between smap.bin and dungeon_minimap.json detected!");
                _utils.LogWarning($"smap.bin contains {fileCount} files, dungeon_minimap.json expects {_minimap_names.Count()} files");
                discrepencyNoted = true;
            }
            for (int i = 0; i < fileCount; i++)
            {
                ecx += 4;
                _memory.SafeReadRaw((nuint)ecx, out byteArray, 32);
                ecx += 32;
                fileName = System.Text.Encoding.UTF8.GetString(byteArray).Replace("\0", string.Empty);
                if (fileName.Equals(_minimap_names[edx]))
                {
                    //Found our filename
                    if (edx != i)
                    {
                        //At an unintended location
                        _utils.LogWarning($"Location of {fileName} in smap.bin differs from placement in dungeon_map.json");
                    }
                    ecx += 4;
                    return ecx;
                }
                else
                {
                    _memory.SafeRead((nuint)ecx, out fileSize);
                    ecx += fileSize;
                }
            }
            throw new MissingMinimapImageException(fileName, _utils);
        }

        // Command 2
        public int GetMinimapInfoAddress(int ebx)
        {
            /*
              OK, this isn't particularly relevant to much, but this has been a thorn in my side since I began fiddling with the minimap.
              When loading in certain textures (exit rooms, lead-in rooms for room 13 and room 14), I've always found some difficulties in
              the past with having them render properly. The reason for this, as I found out, is because the game never loads these textures
              AT ALL. Instead, it defaults to other textures from similar rooms (non-exit big rooms for the exits and lead-in for room 9 instead
              of 13 and 14's). This wouldn't be so annoying if these never-loaded textures WEREN'T ACTUAL TEXTURES IN SMAP.BIN WITH CORRESPONDING
              STRINGS WITHIN THE EXECUTABLE. So every time I have loaded in the 'proper' textures for these oddballs, I've been getting screwy results.

              I'm certain that I've lost some number of weeks in my lifespan from this.

              The workaround for this asinine affair is that I've replaced 
             */
            byte eax;
            byte ecx;
            int index1;
            String tileName;

            _memory.SafeRead((nuint)(ebx+4), out eax);
            _memory.SafeRead((nuint)(ebx+5), out ecx);
            if (eax < 1 || eax >_minimap.Count())
            {
                throw new MinimapInfoIdOutOfRangeException(eax, _utils);
            }
            if (_minimap[eax-1].multipleNames)
            {
                if (ecx == 0)
                {
                    tileName = _minimap[eax-1].name;
                }
                else
                { 
                    tileName = _minimap[eax-1].names[ecx-1];

                    //Just for debugging atm
                    if (eax == 13 && ecx != 2)
                    {
                        tileName = "smap09_01.tmx";
                    }

                }
            }
            else
            {
                tileName = _minimap[eax-1].name;
            }
            index1 = _minimap_names.IndexOf(tileName);
            if (index1 == -1)
            {
                throw new MinimapStringOutOfRangeException(tileName, _utils);
            }
            return index1;
        }


        [Function(Register.eax, Register.ebx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetTileMapIndexStartFunction(int eax);


        [Function(new[] { Register.ecx, Register.edx }, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetMinimapImageAddressFunction(int ecx, int edx);

        [Function(Register.ebx, Register.edx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetMinimapInfoAddressFunction(int ebx);
    }
}
