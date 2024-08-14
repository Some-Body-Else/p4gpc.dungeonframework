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
using static p4gpc.dungeonframework.Configuration.Config;
using static System.Formats.Asn1.AsnWriter;
using System.ComponentModel.Design;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.CompilerServices;

namespace p4gpc.dungeonframework.Accessors
{
    public class FieldComparesAccessor : Accessor
    {
        private List<FieldCompares> _fieldCompareTable;
        private nuint _fieldComparesAddress;
        private nuint _fieldComparesLookupAddress;
        private List<DungeonLinks> _linkList;
        private nuint _randomPregenLinkTable;
        private nuint _randomBattleLinkTable;
        private nuint _pregenBattleLinkTable;

        private byte mod;
        private byte scale;
        private AccessorRegister outReg;
        private AccessorRegister inReg;
        private AccessorRegister baseReg;

        public FieldComparesAccessor(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {
            _fieldCompareTable = jsonImporter.GetFieldCompare();
            _linkList = jsonImporter.GetLinks();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Field compare hooks established.", DebugLevels.AlertConnections);
        }

        protected override void Initialize()
        {
            List<Int64> functions;
            Int64 function_single;
            Int64 jumpLocation;
            Int32 jumpOffset;
            string search_string;
            byte prefixREX;
            byte idByte;
            byte rmByte;
            byte compValue;
            bool hasREX;
            nuint fieldCompSize = 0;
            nuint fieldCompLookupSize = 0;

            foreach (FieldCompares fieldCompares in _fieldCompareTable)
            {
                fieldCompLookupSize++;
                foreach (var room in fieldCompares.rooms)
                {
                    // 1 byte for room ID (lookup purposes)
                    // 1 byte for type of field/room,
                    // 1 byte for flags (may remove later)
                    fieldCompSize+=3;
                }
            }

            _fieldComparesAddress = _memory.Allocate((int)fieldCompSize);
            _utils.LogDebug($"Location of Field Compares Table: {_fieldComparesAddress.ToString("X8")}", Config.DebugLevels.TableLocations);
            _fieldComparesLookupAddress = _memory.Allocate((int)(fieldCompLookupSize*8));
            _utils.LogDebug($"Location of Field Compares Lookup Table: {_fieldComparesLookupAddress.ToString("X8")}", Config.DebugLevels.TableLocations);


            _randomPregenLinkTable = _memory.Allocate((int)_linkList.Count()*2);
            _utils.LogDebug($"Location of Random/Pregen Link Table: {_randomPregenLinkTable.ToString("X8")}", Config.DebugLevels.TableLocations);


            _randomBattleLinkTable = _memory.Allocate((int)_linkList.Count()*2);
            _utils.LogDebug($"Location of Random/Battle Link Table: {_randomBattleLinkTable.ToString("X8")}", Config.DebugLevels.TableLocations);


            _pregenBattleLinkTable = _memory.Allocate((int)_linkList.Count()*2);
            _utils.LogDebug($"Location of Pregen/Battle Link Table: {_pregenBattleLinkTable.ToString("X8")}", Config.DebugLevels.TableLocations);


            fieldCompSize = 0;
            fieldCompLookupSize = 0;
            foreach (FieldCompares fieldCompares in _fieldCompareTable)
            {
                _memory.SafeWrite(_fieldComparesLookupAddress+(fieldCompLookupSize*8), (UInt64)(_fieldComparesAddress + fieldCompSize));
                fieldCompLookupSize++;
                foreach (byte key in fieldCompares.rooms.Keys)
                {
                    // 1 byte for room ID (lookup purposes)
                    _memory.SafeWrite(_fieldComparesAddress+fieldCompSize, key);
                    fieldCompSize++;
                    // 1 byte for type of field/room
                    _memory.SafeWrite(_fieldComparesAddress+fieldCompSize, (byte)fieldCompares.rooms[key].LoadType);
                    fieldCompSize++;
                    // 1 byte for flags (may remove later)
                    _memory.SafeWrite(_fieldComparesAddress+fieldCompSize, fieldCompares.rooms[key].Flags);
                    fieldCompSize++;
                }

            }


            fieldCompSize = 0;
            fieldCompLookupSize = 0;
            foreach (DungeonLinks links in _linkList)
            {
                _memory.SafeWrite(_randomPregenLinkTable+(fieldCompLookupSize*2), (byte)links.RandomPregen[0]);
                _memory.SafeWrite(_randomPregenLinkTable+(fieldCompLookupSize*2+1), (byte)links.RandomPregen[1]);

                _memory.SafeWrite(_randomBattleLinkTable+(fieldCompLookupSize*2), (byte)links.RandomBattle[0]);
                _memory.SafeWrite(_randomBattleLinkTable+(fieldCompLookupSize*2+1), (byte)links.RandomBattle[1]);

                _memory.SafeWrite(_pregenBattleLinkTable+(fieldCompLookupSize*2), (byte)links.PregenBattle[0]);
                _memory.SafeWrite(_pregenBattleLinkTable+(fieldCompLookupSize*2+1), (byte)links.PregenBattle[1]);
                fieldCompLookupSize++;

            }

            search_string = "8D 43 D8 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 44 ?? ?? 66 83 ?? 13 77 ??";
            function_single = _utils.SigScan(search_string, "LEA [OOO-0x28] Opcode");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+28), out compValue);
            
            jumpLocation = function_single + compValue+29;

            _memory.SafeRead((nuint)(function_single+0), out idByte);

            _memory.SafeRead((nuint)(function_single+1), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceDungeonRandomCheckAdded(function_single, search_string, jumpLocation, false, 1);



            search_string = "66 83 ?? 28 ?? ?? 66 83 ?? 13 77 ??";
            function_single = _utils.SigScan(search_string, "SUB OOO,0x28 Opcode");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+11), out compValue);

            jumpLocation = function_single + compValue+12;

            _memory.SafeRead((nuint)(function_single+2), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceDungeonRandomCheckAdded(function_single, search_string, jumpLocation, false, 2);


            search_string = "45 89 ?? 66 83 ?? 13 0F 87 ?? ?? ?? ??";
            function_single = _utils.SigScan(search_string, "LEA OOO,[XXX-0x28] Spread-out Opcode");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+9), out jumpOffset);

            jumpLocation = function_single + jumpOffset+13;

            _memory.SafeRead((nuint)(function_single+5), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceDungeonRandomCheckAdded(function_single, search_string, jumpLocation, false, 3);


            search_string = "8D ?? D8 45 33 ?? 66 ?? F8 13 77 ??";
            function_single = _utils.SigScan(search_string, "LEA OOO,[XXX-0x28] Spread-out Opcode");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+11), out compValue);

            jumpLocation = function_single + compValue+12;

            _memory.SafeRead((nuint)(function_single+2), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceDungeonRandomCheckAdded(function_single, search_string, jumpLocation, false, 4);



            search_string = "66 83 ?? 28 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 66 83 ?? 13";
            function_single = _utils.SigScan(search_string, "CMP OOO,0x13 Opcode");
            // change to a jne instead of a ja to account for logic differences
            _memory.SafeWrite((nuint)(function_single+18), (byte)0x75);

            _memory.SafeRead((nuint)(function_single+2), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceDungeonRandomCheckAfter(function_single, search_string, false, true);


            search_string = "83 ?? 28 83 ?? 13 77 ??";
            function_single = _utils.SigScan(search_string, "SUB OOO,0x28 Opcode");
            _memory.SafeRead((nuint)(function_single+7), out compValue);

            jumpLocation = function_single + compValue+8;
            _memory.SafeRead((nuint)(function_single+1), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceDungeonRandomCheck(function_single, search_string, false, jumpLocation, false);


            search_string = "8D ?? D8 66 ?? ?? 13 77 ??";
            functions = _utils.SigScan_FindAll(search_string, "FieldCompare LEA OOO,[XXX-0x28] Short Jump Opcodes");
            
            foreach (long function in functions)
            {
                // Figure out where we are going
                _memory.SafeRead((nuint)(function+8), out compValue);
                // Add 9 because we are going off the end of the jump instruction, not our search's base
                jumpLocation = function + compValue+9;

                hasREX = false;
                _memory.SafeRead((nuint)(function-1), out prefixREX);
                _memory.SafeRead((nuint)(function+0), out idByte);

                _memory.SafeRead((nuint)(function+1), out rmByte);

                outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
                baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
                if (!(prefixREX >0x4F || prefixREX < 0x40))
                {
                    hasREX = true;
                    // change this around
                    baseReg += (prefixREX & 0x1) << 3;
                    outReg += (prefixREX & 0x4) << 1;
                }
                _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                ReplaceDungeonRandomCheck(function, search_string, hasREX, jumpLocation, false);
            }

            search_string = "41 8D ?? D8 66 ?? ?? 13 0F 87 ?? ?? ?? ??";
            function_single = _utils.SigScan(search_string, "FieldCompare LEA OOO,[XXX-0x28] Near Jump Opcode");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+10), out jumpOffset);
            
            jumpLocation = function_single + jumpOffset+14;

            _memory.SafeRead((nuint)(function_single), out prefixREX);
            _memory.SafeRead((nuint)(function_single+1), out idByte);

            _memory.SafeRead((nuint)(function_single+2), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            outReg += (prefixREX & 0x4) << 1;
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
            baseReg += (prefixREX & 0x1) << 3;

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceDungeonRandomCheck(function_single, search_string, false, jumpLocation, false);

            search_string = "66 ?? ?? 28 66 83 ?? 13";
            functions = _utils.SigScan_FindAll(search_string, "FieldCompare SUB OOO,0x28 Opcodes");
            foreach (long function in functions)
            {
                // Resetting search string to maintain length of replaced code
                search_string = "66 ?? ?? 28 66 83  ?? 13";
                _memory.SafeRead((nuint)(function+0), out idByte);

                _memory.SafeRead((nuint)(function+2), out rmByte);

                outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
                baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

                _memory.SafeRead((nuint)(function+8), out idByte);
                if (idByte == 0x0F)
                {
                    // Either doing a neear jump or setting a byte, based on next byte's value
                    _memory.SafeRead((nuint)(function+9), out compValue);
                    if (compValue == 0x86 || compValue == 0x87)
                    {
                        // Padding string to ensure all code is replaced
                        search_string += " 0F 8? ?? ?? ?? ??";
                        _memory.SafeRead((nuint)(function+10), out jumpOffset);
                        jumpLocation = function + 14 + jumpOffset;
                        if (compValue == 0x86)
                        {
                            _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                            ReplaceDungeonRandomCheck(function, search_string, false, jumpLocation, true);
                        }
                        else
                        {
                            _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                            ReplaceDungeonRandomCheck(function, search_string, false, jumpLocation, false);
                        }
                    }
                    else
                    {

                        // Padding string to ensure all code is replaced
                        search_string += " 0F 96 ??";
                        jumpLocation = 0;
                        _memory.SafeRead((nuint)(function+10), out compValue);
                        inReg = (AccessorRegister)(compValue & 0x7);
                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        ReplaceDungeonRandomCheck(function, search_string, false,  jumpLocation, false);
                    }

                }
                else
                {
                    // Padding string to ensure all code is replaced
                    search_string += " 7? ??";
                    _memory.SafeRead((nuint)(function+9), out compValue);
                    jumpLocation = function + 10 + compValue;
                    // some form of near jump
                    if (idByte == 0x76)
                    {
                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        ReplaceDungeonRandomCheck(function, search_string, false, jumpLocation, true);
                    }
                    else
                    {
                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        ReplaceDungeonRandomCheck(function, search_string, false, jumpLocation, false);
                    }
                }
            }

            // Checks for pregenerated dungeon floors
            search_string = "66 83 ?? 3C 66 83 ?? 13";
            functions = _utils.SigScan_FindAll(search_string, "FieldCompare SUB OOO,0x3C Opcodes");
            foreach (long function in functions)
            {
                // Resetting search string to maintain length of replaced code
                search_string = "66 83 ?? 3C 66 83 ?? 13";
                _memory.SafeRead((nuint)(function+0), out idByte);

                _memory.SafeRead((nuint)(function+2), out rmByte);

                outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
                baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

                // Figure out what we are doing
                _memory.SafeRead((nuint)(function+8), out idByte);
                if (idByte == 0x0F)
                {
                    // Either doing a neear jump or setting a byte, based on next byte's value
                    _memory.SafeRead((nuint)(function+9), out compValue);
                    if (compValue == 0x86 || compValue == 0x87)
                    {
                        // Padding string to ensure all code is replaced
                        search_string += " 0F 8? ?? ?? ?? ??";
                        _memory.SafeRead((nuint)(function+10), out jumpOffset);
                        jumpLocation = function + 14 + jumpOffset;
                        if (compValue == 0x86)
                        {
                            _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                            ReplaceDungeonPregenCheck(function, search_string, jumpLocation, true);
                        }
                        else
                        {
                            _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                            ReplaceDungeonPregenCheck(function, search_string, jumpLocation, false);
                        }
                    }
                    else
                    {

                        // Padding string to ensure all code is replaced
                        search_string += " 0F 96 ??";
                        jumpLocation = 0;
                        _memory.SafeRead((nuint)(function+10), out compValue);
                        inReg = (AccessorRegister)(compValue & 0x7);
                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        ReplaceDungeonPregenCheck(function, search_string, jumpLocation, false);
                    }

                }
                else
                {
                    // Padding string to ensure all code is replaced
                    search_string += " 7? ??";
                    _memory.SafeRead((nuint)(function+9), out compValue);
                    jumpLocation = function + 10 + compValue;
                    // some form of near jump
                    if (idByte == 0x76)
                    {
                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        ReplaceDungeonPregenCheck(function, search_string, jumpLocation, true);
                    }
                    else
                    {
                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        ReplaceDungeonPregenCheck(function, search_string, jumpLocation, false);
                    }
                }
            }

            // Checks involving REX prefixes
            search_string = "66 ?? 83 ?? 3C 66 ?? 83 ?? 13";
            functions = _utils.SigScan_FindAll(search_string, "FieldCompare SUB OOO,0x3C Extended Opcodes");
            foreach (long function in functions)
            {
                // Resetting search string to maintain length of replaced code
                search_string = "66 ?? 83 ?? 3C 66 ?? 83 ?? 13";
                _memory.SafeRead((nuint)(function+0), out idByte);
                _memory.SafeRead((nuint)(function+1), out prefixREX);

                _memory.SafeRead((nuint)(function+3), out rmByte);


                outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
                outReg += (prefixREX & 0x4) << 1;
                baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
                baseReg += (prefixREX & 0x1) << 3;

                // Figure out what we are doing
                _memory.SafeRead((nuint)(function+10), out idByte);
                if (idByte == 0x0F)
                {
                    // Either doing a neear jump or setting a byte, based on next byte's value
                    _memory.SafeRead((nuint)(function+11), out compValue);
                    if (compValue == 0x86 || compValue == 0x87)
                    {
                        // Padding string to ensure all code is replaced
                        search_string += " 0F 8? ?? ?? ?? ??";
                        _memory.SafeRead((nuint)(function+12), out jumpOffset);
                        jumpLocation = function + 16 + jumpOffset;
                        if (compValue == 0x86)
                        {
                            _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                            ReplaceDungeonPregenCheck(function, search_string, jumpLocation, true);
                        }
                        else
                        {
                            _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                            ReplaceDungeonPregenCheck(function, search_string, jumpLocation, false);
                        }
                    }
                    else
                    {

                        // Padding string to ensure all code is replaced
                        search_string += " 0F 96 ??";
                        jumpLocation = 0;
                        _memory.SafeRead((nuint)(function+12), out compValue);
                        inReg = (AccessorRegister)(compValue & 0x7);
                        _utils.LogDebug($"Location(?) of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        ReplaceDungeonPregenCheck(function, search_string, jumpLocation, false);
                    }

                }
                else
                {
                    // Padding string to ensure all code is replaced
                    search_string += " 7? ??";
                    _memory.SafeRead((nuint)(function+11), out compValue);
                    jumpLocation = function + 12 + compValue;
                    // some form of near jump
                    if (idByte == 0x76)
                    {
                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        ReplaceDungeonPregenCheck(function, search_string, jumpLocation, true);
                    }
                    else
                    {
                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        ReplaceDungeonPregenCheck(function, search_string, jumpLocation, false);
                    }
                }
            }


            search_string = "8D ?? C4 66 ?? ?? 13 77 ??";
            functions = _utils.SigScan_FindAll(search_string, "FieldCompare LEA OOO,[XXX-0x3C] Opcodes");

            foreach (long function in functions)
            {
                // Figure out where we are going
                _memory.SafeRead((nuint)(function+8), out compValue);
                // Add 9 because we are going off the end of the jump instruction, not our search's base
                jumpLocation = function + compValue+9;

                hasREX = false;
                _memory.SafeRead((nuint)(function-1), out prefixREX);
                _memory.SafeRead((nuint)(function+0), out idByte);

                _memory.SafeRead((nuint)(function+1), out rmByte);

                outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
                baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
                if (!(prefixREX >0x4F || prefixREX < 0x40))
                {
                    hasREX = true;
                    // change this around
                    baseReg += (prefixREX & 0x1) << 3;
                    outReg += (prefixREX & 0x4) << 1;
                }
                _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                ReplaceDungeonPregenCheck(function, search_string, jumpLocation, false);
            }

            search_string = "8D ?? C4 66 ?? ?? 09 77 ??";
            function_single = _utils.SigScan(search_string, "FieldCompare LEA OOO,[XXX-0x3C] Opcodes");

            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+8), out compValue);
            // Add 9 because we are going off the end of the jump instruction, not our search's base
            jumpLocation = function_single + compValue+9;
            _memory.SafeRead((nuint)(function_single+0), out idByte);
            _memory.SafeRead((nuint)(function_single+1), out rmByte);

            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceDungeonPregenCheckEx(function_single, search_string, jumpLocation, false);




             search_string = "83 ?? 33 83 ?? 1B 76 ??";
            function_single = _utils.SigScan(search_string, "FieldCompare SUB OOO, 0x33");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+7), out compValue);

            jumpLocation = function_single + compValue+8;

            _memory.SafeRead((nuint)(function_single+1), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // ReplaceDungeonPregenCheck(function_single, search_string, jumpLocation, true);

            search_string = "83 ?? 33 83 ?? 1B 0F ?? ?? ?? ?? ??";
            function_single = _utils.SigScan(search_string, "FieldCompare SUB OOO, 0x33");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+8), out jumpOffset);

            jumpLocation = function_single + jumpOffset+12;

            _memory.SafeRead((nuint)(function_single+1), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceDungeonPregenCheck(function_single, search_string, jumpLocation, false);





            search_string = "41 81 F8 C8 00 00 00 7D 0B";
            function_single = _utils.SigScan(search_string, "FieldCompare CMP OOO, 0x000000C8");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+8), out compValue);
            
            jumpLocation = function_single + compValue+9;

            _memory.SafeRead((nuint)(function_single), out prefixREX);
            _memory.SafeRead((nuint)(function_single+1), out idByte);

            _memory.SafeRead((nuint)(function_single+2), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            outReg += (prefixREX & 0x4) << 1;
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
            baseReg += (prefixREX & 0x1) << 3;

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceBattleFieldCheck(function_single, search_string, jumpLocation, true, false);

            search_string = "3D C8 00 00 00 0F 8D ?? ?? ?? ??";
            function_single = _utils.SigScan(search_string, "FieldCompare CMP OOO, 0x000000C8");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+8), out jumpOffset);

            jumpLocation = function_single + jumpOffset+11;

            _memory.SafeRead((nuint)(function_single+1), out idByte);
            // Just putting in some dummy register here.
            outReg = AccessorRegister.rdi;
            // 3D uses EAX by default, just bumping up the size out of laziness
            baseReg = AccessorRegister.rax;

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceBattleFieldCheck(function_single, search_string, jumpLocation, true, false);


            search_string = "3D C8 00 00 00 0F 8C ?? ?? ?? ??";
            function_single = _utils.SigScan(search_string, "FieldCompare CMP OOO, 0x000000C8");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+7), out jumpOffset);

            jumpLocation = function_single + jumpOffset+11;

            // Just putting in some dummy register here.
            outReg = AccessorRegister.rdi;
            // 3D uses EAX by default, just bumping up the size out of laziness
            baseReg = AccessorRegister.rax;

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceBattleFieldCheck(function_single, search_string, jumpLocation, false, false);


            search_string = "81 39 C8 00 00 00 7D 16";
            function_single = _utils.SigScan(search_string, "FieldCompare CMP OOO, 0x000000C8");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+7), out compValue);
            
            jumpLocation = function_single + compValue+8;

            _memory.SafeRead((nuint)(function_single), out idByte);

            _memory.SafeRead((nuint)(function_single+1), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceBattleFieldCheck(function_single, search_string, jumpLocation, true, false);

            search_string = "81 39 C8 00 00 00 48 89 81 B8 01 00 00 7C 1E";
            function_single = _utils.SigScan(search_string, "FieldCompare CMP OOO, 0x000000C8");
            // Figure out where we are going
            _memory.SafeRead((nuint)(function_single+15), out compValue);
            
            jumpLocation = function_single + compValue+16;

            _memory.SafeRead((nuint)(function_single), out idByte);

            _memory.SafeRead((nuint)(function_single+1), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM

            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // HasREX = false due to search already accounting for the prefix natively
            ReplaceBattleFieldCheck(function_single, search_string, jumpLocation, false, true);



            // This jump appears to be always taken in the vanilla game, but it has an ID dependency that
            // needs to be routed out
            search_string = "44 8B 00 41 8D ?? CD 83 ?? 07 77 ??";
            function_single = _utils.SigScan(search_string, "FieldCompare SUB OOO, 0x33");
            _memory.SafeRead((nuint)(function_single+11), out compValue);
            jumpLocation = function_single + compValue+12;
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceWithJump(function_single, search_string, jumpLocation);

            // Waiting to get a better understanding of flags
            search_string = "83 ?? FA 83 ?? 19 0F ?? ?? ?? ?? ??";
            function_single = _utils.SigScan(search_string, "FieldCompare ADD OOO, -6");
            _memory.SafeRead((nuint)(function_single+1), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
            _memory.SafeRead((nuint)(function_single+11), out jumpOffset);
            jumpLocation = function_single + jumpOffset+12;
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            // FIGUREOUTFUNCTIONNAMEHERE()

            // TODO: Write the actual code replacements in, want to cover all the non-flag
            // stuff I find first.

            // Note by flag, I'm referring to something that is not an intrinsic part of 
            // how the field works. Being a randomly-generated floor could be described
            // as a flag, but there are flags set for some random floors and not others,
            // while the randomly-generated nature of the floor is constant for all of them

            // Replacing some flag checks
            search_string = "83 ?? D7 83 ?? 18 77 ?? ?? 13 00 30 01 0F A3 ??";
            functions = _utils.SigScan_FindCount(search_string, "FieldCompare _____ Opcodes", 4);

            foreach (long function in functions)
            {
                // Check value in outReg to see if the flags have a value set somewhere
                // (need to decide where at a later point), just run the compare and let
                // program return to regular running point

                _memory.SafeRead((nuint)(function+1), out rmByte);

                outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
                baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
                if (!(prefixREX >0x4F || prefixREX < 0x40))
                {
                    hasREX = true;
                    // change this around
                    baseReg += (prefixREX & 0x1) << 3;
                    outReg += (prefixREX & 0x4) << 1;
                }
                _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                // ReplaceDungeonPregenCheck(function, search_string, jumpLocation, false);
            }

            // Versions with Prefix
            search_string = "83 ?? D7 83 ?? 18 77 ?? 41 ?? 13 00 30 01 41 0F A3 ??";
            functions = _utils.SigScan_FindCount(search_string, "FieldCompare _____ Opcodes", 4);

            foreach (long function in functions)
            {
                // Check value in outReg to see if the flags have a value set somewhere
                // (need to decide where at a later point), just run the compare and let
                // program return to regular running point

                _memory.SafeRead((nuint)(function+1), out rmByte);

                outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
                baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
                if (!(prefixREX >0x4F || prefixREX < 0x40))
                {
                    hasREX = true;
                    // change this around
                    baseReg += (prefixREX & 0x1) << 3;
                    outReg += (prefixREX & 0x4) << 1;
                }
                _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                // ReplaceDungeonPregenCheck(function, search_string, jumpLocation, false);
            }

            search_string = "83 ?? 17 75 ?? 8B ?? 04 FF C8 83 ?? 02 EB 38 83 ?? 18 74 ?? 83 ?? 19 74 ?? 83 ?? 1A 74 ?? 83 ?? 1B 74 ?? 83 ?? 1C 74 ?? 83 ?? 1D 75 08 83 78 04 02 75 ?? EB 14 83 ?? 1E 74 ?? 83 ?? 1F 75 ?? 8B ?? 04 FF C8 83 ?? 01 77 ??";
            function_single = _utils.SigScan(search_string, "FieldCompare DUNGEON_FIELD_CHECK_A");
            _memory.SafeRead((nuint)(function_single+1), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
            _memory.SafeRead((nuint)(function_single+72), out compValue);
            jumpLocation = function_single + compValue+73;
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceDungeonFieldCheckA(function_single, search_string, jumpLocation, false);

            // Determines if using models w/ weapons or not
            search_string = "33 ?? 66 83 ?? 14 75 ??";
            function_single = _utils.SigScan(search_string, "FieldCompare ReplaceDungeonModelCheck");
            _memory.SafeRead((nuint)(function_single+4), out rmByte);
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
            outReg = AccessorRegister.rbx;
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceDungeonModelCheck(function_single, search_string);


            // Gets random-gen tileset for pregen floors
            search_string = "66 83 ?? 3C 66 83 ?? 09 77 ?? 66 83 ?? EC 31 ?? EB ?? 0F B7 4C ?? 38";
            function_single = _utils.SigScan(search_string, "PregenRandomLookup");
            baseReg = AccessorRegister.rax;
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplacePregenRandomLookup(function_single, search_string);

            // 41 8D ?? EC 66 83 ?? 13 0F 86 ?? ?? ?? ??
            search_string = "41 8D ?? EC 66 83 ?? 13 0F 86 ?? ?? ?? ??";
            function_single = _utils.SigScan(search_string, "FieldCompare DungeonStaticCheck");
            _memory.SafeRead((nuint)(function_single+10), out jumpOffset);
            jumpLocation = function_single + jumpOffset+14;
            _memory.SafeRead((nuint)(function_single+2), out rmByte);
            outReg = AccessorRegister.rax;          // Reg_Out   modrm.REG
            baseReg = AccessorRegister.r8;          // Reg_In    modrm.RM
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceDungeonStaticCheck(function_single, search_string, jumpLocation, true);

            search_string = "66 83 ?? 32 8D 41 ?? 66 0F ?? C8 B8 C8 00 00 00 66 03 C8";
            function_single = _utils.SigScan(search_string, "DungeonBattleLookup");
            baseReg = AccessorRegister.rcx;
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceDungeonBattleLookup(function_single, search_string);

            // 83 ?? FA 83 ?? 19 0F 87 68 ?? ?? ??
            search_string = "83 ?? FA 83 ?? 19 0F 87 68 ?? ?? ??";
            function_single = _utils.SigScan(search_string, "DungeonBattleLookup");
            baseReg = AccessorRegister.rax;
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceFieldCameraPanCheck(function_single, search_string);

            // 66 83 ?? 13 0F 87 2E 01 00 00
            search_string = "66 83 ?? 13 0F 87 2E 01 00 00";
            function_single = _utils.SigScan(search_string, "FieldCompare Misc. Random Dungeon Check");
            _memory.SafeRead((nuint)(function_single+2), out rmByte);
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);    // Reg_Out   modrm.REG
            baseReg = (AccessorRegister)(rmByte & 0x7);          // Reg_In    modrm.RM
            _memory.SafeRead((nuint)(function_single+6), out jumpOffset);
            jumpLocation = function_single + jumpOffset+10;
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceDungeonRandomCheck(function_single, search_string, false, jumpLocation, false);

            // 66 8B ?? ?? ?? ?? ?? 66 81 ?? ?? ?? 66 29 ?? 66 44 8B ?? ?? ?? ?? ?? 66 41 81 ?? ?? ?? 66 44 39 ??
            search_string = "66 8B ?? ?? ?? ?? ?? 66 81 ?? ?? ?? 66 29 ?? 66 44 8B ?? ?? ?? ?? ?? 66 41 81 ?? ?? ?? 66 44 39 ??";
            function_single = _utils.SigScan(search_string, "CameraCollisionLoadCheck");
            baseReg = AccessorRegister.rbx;
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceCameraCollisionLoadCheck(function_single, search_string);

        }

        private void ReplaceWithJump(Int64 functionAddress, string pattern, Int64 jumpLocation)
        {

            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");
            instruction_list.Add($"push rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, {jumpLocation}");
            instruction_list.Add($"mov [rsp+8], rax");
            instruction_list.Add($"pop rax");
            instruction_list.Add($"ret");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        /*
         Every regular field check devolves into extended flag checks that I'll handle on a little further
         down the line.
         */
        private void ReplaceRegularFieldCheck(Int64 functionAddress, string pattern, Int64 jumpLocation, bool jumpIfTrue, int? otherOp = null)
        {
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

            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            instruction_list.Add($"cmp {baseReg}, 0");
            instruction_list.Add($"je CHECK_FAIL");
            instruction_list.Add($"cmp {baseReg}, 0xFFFF");
            instruction_list.Add($"je CHECK_FAIL");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.OVERWORLD, functionAddress);
            instruction_list.Add($"je CHECK_SUCCESS");
            instruction_list.Add($"jmp CHECK_FAIL");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            else
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            // All of the replaced opcodes jump when it is NOT a random dungeon field, so
            // failing forces a return to a different address
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            // Prepare to jump to new location
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"mov {usedRegs[0]}, {jumpLocation}");
            instruction_list.Add($"mov [rsp+8], {usedRegs[0]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"ret");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            else
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            // Ideally not needed for a future version of this, but I've only found one
            // regular field check, and the subtraction does matter
            instruction_list.Add($"sub {baseReg}, 6");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        private void ReplaceDungeonRandomCheckAdded(Int64 functionAddress, string pattern, Int64 jumpLocation, bool jumpIfTrue, int? otherOp = null)
        {
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


            if (otherOp != null)
            {
                switch (otherOp)
                {
                    case 1:
                        {
                            instruction_list.Add($"xor ebp, ebp");
                            instruction_list.Add($"mov r15d, 0xFFFFFFFF");
                            instruction_list.Add($"mov [rsp+0xB0], ebp");
                            instruction_list.Add($"mov edi, ebp");
                            instruction_list.Add($"mov r12d, ebp");
                            break;
                        }
                    case 2:
                        {
                            instruction_list.Add($"xor edi, edi");
                            break;
                        }
                    case 3:
                        {
                            instruction_list.Add($"mov [r15], r14d");
                            instruction_list.Add($"add {baseReg}, 0x28");
                            break;
                        }
                    case 4:
                        {
                            instruction_list.Add($"xor r14d, r14d");
                            break;
                        }
                    default:
                        {
                            break;
                        }

                }
            }

            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            instruction_list.Add($"cmp {baseReg}, 0");
            instruction_list.Add($"je CHECK_FAIL");
            instruction_list.Add($"cmp {baseReg}, 0xFFFF");
            instruction_list.Add($"je CHECK_FAIL");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_RANDOM, functionAddress);
            instruction_list.Add($"je CHECK_SUCCESS");
            instruction_list.Add($"jmp CHECK_FAIL");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            else
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            // Prepare to jump to new location
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"mov {usedRegs[0]}, {jumpLocation}");
            instruction_list.Add($"mov [rsp+8], {usedRegs[0]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"ret");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            else
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }
        private void ReplaceDungeonRandomCheckAfter(Int64 functionAddress, string pattern, bool jumpIfTrue, bool readjustValue)
        {
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

            // Is being hitched onto a subtraction call, so we need to add 40 to scale is back up
            if (readjustValue)
            {
                instruction_list.Add($"add {baseReg}, 0x28");
                // Need an AND to account for overflow
                instruction_list.Add($"and {baseReg}, 0xFF");
            }


            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_RANDOM, functionAddress);

            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.ExecuteAfter, _utils.GetPatternLength(pattern)).Activate());
        }
        private void ReplaceDungeonRandomCheck(Int64 functionAddress, string pattern, bool HasREX, Int64 jumpLocation, bool jumpIfTrue)
        {
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


            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            instruction_list.Add($"cmp {baseReg}, 0");
            instruction_list.Add($"je CHECK_FAIL");
            instruction_list.Add($"cmp {baseReg}, 0xFFFF");
            instruction_list.Add($"je CHECK_FAIL");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_RANDOM, functionAddress);
            instruction_list.Add($"je CHECK_SUCCESS");
            instruction_list.Add($"jmp CHECK_FAIL");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            else
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            // All of the replaced opcodes jump when it is NOT a random dungeon field, so
            // failing forces a return to a different address
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            // Prepare to jump to new location
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"mov {usedRegs[0]}, {jumpLocation}");
            instruction_list.Add($"mov [rsp+8], {usedRegs[0]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"ret");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            else
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");

            if (HasREX)
            {
                _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress-1, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)+1).Activate());
            }
            else
            {
                _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
            }
        }
        private void ReplaceDungeonPregenCheck(Int64 functionAddress, string pattern, Int64 jumpLocation, bool jumpIfTrue)
        {
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


            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");


            instruction_list.Add($"cmp {baseReg}, 0");
            instruction_list.Add($"je CHECK_FAIL");
            instruction_list.Add($"cmp {baseReg}, 0xFFFF");
            instruction_list.Add($"je CHECK_FAIL");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_PREGEN, functionAddress);
            instruction_list.Add($"je CHECK_SUCCESS");
            instruction_list.Add($"jmp CHECK_FAIL");

            if (jumpLocation != 0)
            {
                if (jumpIfTrue)
                {
                    instruction_list.Add($"label CHECK_SUCCESS");
                }
                else
                {
                    instruction_list.Add($"label CHECK_FAIL");
                }
                // All of the replaced opcodes jump when it is NOT a random dungeon field, so
                // failing forces a return to a different address
                instruction_list.Add($"pop {usedRegs[3]}");
                instruction_list.Add($"pop {usedRegs[2]}");
                instruction_list.Add($"pop {usedRegs[1]}");
                instruction_list.Add($"pop {usedRegs[0]}");
                // Prepare to jump to new location
                instruction_list.Add($"push {usedRegs[0]}");
                instruction_list.Add($"push {usedRegs[0]}");
                instruction_list.Add($"mov {usedRegs[0]}, {jumpLocation}");
                instruction_list.Add($"mov [rsp+8], {usedRegs[0]}");
                instruction_list.Add($"pop {usedRegs[0]}");
                instruction_list.Add($"ret");

                if (jumpIfTrue)
                {
                    instruction_list.Add($"label CHECK_FAIL");
                }
                else
                {
                    instruction_list.Add($"label CHECK_SUCCESS");
                }
                instruction_list.Add($"pop {usedRegs[3]}");
                instruction_list.Add($"pop {usedRegs[2]}");
                instruction_list.Add($"pop {usedRegs[1]}");
                instruction_list.Add($"pop {usedRegs[0]}");

            }
            else
            {
                instruction_list.Add($"label CHECK_SUCCESS");
                instruction_list.Add($"pop {usedRegs[3]}");
                instruction_list.Add($"pop {usedRegs[2]}");
                instruction_list.Add($"pop {usedRegs[1]}");
                instruction_list.Add($"pop {usedRegs[0]}");
                instruction_list.Add($"mov {inReg}, 1");
                instruction_list.Add($"jmp RETURN");

                instruction_list.Add($"label CHECK_FAIL");
                instruction_list.Add($"pop {usedRegs[3]}");
                instruction_list.Add($"pop {usedRegs[2]}");
                instruction_list.Add($"pop {usedRegs[1]}");
                instruction_list.Add($"pop {usedRegs[0]}");
                instruction_list.Add($"mov {inReg}, 0");

                instruction_list.Add($"label RETURN");
            }

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        // Single instruction is giving me hassle due to it accessing data in one field other than itself.
        // This occurs on pregen floors, where the field ID uses the dungeon tiles of the entry 20 IDs below itself
        // to be constructed. Need to figure out where and how it decides this
        private void ReplaceDungeonPregenCheckEx(Int64 functionAddress, string pattern, Int64 jumpLocation, bool jumpIfTrue)
        {
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

            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");


            // Sometimes hits points when data isn't loaded in
            instruction_list.Add($"cmp {baseReg}, 0");
            instruction_list.Add($"je CHECK_FAIL");
            instruction_list.Add($"cmp {baseReg}, 0xFFFF");
            instruction_list.Add($"je CHECK_FAIL");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_PREGEN, functionAddress, checkBaseAgainstRam:true);
            instruction_list.Add($"je CHECK_SUCCESS");
            instruction_list.Add($"jmp CHECK_FAIL");

            if (jumpLocation != 0)
            {
                if (jumpIfTrue)
                {
                    instruction_list.Add($"label CHECK_SUCCESS");
                }
                else
                {
                    instruction_list.Add($"label CHECK_FAIL");
                }
                // All of the replaced opcodes jump when it is NOT a random dungeon field, so
                // failing forces a return to a different address
                instruction_list.Add($"pop {usedRegs[3]}");
                instruction_list.Add($"pop {usedRegs[2]}");
                instruction_list.Add($"pop {usedRegs[1]}");
                instruction_list.Add($"pop {usedRegs[0]}");
                // Prepare to jump to new location
                instruction_list.Add($"push {usedRegs[0]}");
                instruction_list.Add($"push {usedRegs[0]}");
                instruction_list.Add($"mov {usedRegs[0]}, {jumpLocation}");
                instruction_list.Add($"mov [rsp+8], {usedRegs[0]}");
                instruction_list.Add($"pop {usedRegs[0]}");
                instruction_list.Add($"ret");

                if (jumpIfTrue)
                {
                    instruction_list.Add($"label CHECK_FAIL");
                }
                else
                {
                    instruction_list.Add($"label CHECK_SUCCESS");
                }
                instruction_list.Add($"pop {usedRegs[3]}");
                instruction_list.Add($"pop {usedRegs[2]}");
                instruction_list.Add($"pop {usedRegs[1]}");
                instruction_list.Add($"pop {usedRegs[0]}");

            }
            else
            {
                instruction_list.Add($"label CHECK_SUCCESS");
                instruction_list.Add($"pop {usedRegs[3]}");
                instruction_list.Add($"pop {usedRegs[2]}");
                instruction_list.Add($"pop {usedRegs[1]}");
                instruction_list.Add($"pop {usedRegs[0]}");
                instruction_list.Add($"mov {inReg}, 1");
                instruction_list.Add($"jmp RETURN");

                instruction_list.Add($"label CHECK_FAIL");
                instruction_list.Add($"pop {usedRegs[3]}");
                instruction_list.Add($"pop {usedRegs[2]}");
                instruction_list.Add($"pop {usedRegs[1]}");
                instruction_list.Add($"pop {usedRegs[0]}");
                instruction_list.Add($"mov {inReg}, 0");

                instruction_list.Add($"label RETURN");
            }

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        private void ReplaceBattleFieldCheck(Int64 functionAddress, string pattern, Int64 jumpLocation, bool jumpIfTrue, bool HasMiddleInstruction)
        {
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

            if (HasMiddleInstruction)
            {
                // Just account for that here
                instruction_list.Add($"add rcx, 440");
                instruction_list.Add($"mov [rcx], rax");
                instruction_list.Add($"sub rcx, 440");
            }


            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            instruction_list.Add($"cmp {baseReg}, 0");
            instruction_list.Add($"je CHECK_FAIL");
            instruction_list.Add($"cmp {baseReg}, 0xFFFF");
            instruction_list.Add($"je CHECK_FAIL");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.BATTLE, functionAddress, pullFieldFromMemory: true);
            instruction_list.Add($"je CHECK_SUCCESS");
            instruction_list.Add($"jmp CHECK_FAIL");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            else
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            // All of the replaced opcodes jump when it is NOT a random dungeon field, so
            // failing forces a return to a different address
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            // Prepare to jump to new location
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"mov {usedRegs[0]}, {jumpLocation}");
            instruction_list.Add($"mov [rsp+8], {usedRegs[0]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"ret");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            else
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }
        private void ReplaceDungeonStaticCheck(Int64 functionAddress, string pattern, Int64 jumpLocation, bool jumpIfTrue)
        {
            /*
             The first instance of this that I found appears to have some lighting properties, keep in mind for later
             */
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



            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_STATIC, functionAddress);
            instruction_list.Add($"je CHECK_SUCCESS");
            instruction_list.Add($"jmp CHECK_FAIL");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            else
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            // Prepare to jump to new location
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"mov {usedRegs[0]}, {jumpLocation}");
            instruction_list.Add($"mov [rsp+8], {usedRegs[0]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"ret");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            else
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");



            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        private void ReplaceDungeonFieldCheckA(Int64 functionAddress, string pattern, Int64 jumpLocation, bool jumpIfTrue)
        {
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



            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");


            // Sometimes hits points when data isn't loaded in
            instruction_list.Add($"cmp {baseReg}, 0");
            instruction_list.Add($"je CHECK_FAIL");
            instruction_list.Add($"cmp {baseReg}, 0xFFFF");
            instruction_list.Add($"je CHECK_FAIL");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_RANDOM, functionAddress);
            instruction_list.Add($"jne CHECK_FAIL");


            // Check the bitflag
            instruction_list.Add($"add {usedRegs[1]}, 1");
            instruction_list.Add($"mov {usedRegs[3]}, [{usedRegs[1]}]");
            instruction_list.Add($"and {usedRegs[3]}, 0x1");
            instruction_list.Add($"cmp {usedRegs[3]}, 1");
            instruction_list.Add($"je CHECK_SUCCESS");
            instruction_list.Add($"jmp CHECK_FAIL");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            else
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            // All of the replaced opcodes jump when it is NOT a random dungeon field, so
            // failing forces a return to a different address
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            // Prepare to jump to new location
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"mov {usedRegs[0]}, {jumpLocation}");
            instruction_list.Add($"mov [rsp+8], {usedRegs[0]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"ret");

            if (jumpIfTrue)
            {
                instruction_list.Add($"label CHECK_FAIL");
            }
            else
            {
                instruction_list.Add($"label CHECK_SUCCESS");
            }
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        private void ReplaceDungeonModelCheck(Int64 functionAddress, string pattern)
        {
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

            instruction_list.Add($"xor rax, rax");

            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_RANDOM, functionAddress, roomIdFromRegAddr:true);
            instruction_list.Add($"je CHECK_SUCCESS");
            instruction_list.Add($"cmp {usedRegs[3]}, {(int)RoomLoadType.DUNGEON_PREGEN}");
            instruction_list.Add($"je CHECK_SUCCESS");
            instruction_list.Add($"cmp {usedRegs[3]}, {(int)RoomLoadType.DUNGEON_STATIC}");
            
            
            instruction_list.Add($"jne CHECK_FAIL");

            // Check the bitflag
            instruction_list.Add($"add {usedRegs[1]}, 1");
            instruction_list.Add($"mov {usedRegs[3]}, [{usedRegs[1]}]");
            instruction_list.Add($"and {usedRegs[3]}, 0x1");
            instruction_list.Add($"cmp {usedRegs[3]}, 1");
            instruction_list.Add($"je CHECK_SUCCESS");

            instruction_list.Add($"label CHECK_FAIL");
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"mov rax, 1");
            instruction_list.Add($"mov rbx, [rsp+0x30]");
            instruction_list.Add($"add rsp, 0x20");
            instruction_list.Add($"pop rdi");
            instruction_list.Add($"ret");

            instruction_list.Add($"label CHECK_SUCCESS");
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"mov rbx, [rsp+0x30]");
            instruction_list.Add($"add rsp, 0x20");
            instruction_list.Add($"pop rdi");
            instruction_list.Add($"ret");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        private void ReplacePregenRandomLookup(Int64 functionAddress, string pattern)
        {
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


            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_PREGEN, functionAddress, roomIdFromRegAddr:true);
            instruction_list.Add($"jne CHECK_FAIL");
            // Pregen floor, need to get corresponding random floor ID

            instruction_list.Add($"mov {usedRegs[2]}, {baseReg}");
            instruction_list.Add($"mov {usedRegs[0]}, {_randomPregenLinkTable+1}");
            instruction_list.Add($"mov {usedRegs[1]}, {_randomPregenLinkTable + (nuint)(_linkList.Count()*2)}");


            instruction_list.Add($"label LOOP2_START");
            instruction_list.Add($"mov {usedRegs[3]}, [{usedRegs[0]}]");
            instruction_list.Add($"and {usedRegs[3]}, 0xFF");
            instruction_list.Add($"cmp {usedRegs[3]}, {usedRegs[2]}");
            instruction_list.Add($"je FOUND_MATCH");
            instruction_list.Add($"add {usedRegs[0]}, 2");
            instruction_list.Add($"cmp {usedRegs[1]}, {usedRegs[0]}");
            instruction_list.Add($"jne LOOP2_START");
            // Just gonna keep crashing for the moment
            instruction_list.Add($"ret");


            instruction_list.Add($"label FOUND_MATCH");
            instruction_list.Add($"mov {usedRegs[3]}, [{usedRegs[0]}-1]");
            instruction_list.Add($"and {usedRegs[3]}, 0xFF");
            instruction_list.Add($"mov {baseReg}, {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"xor rcx, rcx");
            instruction_list.Add($"jmp RETURN");

            instruction_list.Add($"label CHECK_FAIL");
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"mov rcx, [rsp+0x38]");
            instruction_list.Add($"and rcx, 0xFF");

            instruction_list.Add($"label RETURN");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }
        private void ReplaceDungeonBattleLookup(Int64 functionAddress, string pattern)
        {
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


            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_RANDOM, functionAddress);
            instruction_list.Add($"je RANDOM_CONVERT");

            instruction_list.Add($"cmp {usedRegs[3]}, {(int)RoomLoadType.DUNGEON_PREGEN}");
            instruction_list.Add($"jne CHECK_FAIL");
            // Pregen floor, need to get corresponding random floor ID

            instruction_list.Add($"mov {usedRegs[2]}, {baseReg}");
            instruction_list.Add($"mov {usedRegs[0]}, {_pregenBattleLinkTable}");
            instruction_list.Add($"mov {usedRegs[1]}, {_pregenBattleLinkTable + (nuint)(_linkList.Count()*2)}");
            instruction_list.Add($"jmp LOOP2_START");

            instruction_list.Add($"label RANDOM_CONVERT");
            instruction_list.Add($"mov {usedRegs[2]}, {baseReg}");
            instruction_list.Add($"mov {usedRegs[0]}, {_randomBattleLinkTable}");
            instruction_list.Add($"mov {usedRegs[1]}, {_randomBattleLinkTable + (nuint)(_linkList.Count()*2)}");


            instruction_list.Add($"label LOOP2_START");
            instruction_list.Add($"mov {usedRegs[3]}, [{usedRegs[0]}]");
            instruction_list.Add($"and {usedRegs[3]}, 0xFF");
            instruction_list.Add($"cmp {usedRegs[3]}, {usedRegs[2]}");
            instruction_list.Add($"je FOUND_MATCH");
            instruction_list.Add($"add {usedRegs[0]}, 2");
            instruction_list.Add($"cmp {usedRegs[1]}, {usedRegs[0]}");
            instruction_list.Add($"jne LOOP2_START");
            // Just gonna keep crashing for the moment
            instruction_list.Add($"ret");


            instruction_list.Add($"label FOUND_MATCH");
            instruction_list.Add($"mov {usedRegs[3]}, [{usedRegs[0]}+1]");
            instruction_list.Add($"and {usedRegs[3]}, 0xFF");
            instruction_list.Add($"mov {baseReg}, {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");

            instruction_list.Add($"jmp RETURN");

            instruction_list.Add($"label CHECK_FAIL");
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");


            instruction_list.Add($"label RETURN");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        /*
         * Logic that dictates if the room's camera pans as the player moves throughout the room, used for bigger rooms like
         * the Shopping District and Flood Banks.
         * 
         * List of rooms that use the pan camera (according to the game logic)
         * 
                Fields: 6
                ==Rooms: 14, 19
                (School roof)

                Fields: 7
                ==Rooms: 1, 4
                (Outside Dojima's house)

                Fields: 8
                ==Rooms: 1, 4, 5, 6, 7, 8, 9, 11, 12
                (Various parts of the shopping district and the shrine)

                Fields: 10
                ==Rooms: all, check for the room ID can't be hit (afaik)
                (Samegawa Flood Plain, both the road and the riverbank)

                Fields: 11, 18, 
                ==Rooms: 1
                (Okina Station Front, Mountain Road [winter event])

                Fields: 22, 23, 24, 25, 26, 27, 28, 30, 31
                ==Rooms: 1
                ( All dungeon entrances, exempting Magatsu Inaba )

                Fields: 29
                == Rooms: 2
                ( Magatsu Inaba, right before the murderer fight )

                Attempting to load in stuff that isn't built for panning cams crashes.
        */
        private void ReplaceFieldCameraPanCheck(Int64 functionAddress, string pattern)
        {
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

            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.OVERWORLD, functionAddress);
            instruction_list.Add($"je CHECK_FLAGS");
            instruction_list.Add($"cmp {usedRegs[3]}, {(int)RoomLoadType.DUNGEON_STATIC}");
            instruction_list.Add($"jne CHECK_FAIL");
            instruction_list.Add($"label CHECK_FLAGS");

            // Check the bitflag
            instruction_list.Add($"add {usedRegs[1]}, 1");
            instruction_list.Add($"mov {usedRegs[3]}, [{usedRegs[1]}]");
            instruction_list.Add($"and {usedRegs[3]}, 0x2");
            instruction_list.Add($"cmp {usedRegs[3]}, 2");
            instruction_list.Add($"jne CHECK_FAIL");

            instruction_list.Add($"label CHECK_SUCCESS");
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"mov {usedRegs[0]}, {_utils.SigScan("48 8D 4D B8 48 FF C9 66 0F 1F 84 00 00 00 00 00", "PanCamTrue")}");
            instruction_list.Add($"mov [rsp+8], {usedRegs[0]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"ret");

            instruction_list.Add($"label CHECK_FAIL");
            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"mov {usedRegs[0]}, {_utils.SigScan("33 C0 48 8B 4D F8 48 33 CC E8 6A BD 54 00", "PanCamFalse")}");
            instruction_list.Add($"mov [rsp+8], {usedRegs[0]}");
            instruction_list.Add($"pop {usedRegs[0]}");
            instruction_list.Add($"ret");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        private void ReplaceCameraCollisionLoadCheck(Int64 functionAddress, string pattern)
        {
            AccessorRegister lookupAddrReg = AccessorRegister.rax;
            AccessorRegister compareAddrReg = AccessorRegister.rbx;
            AccessorRegister intermediateRegA = AccessorRegister.rcx;
            AccessorRegister intermediateRegB = AccessorRegister.rdx;
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


            usedRegs = SetupRegisters();

            instruction_list.Add($"push {usedRegs[0]}");
            instruction_list.Add($"push {usedRegs[1]}");
            instruction_list.Add($"push {usedRegs[2]}");
            instruction_list.Add($"push {usedRegs[3]}");

            CheckForRoomType(instruction_list, usedRegs, RoomLoadType.DUNGEON_RANDOM, functionAddress, checkBaseAgainstRam:true);

            instruction_list.Add($"pop {usedRegs[3]}");
            instruction_list.Add($"pop {usedRegs[2]}");
            instruction_list.Add($"pop {usedRegs[1]}");
            instruction_list.Add($"pop {usedRegs[0]}");




            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        private List<AccessorRegister> SetupRegisters()
        {
            List<AccessorRegister> registers = new();
            AccessorRegister lookupAddrReg = AccessorRegister.rax;
            AccessorRegister compareAddrReg = AccessorRegister.rbx;
            AccessorRegister intermediateRegA = AccessorRegister.rcx;
            AccessorRegister intermediateRegB = AccessorRegister.rdx;
            if (baseReg == AccessorRegister.rax)
            {
                if (!(outReg == AccessorRegister.r8))
                {
                    lookupAddrReg = AccessorRegister.r8;
                }
                else
                {
                    lookupAddrReg = AccessorRegister.r9;
                }
            }
            else if (outReg == AccessorRegister.rax)
            {
                if (!(baseReg == AccessorRegister.r8))
                {
                    lookupAddrReg = AccessorRegister.r8;
                }
                else
                {
                    lookupAddrReg = AccessorRegister.r9;
                }
            }
            registers.Add(lookupAddrReg);

            if (baseReg == AccessorRegister.rbx)
            {
                if (!(outReg == AccessorRegister.rsi))
                {
                    compareAddrReg = AccessorRegister.rsi;
                }
                else
                {
                    compareAddrReg = AccessorRegister.r10;
                }
            }
            else if (outReg == AccessorRegister.rbx)
            {
                if (!(baseReg == AccessorRegister.rsi))
                {
                    compareAddrReg = AccessorRegister.rsi;
                }
                else
                {
                    compareAddrReg = AccessorRegister.r10;
                }
            }
            registers.Add(compareAddrReg);

            if (baseReg == AccessorRegister.rcx)
            {
                if (!(outReg == AccessorRegister.rdi))
                {
                    intermediateRegA = AccessorRegister.rdi;
                }
                else
                {
                    intermediateRegA = AccessorRegister.r11;
                }
            }
            else if (outReg == AccessorRegister.rcx)
            {
                if (!(baseReg == AccessorRegister.rdi))
                {
                    intermediateRegA = AccessorRegister.rdi;
                }
                else
                {
                    intermediateRegA = AccessorRegister.r11;
                }
            }
            registers.Add(intermediateRegA);

            if (baseReg == AccessorRegister.rdx)
            {
                if (!(outReg == AccessorRegister.r12))
                {
                    intermediateRegB = AccessorRegister.r12;
                }
                else
                {
                    intermediateRegB = AccessorRegister.r13;
                }
            }
            else if (outReg == AccessorRegister.rdx)
            {
                if (!(baseReg == AccessorRegister.r12))
                {
                    intermediateRegB = AccessorRegister.r12;
                }
                else
                {
                    intermediateRegB = AccessorRegister.r13;
                }
            }
            registers.Add(intermediateRegB);

            return registers;
        }

        // pullFieldFromMemory is needed because one or two of the functions being replaced don't have the field stored in a register
        // Likewise, one function pulls the room ID not from a known memory location, but from an address in RBX
        private void CheckForRoomType(List<string> instruction_list, List<AccessorRegister> registers, RoomLoadType roomType, Int64 functionAddress, bool pullFieldFromMemory = false, bool roomIdFromRegAddr = false, bool checkBaseAgainstRam = false)
        {
            instruction_list.Add($"mov {registers[0]}, {_fieldComparesLookupAddress}");

            if (!pullFieldFromMemory)
            {
                instruction_list.Add($"mov {registers[2]}, {baseReg}");
            }
            else
            {
                instruction_list.Add($"mov {registers[2]}, {(Int64)0x140ECA340}");
                instruction_list.Add($"mov {registers[2]}, [{registers[2]}]");
            }
            instruction_list.Add($"and {registers[2]}, 0xFF");

            // Multiply by 8 to account for address size
            instruction_list.Add($"shl {registers[2]}, 3");
            instruction_list.Add($"add {registers[0]}, {registers[2]}");
            instruction_list.Add($"mov {registers[1]}, [{registers[0]}]");

            // Get room ID
            if (!roomIdFromRegAddr)
            {
                if (checkBaseAgainstRam)
                {


                    instruction_list.Add($"mov {registers[0]}, {(Int64)0x140ECA340}");
                    instruction_list.Add($"mov {registers[0]}, [{registers[0]}]");
                    instruction_list.Add($"and {registers[0]}, 0xFF");
                    instruction_list.Add($"cmp {baseReg}, {registers[0]}");
                    instruction_list.Add($"jne DIFFERENT");
                    instruction_list.Add($"mov {registers[0]}, {(Int64)0x140ECA344}");
                    instruction_list.Add($"mov {registers[3]}, [{registers[0]}]");
                    instruction_list.Add($"jmp CONTINUE");
                    instruction_list.Add("label DIFFERENT");

                    instruction_list.Add($"mov {registers[3]}, 0");

                    instruction_list.Add("label CONTINUE");
                }
                else
                {
                    instruction_list.Add($"mov {registers[0]}, {(Int64)0x140ECA344}");
                    instruction_list.Add($"mov {registers[3]}, [{registers[0]}]");
                }
            }
            else
            {
                instruction_list.Add($"mov {registers[3]}, rbx");

            }
            instruction_list.Add($"and {registers[3]}, 0xFF");

            // Find address of next entry in table
            if (!pullFieldFromMemory)
            {
                instruction_list.Add($"mov {registers[2]}, {baseReg}");
            }
            else
            {
                instruction_list.Add($"mov {registers[2]}, {(Int64)0x140ECA340}");
                instruction_list.Add($"mov {registers[2]}, [{registers[2]}]");
            }
            instruction_list.Add($"and {registers[2]}, 0xFF");
            instruction_list.Add($"add {registers[2]}, 1");
            instruction_list.Add($"shl {registers[2]}, 3");
            instruction_list.Add($"mov {registers[0]}, {_fieldComparesLookupAddress}");
            instruction_list.Add($"add {registers[0]}, {registers[2]}");
            instruction_list.Add($"mov {registers[2]}, [{registers[0]}]");


            instruction_list.Add($"label LOOP_START");
            instruction_list.Add($"mov {registers[0]}, [{registers[1]}]");
            instruction_list.Add($"and {registers[0]}, 0xFF");
            instruction_list.Add($"cmp {registers[3]}, {registers[0]}");
            instruction_list.Add($"je FOUND_DATA");
            instruction_list.Add($"add {registers[1]}, 3");
            instruction_list.Add($"cmp {registers[1]}, {registers[2]}");
            instruction_list.Add($"jne LOOP_START");
            // Something's gone wrong, gonna crash the game for the moment
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"{_logCrashCallMnemonic}");

            instruction_list.Add($"label FOUND_DATA");

            instruction_list.Add($"add {registers[1]}, 1");
            instruction_list.Add($"mov {registers[3]}, [{registers[1]}]");
            instruction_list.Add($"and {registers[3]}, 0xFF");

            instruction_list.Add($"cmp {registers[3]}, {(int)roomType}");
        }
    }
}
