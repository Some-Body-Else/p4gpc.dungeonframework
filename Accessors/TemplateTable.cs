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
using static System.Formats.Asn1.AsnWriter;
using System.Xml.Serialization;
// using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.dungeonloader.Accessors
{
    public class TemplateTable : Accessor
    {

        /*
        To do:
            -Handle the internal list that tells the game which dungeons have which templates
            -Need to properly handle the expected distance between tables, each entry in the template table is EXPECTED to be 12 bytes long, need a more direct rewrite here instead of address replacement
         */

        private List<DungeonTemplates> _templates;
        private Dictionary<byte, byte> _dungeon_template_dict;
        private nuint _templateLookupTable;
        private nuint _templateExitLookupTable;

        // TODO: See if we can easily use this to circumvent code replacements elegantly
        private nuint _currentTemplate;
        private nuint _templateTypeTable;
        private long _templateTable;


        private byte mod;
        private byte scale;
        private AccessorRegister outReg;
        private AccessorRegister inReg;
        private AccessorRegister baseReg;
        private Instruction type;
        private enum Instruction
        {
            MOVSX = 0,
            MOVZX = 1,
            CMP = 2,
            LEA = 3,
            MULTI = 4
        }

        private enum TemplateAccessType
        {
            ROOM_COUNT = 0,
            ROOM_COUNT_EX = 1,
            ROOM_ID = 2
        };

        public TemplateTable(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {
            _templates = jsonImporter.GetTemplates();
            _dungeon_template_dict = jsonImporter.GetDungeonTemplateDictionary();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Templates hooks established.", Config.DebugLevels.AlertConnections);
        }

        protected override void Initialize()
        {
            //Debugger.Launch();
            TemplateAccessType accessType;
            Instruction instruction_type;
            List<Int64> functions;
            Int64 function_single;
            Int64 address;
            Int32 totalTemplateTableSize = 0;
            String address_str_new;
            String address_str_old;
            String search_string;
            nuint templateTableAddress;
            byte prefixREX;
            byte idByte1;
            byte idByte2;
            byte idByte3;
            byte rmByte;
            byte sibByte;
            bool hasREX;

            _currentTemplate = _memory.Allocate(sizeof(byte));


            _templateTable = _utils.SigScan("08 09 01 02 03 05 06 07 09 0A 04 00 09 0A 01 02 03 05 07 06 08 0B 0C 04 08 09 01 02 03 05 06 08 0D 0E 04 00", "TemplateTable");
            _templateTable = _utils.StripBaseAddress(_templateTable);
            
            _templateLookupTable = _memory.Allocate(_templates.Count() * DOUBLEWORD);
            _utils.LogDebug($"Location of TemplateLookupTable table: {_templateLookupTable.ToString("X8")}", Config.DebugLevels.TableLocations);
            
            _templateExitLookupTable = _memory.Allocate(_templates.Count());
            _utils.LogDebug($"Location of TemplateExitLookupTable table: {_templateExitLookupTable.ToString("X8")}", Config.DebugLevels.TableLocations);
            
            _templateTypeTable = _memory.Allocate(sizeof(byte)*256);
            _utils.LogDebug($"Location of TemplateTypeTable table: {_templateTypeTable.ToString("X8")}", Config.DebugLevels.TableLocations);

            for (int i = 0; i <= 255; i++)
            {
                if (_dungeon_template_dict.TryGetValue((byte)i, out idByte1))
                {
                    _memory.SafeWrite(_templateTypeTable+(nuint)i, _dungeon_template_dict[(byte)i]);
                }
                else
                {
                    _memory.SafeWrite(_templateTypeTable+(nuint)i, 0xFF);
                }
            }
            /*
            4? opcode indicates that we're using extended opcodes 
            */
            int tablecounter = 0;
            foreach (DungeonTemplates template in _templates)
            {

                totalTemplateTableSize = 0;
                templateTableAddress = _memory.Allocate((template.roomExCount + 2));
                _memory.SafeWrite(_templateLookupTable + (nuint)(DOUBLEWORD*tablecounter), templateTableAddress);

                _memory.SafeWrite(templateTableAddress, template.roomCount);
                _memory.SafeWrite(templateTableAddress+1, template.roomExCount);
                totalTemplateTableSize += 2;
                foreach (byte room in template.rooms)
                {
                    _memory.SafeWrite((templateTableAddress + (nuint)(totalTemplateTableSize)), room);
                    totalTemplateTableSize++;
                }
                _memory.SafeWrite(_templateExitLookupTable+(nuint)tablecounter, template.exitNum);

                tablecounter++;
            }


            // Special replacements:
            // 48 8D 0C 40 0F B6 B4 8B [ADDRESS]
            // 4B 8D 14 40 0F B6 8C 90 [ADDRESS]

            // 8D ?? ?? 0F ?? ?? ??
            // Needs to happen because these MOVE commands don't use the REX prefix byte.
            // Don't think there's a more abstract way to handle them, byte before each opcode 
            // structurally is a valid REX byte but is part of preceeding opcode
            // [ADDRESS] for both is basic address, 0x00A7B378

            address_str_old = (_templateTable).ToString("X8");
            address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
            search_string = "8D ?? ?? 0F ?? ?? ?? " + address_str_old;
            functions = _utils.SigScan_FindAll(search_string, "TemplateTable Move/Compare Opcodes");
            foreach (long function in functions)
            {
                hasREX = false;
                _memory.SafeRead((nuint)(function-1), out prefixREX);
                _memory.SafeRead((nuint)(function+4), out idByte1);
                if (idByte1 == 0xBE)
                {
                    instruction_type = Instruction.MOVSX;
                }
                else if (idByte1 == 0xB6)
                {
                    instruction_type = Instruction.MOVZX;
                }
                else
                {
                    throw new InvalidAsmInstructionTypeException(function, _utils);
                }
                _memory.SafeRead((nuint)(function+5), out rmByte);
                _memory.SafeRead((nuint)(function+2), out sibByte);
                mod = (byte)(rmByte >> 6);
                scale = (byte)(sibByte >> 6);
                outReg = (AccessorRegister)((rmByte >> 3) & 0x7);           //Reg_Out   rm.REG
                inReg = (AccessorRegister)((sibByte >> 3) & 0x7);           //Reg_In    sib.INDEX
                baseReg = (AccessorRegister)(sibByte & 0x7);                //Reg_Base  sib.BASE
                if (prefixREX >0x4F || prefixREX < 0x40)
                {
                }
                else
                {
                    baseReg += (prefixREX & 0x1) << 3;
                    inReg += (prefixREX & 0x2) << 2;
                    hasREX = true;
                }
                _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                ReplaceMoveInstructionE(function, search_string, TemplateAccessType.ROOM_COUNT);
            }




            /*
             Okay, really special note about this next search and replace:

            It gets the room ID of whatever room is designated as the exit for that particular template.
            Immediately afterward, that value is used to get values for the exit in the room table (86-byte table).
            The offset value used for the program (0x140000000) is used from the same register, which is why the
            push/pop setup in the function is there.

            Making note of this specifically for when we tackle the rooms, since we may want to refactor this specifically.
             */

            // 0F ?? ?? ?? AD DR ES S! ?? ?? ?? ?? 0F ?? ?? ?? AD DR ES S!
            address_str_old = (_templateTable).ToString("X8");
            address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
            // _utils.LogDebug($"Old template table address for search: {address_str_old} bytes", 2);
            search_string = "0F ?? ?? ?? " + address_str_old;

            address_str_old = (_templateTable+1).ToString("X8");
            address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
            search_string = search_string + " ?? ?? ?? ?? 0F ?? ?? ?? " + address_str_old;
            functions = _utils.SigScan_FindAll(search_string, "TemplateTable Move/Compare Opcodes");
            foreach (long function in functions)
            {
                hasREX = false;

                _memory.SafeRead((nuint)(function-1), out prefixREX);
                _memory.SafeRead((nuint)(function+1), out idByte1);
                if (idByte1 == 0xBE)
                {
                    instruction_type = Instruction.MOVSX;
                }
                else if (idByte1 == 0xB6)
                {
                    instruction_type = Instruction.MOVZX;
                }
                else
                {
                    throw new InvalidAsmInstructionTypeException(function, _utils);
                }
                _memory.SafeRead((nuint)(function+2), out rmByte);
                _memory.SafeRead((nuint)(function+3), out sibByte);
                mod = (byte)(rmByte >> 6);
                scale = (byte)(sibByte >> 6);
                outReg = (AccessorRegister)((rmByte >> 3) & 0x7);           //Reg_Out   rm.REG
                inReg = (AccessorRegister)((sibByte >> 3) & 0x7);           //Reg_In    sib.INDEX
                baseReg = (AccessorRegister)(sibByte & 0x7);                //Reg_Base  sib.BASE
                if (prefixREX >0x4F || prefixREX < 0x40)
                {
                }
                else
                {
                    baseReg += (prefixREX & 0x1) << 3;
                    inReg += (prefixREX & 0x2) << 2;
                    hasREX = true;
                }
                // _utils.LogDebug($"Location: {function.ToString("X8")}, RM: {rmByte.ToString("X8")}, mod: {mod}, SIB: {sibByte.ToString("X8")}, scale: {scale},  OUT: {outReg}, IN: {inReg}, BASE: {baseReg}", 3);
                _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                GetExitRoomID(function, search_string, hasREX);
            }

            for (int i = 0; i < 3; i++)
            {
                byte temp;
                address_str_old = (_templateTable+i).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                if (i != 2)
                {
                    search_string = "0F ?? ?? ?? " + address_str_old;
                    functions = _utils.SigScan_FindAll(search_string, "TemplateTable Move/Compare Opcodes");
                    // _utils.LogDebug($"Function count: {functions.Count()}", 3);
                    foreach (long function in functions)
                    {
                        hasREX = false;
                        _memory.SafeRead((nuint)(function-1), out prefixREX);
                        _memory.SafeRead((nuint)(function+1), out idByte1);
                        if (idByte1 == 0xBE)
                        {
                            instruction_type = Instruction.MOVSX;
                        }
                        else if (idByte1 == 0xB6)
                        {
                            instruction_type = Instruction.MOVZX;
                        }
                        else
                        {
                            throw new InvalidAsmInstructionTypeException(function, _utils);
                        }
                        _memory.SafeRead((nuint)(function+2), out rmByte);
                        _memory.SafeRead((nuint)(function+3), out sibByte);
                        mod = (byte)(rmByte >> 6);
                        scale = (byte)(sibByte >> 6);
                        outReg = (AccessorRegister)((rmByte >> 3) & 0x7);           //Reg_Out   rm.REG
                        inReg = (AccessorRegister)((sibByte >> 3) & 0x7);           //Reg_In    sib.INDEX
                        baseReg = (AccessorRegister)(sibByte & 0x7);                //Reg_Base  sib.BASE

                        if (prefixREX >0x4F || prefixREX < 0x40)
                        {
                            
                        }
                        else
                        {
                            baseReg += (prefixREX & 0x1) << 3;
                            inReg += (prefixREX & 0x2) << 2;
                            outReg += ((prefixREX & 0x4)) << 1;
                            hasREX = true;
                        }

                        // _utils.LogDebug($"Location: {function.ToString("X8")}, RM: {rmByte.ToString("X8")}, mod: {mod}, SIB: {sibByte.ToString("X8")}, scale: {scale},  OUT: {outReg}, IN: {inReg}, BASE: {baseReg}", 3);
                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

                        ReplaceMoveInstruction(function, search_string, (TemplateAccessType)i, hasREX);
                    }
                }
                else
                {
                    search_string = "8D ?? ?? ?? 0F ?? ?? ?? " + address_str_old;
                    functions = _utils.SigScan_FindAll(search_string, "TemplateTable Move/Compare Opcodes");
                    foreach (long function in functions)
                    {
                        hasREX = false;
                        // Need details from previous op to do what we intend to do
                        _memory.SafeRead((nuint)(function-1), out prefixREX);
                        _memory.SafeRead((nuint)(function), out idByte1);
                        if (idByte1 == 0x8D)
                        {
                            instruction_type = Instruction.LEA;
                        }
                        else
                        {
                            throw new ToBeNamedException(_utils);
                        }
                        _memory.SafeRead((nuint)(function+2), out sibByte);
                        inReg = (AccessorRegister)((sibByte >> 3) & 0x7);       //Reg_In    sib.INDEX
                        baseReg = (AccessorRegister)(sibByte & 0x7);            //Reg_Base  sib.BASE
                        if (prefixREX >0x4F || prefixREX < 0x40)
                        {
                        }
                        else
                        {
                            baseReg += (prefixREX & 0x1) << 3;
                            inReg += (prefixREX & 0x2) << 2;
                            hasREX = true;
                        }

                        _memory.SafeRead((nuint)(function+3), out prefixREX);
                        _memory.SafeRead((nuint)(function+5), out idByte1);
                        if (idByte1 == 0xBE)
                        {
                            instruction_type = Instruction.MOVSX;
                        }
                        else if (idByte1 == 0xB6)
                        {
                            instruction_type = Instruction.MOVZX;
                        }
                        else
                        {
                            throw new InvalidAsmInstructionTypeException(function, _utils);
                        }
                        _memory.SafeRead((nuint)(function+6), out rmByte);
                        mod = (byte)(rmByte >> 6);
                        scale = (byte)(sibByte >> 6);
                        outReg = (AccessorRegister)((rmByte >> 3) & 0x7);       //Reg_Out   rm.REG

                        if (prefixREX >0x4F || prefixREX < 0x40)
                        {
                        }
                        else
                        {
                            outReg += ((prefixREX & 0x4)) << 1;
                        }

                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        ReplaceMoveInstruction(function, search_string, (TemplateAccessType)i, hasREX);
                    }
                }
            }
;


            // The 0xBC maybe should be a wildcard, but that requires effort I'm simply not going to put in right now
            // Doesn't make a difference with searches now, but maybe will in the future (doubtful)
            for (int i = 0; i < 3; i++)
            {
                byte temp;
                address_str_old = (_templateTable+i).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                if (i != 2)
                {
                    search_string = "41 80 BC ?? " + address_str_old;
                    functions = _utils.SigScan_FindAll(search_string, "TemplateTable Move/Compare Opcodes");
                    foreach (long function in functions)
                    {
                        hasREX = true;
                        _memory.SafeRead((nuint)(function), out prefixREX);
                        _memory.SafeRead((nuint)(function+1), out idByte1);
                        if (idByte1 == 0x80)
                        {
                            instruction_type = Instruction.CMP;
                        }
                        else
                        {
                            throw new ToBeNamedException(_utils);
                        }
                        // _memory.SafeRead((nuint)(function+1), out rmByte);
                        _memory.SafeRead((nuint)(function+3), out sibByte);
                        // mod = (byte)(rmByte >> 6);
                        scale = (byte)(sibByte >> 6);
                        // output register is irrelevant, since everything revolves around a comparison to an immediate value
                        // outReg = (AccessorRegister)((rmByte >> 3) & 0x7);           //Reg_Out   rm.REG
                        inReg = (AccessorRegister)((sibByte >> 3) & 0x7);           //Reg_In    sib.INDEX
                        baseReg = (AccessorRegister)(sibByte & 0x7);                //Reg_Base  sib.BASE
                        _memory.SafeRead((nuint)(function+8), out temp);

                        if (prefixREX >0x4F || prefixREX < 0x40)
                        {
                        }
                        else
                        {
                            baseReg += (prefixREX & 0x1) << 3;
                            inReg += (prefixREX & 0x2) << 2;
                            hasREX = true;
                            // outReg += ((prefixREX & 0x4)) << 1;
                        }

                        if ((prefixREX & 0x8) != 0)
                        {
                        }
                        _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                        // ReplaceMoveInstruction(function, search_string, (TemplateAccessType)i);
                        ReplaceCompareInstruction(function, search_string, (TemplateAccessType)i, temp, false);

                    }
                }
                else
                {
                    search_string = "48 8D ?? ?? 42 80 BC ?? " + address_str_old + " 06";
                    function_single = _utils.SigScan(search_string, "TemplateTable Move/Compare Opcodes");
                    // Need details from previous op to do what we intend to do
                    _memory.SafeRead((nuint)(function_single), out prefixREX);
                    _memory.SafeRead((nuint)(function_single+1), out idByte1);
                    if (idByte1 == 0x8D)
                    {
                        instruction_type = Instruction.LEA;
                    }
                    else
                    {
                        throw new ToBeNamedException(_utils);
                    }
                    _memory.SafeRead((nuint)(function_single+3), out sibByte);
                    inReg = (AccessorRegister)((sibByte >> 3) & 0x7);       //Reg_In    sib.INDEX
                    baseReg = (AccessorRegister)(sibByte & 0x7);            //Reg_Base  sib.BASE
                    if (prefixREX >0x4F || prefixREX < 0x40)
                    {
                    }
                    else
                    {
                        baseReg += (prefixREX & 0x1) << 3;
                        inReg += (prefixREX & 0x2) << 2;
                    }

                    _memory.SafeRead((nuint)(function_single+4), out prefixREX);
                    _memory.SafeRead((nuint)(function_single+5), out idByte1);
                    if (idByte1 == 0x80)
                    {
                        instruction_type = Instruction.CMP;
                    }
                    else
                    {
                        throw new ToBeNamedException(_utils);
                    }
                    scale = (byte)(sibByte >> 6);

                    _memory.SafeRead((nuint)(function_single+12), out temp);

                    _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                    ReplaceCompareInstructionE(function_single, search_string, (TemplateAccessType)i, temp, false);
                    
                }
            }

            // Might want a better search for this one
            // 8D ?? [ADDRESS] ?? 8D ?? ??
            // [ADDRESS = 0x00A7B37A
            // _utils.LogDebug($"Old template table address for search: {search_string} bytes", 2);
            hasREX = false;
            address_str_old = (_templateTable+2).ToString("X8");
            address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);

            search_string = "8D ?? " + address_str_old + " ?? 8D ?? ??";
            function_single = _utils.SigScan(search_string, "TemplateTable Move/Compare Opcodes");
            // Replacement call here
            _memory.SafeRead((nuint)(function_single-1), out prefixREX);
            _memory.SafeRead((nuint)(function_single), out idByte1);
            if (idByte1 == 0x8D)
            {
                instruction_type = Instruction.LEA;
            }
            else
            {
                throw new ToBeNamedException(_utils);
            }
            //baseReg = (AccessorRegister)(sibByte & 0x7);            //Reg_Base  sib.BASE
            if (prefixREX >0x4F || prefixREX < 0x40)
            {
            }
            else
            {
                baseReg += (prefixREX & 0x1) << 3;
                inReg += (prefixREX & 0x2) << 2;
                hasREX = true;
            }

            _memory.SafeRead((nuint)(function_single+6), out prefixREX);
            _memory.SafeRead((nuint)(function_single+7), out idByte1);
            if (idByte1 == 0x8D)
            {
                instruction_type = Instruction.LEA;
            }
            else
            {
                throw new ToBeNamedException(_utils);
            }

            _memory.SafeRead((nuint)(function_single+8), out rmByte);
            _memory.SafeRead((nuint)(function_single+9), out sibByte);
            inReg = (AccessorRegister)((sibByte >> 3) & 0x7);       //Reg_In    sib.INDEX
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);       //Reg_Out   rm.REG
            scale = (byte)(sibByte >> 6);
            mod = (byte)(rmByte >> 6);
            if (prefixREX >0x4F || prefixREX < 0x40)
            {
            }
            else
            {
                baseReg += (prefixREX & 0x1) << 3;
                inReg += (prefixREX & 0x2) << 2;
                hasREX = true;
                outReg += ((prefixREX & 0x4)) << 1;
            }
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceAddressSetupA(function_single, search_string, hasREX);

            
            search_string = "0F ?? ?? ?? 80 FE 90 00";
            functions = _utils.SigScan_FindAll(search_string, "TemplateTable Move/Compare Opcodes");
            foreach (long function in functions)
            {
                hasREX = true;
                _memory.SafeRead((nuint)(function-1), out prefixREX);
                _memory.SafeRead((nuint)(function+1), out idByte1);
                
                _memory.SafeRead((nuint)(function+2), out rmByte);
                _memory.SafeRead((nuint)(function+3), out sibByte);
                mod = (byte)(rmByte >> 6);
                scale = (byte)(sibByte >> 6);
                outReg = (AccessorRegister)((rmByte >> 3) & 0x7);           //Reg_Out   rm.REG
                inReg = (AccessorRegister)((sibByte >> 3) & 0x7);           //Reg_In    sib.INDEX
                baseReg = (AccessorRegister)(sibByte & 0x7);                //Reg_Base  sib.BASE

                if (prefixREX >0x4F || prefixREX < 0x40)
                {
                }
                else
                {
                    baseReg += (prefixREX & 0x1) << 3;
                    inReg += (prefixREX & 0x2) << 2;
                    outReg += ((prefixREX & 0x4)) << 1;
                    hasREX = true;
                }
                _utils.LogDebug($"Location of [{search_string}]: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                ReplaceTemplateTypeAccessA(function, search_string, hasREX);
            }
            

            search_string = "48 ?? ?? 00 00 00 00 ?? ?? ?? 80 FE 90 00 49 ?? ?? 08 01 00 00";
            function_single = _utils.SigScan(search_string, "TemplateTable Move/Compare Opcodes");
            _memory.SafeRead((nuint)(function_single), out prefixREX);
            _memory.SafeRead((nuint)(function_single+1), out idByte1);
            
            //_utils.LogDebug($"Opcode type: {instruction_type}", 4);
            _memory.SafeRead((nuint)(function_single+2), out rmByte);
            _memory.SafeRead((nuint)(function_single+3), out sibByte);
            mod = (byte)(rmByte >> 6);
            scale = (byte)(sibByte >> 6);
            inReg = (AccessorRegister)((sibByte >> 3) & 0x7);       //Reg_In    sib.INDEX
            baseReg = (AccessorRegister)(sibByte & 0x7);            //Reg_Base  sib.BASE
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);       //Reg_Out
            if (prefixREX >0x4F || prefixREX < 0x40)
            {
            }
            else
            {
                baseReg += (prefixREX & 0x1) << 3;
                inReg += (prefixREX & 0x2) << 2;
                outReg += ((prefixREX & 0x4)) << 1;
            }
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceTemplateTypeAccessB(function_single, search_string);

            search_string = "4D ?? ?? 80 FE 90 00 ?? ?? ?? 08 01 00 00";
            function_single = _utils.SigScan(search_string, "TemplateTable Move/Compare Opcodes");
            _memory.SafeRead((nuint)(function_single), out prefixREX);
            _memory.SafeRead((nuint)(function_single+1), out idByte1);

            //_utils.LogDebug($"Opcode type: {instruction_type}", 4);
            _memory.SafeRead((nuint)(function_single+2), out rmByte);
            _memory.SafeRead((nuint)(function_single+3), out sibByte);
            mod = (byte)(rmByte >> 6);
            scale = (byte)(sibByte >> 6);
            inReg = (AccessorRegister)((sibByte >> 3) & 0x7);       //Reg_In    sib.INDEX
            baseReg = (AccessorRegister)(sibByte & 0x7);            //Reg_Base  sib.BASE
            outReg = (AccessorRegister)((rmByte >> 3) & 0x7);       //Reg_Out
            if (prefixREX >0x4F || prefixREX < 0x40)
            {
            }
            else
            {
                baseReg += (prefixREX & 0x1) << 3;
                inReg += (prefixREX & 0x2) << 2;
                outReg += ((prefixREX & 0x4)) << 1;
            }
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            ReplaceTemplateTypeAccessB(function_single, search_string);


            search_string = "49 ?? ?? F6 A5 23 C5 41 ??";
            function_single = _utils.SigScan(search_string, "TemplateTable Move/Compare Opcodes");
            _memory.SafeRead((nuint)(function_single), out prefixREX);
            _memory.SafeRead((nuint)(function_single+1), out idByte1);
            _memory.SafeRead((nuint)(function_single+8), out rmByte);
            _memory.SafeRead((nuint)(function_single+2), out sibByte);
            mod = (byte)(rmByte >> 6);
            scale = (byte)(sibByte >> 6);
            inReg = (AccessorRegister)((sibByte >> 3) & 0x7);       //Reg_In    sib.INDEX
            baseReg = (AccessorRegister)(sibByte & 0x7);            //Reg_Base  sib.BASE
            outReg = (AccessorRegister)(rmByte & 0x7);       //Reg_Out
            if (prefixREX >0x4F || prefixREX < 0x40)
            {
            }
            else
            {
                baseReg += (prefixREX & 0x1) << 3;
                inReg += (prefixREX & 0x2) << 2;

                _memory.SafeRead((nuint)(function_single+7), out prefixREX);
                if (prefixREX >0x4F || prefixREX < 0x40)
                {
                }
                else
                {
                    outReg += ((prefixREX & 0x1)) << 3;
                }
            }
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            ReplaceTemplateTypeAccessC(function_single, search_string);
 

            search_string = "48 0B B5 1C F1 5D 3C 40 8A 3E";
            function_single = _utils.SigScan(search_string, "TemplateTable Move/Compare Opcodes");
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            OddballReplace1(function_single, search_string);

            search_string = "4C 89 F9 41 5E 44 0A 30";
            function_single = _utils.SigScan(search_string, "TemplateTable Move/Compare Opcodes");
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            OddballReplace3(function_single, search_string);

            search_string = "48 8D 64 24 F8 48 2B 04 24 48 01 14 24 48 03 04 24 48 8D 64 24 08 49 89 81 10 01 00 00";
            function_single = _utils.SigScan(search_string, "TemplateTable Move/Compare Opcodes");
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            OddballReplace4(function_single, search_string);

            search_string = "4D 89 B1 60 01 00 00 41 56 49 F7 D6 4C 21 34 24 4C 8B 34 24 48 8D 64 24 08 4D 03 B1 00 02 00 00";
            function_single = _utils.SigScan(search_string, "TemplateTable Move/Compare Opcodes");
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            OddballReplace5(function_single, search_string);

            search_string = "49 33 81 00 02 00 00 49 89 81 E8 00 00 00 50 48 F7 D0";
            function_single = _utils.SigScan(search_string, "TemplateTable Move/Compare Opcodes");
            _utils.LogDebug($"Location of [{search_string}]: {function_single.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            OddballReplace6(function_single, search_string);

        }

        private void ReplaceMoveInstructionE(Int64 functionAddress, string pattern, TemplateAccessType accessType)
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

            instruction_list.Add($"push {inReg}");
            instruction_list.Add($"push {inReg}");
            instruction_list.Add($"push {baseReg}");

            instruction_list.Add($"shr {inReg}, 8");
            instruction_list.Add($"cmp {inReg}, 0");
            instruction_list.Add($"jne swap_regs");



            instruction_list.Add($"pop {baseReg}");
            instruction_list.Add($"pop {inReg}");
            instruction_list.Add($"jmp code_start");


            instruction_list.Add($"label swap_regs");
            instruction_list.Add($"pop {inReg}");
            instruction_list.Add($"pop {baseReg}");

            instruction_list.Add($"label code_start");

            instruction_list.Add($"and {inReg}, 255");

            GetTemplateEntryAddress(instruction_list, inReg);
            if (type == Instruction.MOVZX || type == Instruction.MOVSX)
            {
                switch (accessType)
                {
                    case TemplateAccessType.ROOM_COUNT:
                        instruction_list.Add($"movzx {outReg}, byte [{inReg}]");
                        // instruction_list.Add($"and {outReg}, 255");
                        break;

                    case TemplateAccessType.ROOM_COUNT_EX:
                        instruction_list.Add($"movzx {outReg}, byte [{inReg}+1]");
                        // instruction_list.Add($"and {outReg}, 255");
                        break;

                    case TemplateAccessType.ROOM_ID:
                        instruction_list.Add($"movzx {outReg}, byte [{inReg}+{baseReg}]");
                        // instruction_list.Add($"and {outReg}, 255");

                        break;

                    default:
                        break;
                }
            }
            else
            {
                throw new InvalidAsmInstructionModValueException(functionAddress, _utils);
            }

            if (inReg == outReg)
            {
                instruction_list.Add($"add rsp, 8");
            }
            else
            {
                instruction_list.Add($"pop {inReg}");
            }

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress-1, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)+1).Activate());
        }

        private void GetExitRoomID(Int64 functionAddress, string pattern, bool hasREX)
        {
            AccessorRegister pushReg;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            if (inReg == outReg)
            {

                instruction_list.Add($"push {baseReg}");
                instruction_list.Add($"push {inReg}");
            }
            else
            {
                instruction_list.Add($"push {inReg}");
                instruction_list.Add($"push {baseReg}");
            }

            instruction_list.Add($"push {inReg}");
            instruction_list.Add($"push {baseReg}");

            instruction_list.Add($"shr {inReg}, 8");
            instruction_list.Add($"cmp {inReg}, 0");
            instruction_list.Add($"jne swap_regs");



            instruction_list.Add($"pop {baseReg}");
            instruction_list.Add($"pop {inReg}");
            instruction_list.Add($"jmp code_start");


            instruction_list.Add($"label swap_regs");
            instruction_list.Add($"pop {inReg}");
            instruction_list.Add($"pop {baseReg}");

            instruction_list.Add($"label code_start");
            instruction_list.Add($"and {inReg}, 255");
            instruction_list.Add($"shr {inReg}, 2");

            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"push rax");
                instruction_list.Add($"mov rax, {inReg}");
            }
            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, 3");
            instruction_list.Add($"push rdx");
            instruction_list.Add($"mov rdx, 0");
            instruction_list.Add($"div rcx");
            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rcx");
            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"mov {inReg}, rax");
                instruction_list.Add($"pop rax");
            }

            instruction_list.Add($"movzx {outReg}, byte [{_templateExitLookupTable} + {inReg}]");

            if (inReg == outReg)
            {
                instruction_list.Add($"pop {baseReg}");
                instruction_list.Add($"pop {baseReg}");
            }
            else if (outReg == baseReg)
            {
                instruction_list.Add($"pop {inReg}");
                instruction_list.Add($"pop {inReg}");
            }
            else
            {
                instruction_list.Add($"pop {baseReg}");
                instruction_list.Add($"pop {inReg}");
            }

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), hasREX ? functionAddress-1 : functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, hasREX ? _utils.GetPatternLength(pattern)+1 : _utils.GetPatternLength(pattern)).Activate());
        }

        private void ReplaceMoveInstruction(Int64 functionAddress, string pattern, TemplateAccessType accessType, bool hasREX)
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

            // Debug info, notes the last function that was used before program crashed

            instruction_list.Add($"push {inReg}");

            instruction_list.Add($"push {inReg}");
            instruction_list.Add($"push {baseReg}");

            instruction_list.Add($"shr {inReg}, 8");
            instruction_list.Add($"cmp {inReg}, 0");
            instruction_list.Add($"jne swap_regs");



            instruction_list.Add($"pop {baseReg}");
            instruction_list.Add($"pop {inReg}");
            instruction_list.Add($"jmp code_start");


            instruction_list.Add($"label swap_regs");
            instruction_list.Add($"pop {inReg}");
            instruction_list.Add($"pop {baseReg}");

            instruction_list.Add($"label code_start");

            if (scale == 0)
            {
                //Value is passed in as-is, need to divide by 4 to get entry ID
                instruction_list.Add($"shr {inReg}, 2");
            }
            // instruction_list.Add($"shl {inReg}, 56");
            // instruction_list.Add($"shr {inReg}, 56");
            instruction_list.Add($"and {inReg}, 255");



            if (inReg != AccessorRegister.rax && outReg != AccessorRegister.rax && baseReg != AccessorRegister.rax)
            {
                pushReg = AccessorRegister.rax;
            }
            else if (inReg != AccessorRegister.rbx && outReg != AccessorRegister.rbx && baseReg != AccessorRegister.rbx)
            {
                pushReg = AccessorRegister.rbx;
            }
            else if (inReg != AccessorRegister.rcx && outReg != AccessorRegister.rcx && baseReg != AccessorRegister.rcx)
            {
                pushReg = AccessorRegister.rcx;
            }
            else
            {
                pushReg = AccessorRegister.rdx;
            }

            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"push rax");
                instruction_list.Add($"mov rax, {inReg}");
            }
            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, 3");
            instruction_list.Add($"push rdx");
            instruction_list.Add($"mov rdx, 0");
            instruction_list.Add($"div rcx");
            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rcx");
            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"mov {inReg}, rax");
                instruction_list.Add($"pop rax");
            }

            GetTemplateEntryAddress(instruction_list, inReg);
            if (type == Instruction.MOVZX || type == Instruction.MOVSX)
            {
                switch (accessType)
                {
                    case TemplateAccessType.ROOM_COUNT:
                        instruction_list.Add($"movzx {outReg}, byte [{inReg}]");
                        // instruction_list.Add($"and {outReg}, 255");
                        break;

                    case TemplateAccessType.ROOM_COUNT_EX:
                        instruction_list.Add($"movzx {outReg}, byte [{inReg}+1]");
                        // instruction_list.Add($"and {outReg}, 255");
                        break;

                    case TemplateAccessType.ROOM_ID:
                        instruction_list.Add($"movzx {outReg}, byte [{inReg}+{baseReg}+2]");
                        // instruction_list.Add($"and {outReg}, 255");

                        break;

                    default:
                        break;
                }
            }
            else
            {
                throw new InvalidAsmInstructionModValueException(functionAddress, _utils);
            }

            /*
                So, in the event that the in register contents are not modified since they are passed into our function, we need to reset
                then in case they're used again later on. If inReg is outReg, we ignore this but still need to change the value of the stack


             */
            if (inReg == outReg)
            {
                instruction_list.Add($"add rsp, 8");
            }
            else
            {
                instruction_list.Add($"pop {inReg}");
            }


            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), hasREX ? functionAddress-1 : functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, hasREX ? _utils.GetPatternLength(pattern)+1 : _utils.GetPatternLength(pattern)).Activate());
        }

        private void ReplaceCompareInstructionE(Int64 functionAddress, string pattern, TemplateAccessType accessType, byte compare, bool hasREX)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();

            /*
             Check to make sure that we have the right value in the right register.
             Was needed for at least one MOV opcode, so putting it here more as a precaution than
             anything else
             */

            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"push {inReg}");

            instruction_list.Add($"push {inReg}");
            instruction_list.Add($"push {baseReg}");

            instruction_list.Add($"shr {inReg}, 8");
            instruction_list.Add($"cmp {inReg}, 0");
            instruction_list.Add($"jne swap_regs");



            instruction_list.Add($"pop {baseReg}");
            instruction_list.Add($"pop {inReg}");
            instruction_list.Add($"jmp code_start");


            instruction_list.Add($"label swap_regs");
            instruction_list.Add($"pop {inReg}");
            instruction_list.Add($"pop {baseReg}");

            instruction_list.Add($"label code_start");

            if (scale == 0)
            {
                //Value is passed in as-is, need to divide by 4 to get entry ID
                instruction_list.Add($"shr {inReg}, 2");
            }
            instruction_list.Add($"and {inReg}, 255");

            if (inReg != AccessorRegister.rax && outReg != AccessorRegister.rax && baseReg != AccessorRegister.rax)
            {
                pushReg = AccessorRegister.rax;
            }
            else if (inReg != AccessorRegister.rbx && outReg != AccessorRegister.rbx && baseReg != AccessorRegister.rbx)
            {
                pushReg = AccessorRegister.rbx;
            }
            else if (inReg != AccessorRegister.rcx && outReg != AccessorRegister.rcx && baseReg != AccessorRegister.rcx)
            {
                pushReg = AccessorRegister.rcx;
            }
            else
            {
                pushReg = AccessorRegister.rdx;
            }

            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"push rax");
                instruction_list.Add($"mov rax, {inReg}");
            }
            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, 3");
            instruction_list.Add($"push rdx");
            instruction_list.Add($"mov rdx, 0");
            instruction_list.Add($"div rcx");
            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rcx");
            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"mov {inReg}, rax");
                instruction_list.Add($"pop rax");
            }
            GetTemplateEntryAddress(instruction_list, inReg);
            instruction_list.Add($"add {inReg}, 2");
            if (type == Instruction.MOVZX || type == Instruction.MOVSX)
            {
                switch (accessType)
                {
                    case TemplateAccessType.ROOM_COUNT:
                        instruction_list.Add($"cmp byte [{inReg}], {compare}");
                        break;
                    case TemplateAccessType.ROOM_COUNT_EX:
                        instruction_list.Add($"cmp byte [{inReg}+1], {compare}");
                        break;
                    case TemplateAccessType.ROOM_ID:
                        instruction_list.Add($"cmp byte [{inReg}+{baseReg}], {compare}");
                        break;
                    default:
                        break;
                }
            }
            else
            {
                throw new InvalidAsmInstructionModValueException(functionAddress, _utils);
            }
            instruction_list.Add($"pop {inReg}");
            // Going to have to update this bit, probably
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void ReplaceCompareInstruction(Int64 functionAddress, string pattern, TemplateAccessType accessType, byte compare, bool hasREX)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();

            /*
             Check to make sure that we have the right value in the right register.
             Was needed for at least one MOV opcode, so putting it here more as a precaution than
             anything else
             */

            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"push {inReg}");

            instruction_list.Add($"push {inReg}");
            instruction_list.Add($"push {baseReg}");

            instruction_list.Add($"shr {inReg}, 8");
            instruction_list.Add($"cmp {inReg}, 0");
            instruction_list.Add($"jne swap_regs");



            instruction_list.Add($"pop {baseReg}");
            instruction_list.Add($"pop {inReg}");
            instruction_list.Add($"jmp code_start");


            instruction_list.Add($"label swap_regs");
            instruction_list.Add($"pop {inReg}");
            instruction_list.Add($"pop {baseReg}");

            instruction_list.Add($"label code_start");

            if (scale == 0)
            {
                //Value is passed in as-is, need to divide by 4 to get entry ID
                instruction_list.Add($"shr {inReg}, 2");
            }
            instruction_list.Add($"and {inReg}, 255");

            if (inReg != AccessorRegister.rax && outReg != AccessorRegister.rax && baseReg != AccessorRegister.rax)
            {
                pushReg = AccessorRegister.rax;
            }
            else if (inReg != AccessorRegister.rbx && outReg != AccessorRegister.rbx && baseReg != AccessorRegister.rbx)
            {
                pushReg = AccessorRegister.rbx;
            }
            else if (inReg != AccessorRegister.rcx && outReg != AccessorRegister.rcx && baseReg != AccessorRegister.rcx)
            {
                pushReg = AccessorRegister.rcx;
            }
            else
            {
                pushReg = AccessorRegister.rdx;
            }

            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"push rax");
                instruction_list.Add($"mov rax, {inReg}");
            }
            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, 3");
            instruction_list.Add($"push rdx");
            instruction_list.Add($"mov rdx, 0");
            instruction_list.Add($"div rcx");
            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rcx");
            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"mov {inReg}, rax");
                instruction_list.Add($"pop rax");
            }
            GetTemplateEntryAddress(instruction_list, inReg);
            if (type == Instruction.MOVZX || type == Instruction.MOVSX)
            {
                switch (accessType)
                {
                    case TemplateAccessType.ROOM_COUNT:
                        instruction_list.Add($"cmp byte [{inReg}], {compare}");
                        break;
                    case TemplateAccessType.ROOM_COUNT_EX:
                        instruction_list.Add($"cmp byte [{inReg}+1], {compare}");
                        break;
                    case TemplateAccessType.ROOM_ID:
                        instruction_list.Add($"cmp byte [{inReg}+{baseReg}], {compare}");
                        break;
                    default:
                        break;
                }
            }
            else
            {
                throw new InvalidAsmInstructionModValueException(functionAddress, _utils);
            }
            instruction_list.Add($"pop {inReg}");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), hasREX ? functionAddress-1 : functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, hasREX ? _utils.GetPatternLength(pattern)+2 : _utils.GetPatternLength(pattern)+1).Activate());
        }

        private void ReplaceAddressSetupA(Int64 functionAddress, string pattern, bool hasREX)
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

            instruction_list.Add($"push {inReg}");


            if (inReg != AccessorRegister.rax && outReg != AccessorRegister.rax && baseReg != AccessorRegister.rax)
            {
                pushReg = AccessorRegister.rax;
            }
            else if (inReg != AccessorRegister.rbx && outReg != AccessorRegister.rbx && baseReg != AccessorRegister.rbx)
            {
                pushReg = AccessorRegister.rbx;
            }
            else if (inReg != AccessorRegister.rcx && outReg != AccessorRegister.rcx && baseReg != AccessorRegister.rcx)
            {
                pushReg = AccessorRegister.rcx;
            }
            else
            {
                pushReg = AccessorRegister.rdx;
            }

            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"push rax");
                instruction_list.Add($"mov rax, {inReg}");
            }
            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, 3");
            instruction_list.Add($"push rdx");
            instruction_list.Add($"mov rdx, 0");
            instruction_list.Add($"div rcx");
            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rcx");
            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"mov {inReg}, rax");
                instruction_list.Add($"pop rax");
            }

            GetTemplateEntryAddress(instruction_list, inReg);
            instruction_list.Add($"xor {outReg}, {outReg}");
            instruction_list.Add($"mov {outReg}, {inReg}");
            instruction_list.Add($"add {outReg}, 2");
            if (inReg == outReg)
            {
                instruction_list.Add($"add rsp, 8");
            }
            else
            {
                instruction_list.Add($"pop {inReg}");
            }
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), hasREX ? functionAddress-1 : functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, hasREX ? _utils.GetPatternLength(pattern)+1 : _utils.GetPatternLength(pattern)).Activate());
        }

        private void ReplaceAddressSetupB(Int64 functionAddress, string pattern, bool hasREX)
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

            instruction_list.Add($"and {inReg}, 255");

            if (inReg != AccessorRegister.rax && outReg != AccessorRegister.rax && baseReg != AccessorRegister.rax)
            {
                pushReg = AccessorRegister.rax;
            }
            else if (inReg != AccessorRegister.rbx && outReg != AccessorRegister.rbx && baseReg != AccessorRegister.rbx)
            {
                pushReg = AccessorRegister.rbx;
            }
            else if (inReg != AccessorRegister.rcx && outReg != AccessorRegister.rcx && baseReg != AccessorRegister.rcx)
            {
                pushReg = AccessorRegister.rcx;
            }
            else
            {
                pushReg = AccessorRegister.rdx;
            }

            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"push rax");
                instruction_list.Add($"mov rax, {inReg}");
            }
            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, 3");
            instruction_list.Add($"push rdx");
            instruction_list.Add($"mov rdx, 0");
            instruction_list.Add($"div rcx");
            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rcx");
            if (inReg != AccessorRegister.rax)
            {
                instruction_list.Add($"mov {inReg}, rax");
                instruction_list.Add($"pop rax");
            }

            GetTemplateEntryAddress(instruction_list, inReg);
            instruction_list.Add($"sub {outReg}, 8");
            instruction_list.Add($"mov [{outReg}], {inReg}");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        // Used for mov
        private void ReplaceTemplateTypeAccessA(Int64 functionAddress, string pattern, bool hasREX)
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

            // basereg
            instruction_list.Add($"mov {outReg}, [{baseReg} + {_templateTypeTable}]");
            instruction_list.Add($"and {outReg}, 0xFF");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), hasREX ? functionAddress-1 : functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, hasREX ? _utils.GetPatternLength(pattern)+1 : _utils.GetPatternLength(pattern)).Activate());
        }

        // Used for lea
        private void ReplaceTemplateTypeAccessB(Int64 functionAddress, string pattern)
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

            //basereg
            instruction_list.Add($"mov {outReg}, {_templateTypeTable}");

            instruction_list.Add($"mov [r9 + 0x108], {outReg}");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        // Essentially part 2 of lea
        private void ReplaceTemplateTypeAccessC(Int64 functionAddress, string pattern)
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

            //basereg
            instruction_list.Add($"xor {inReg}, [{baseReg} - 0x3ADC5A0A]");


            instruction_list.Add($"and {outReg}, 255");

            instruction_list.Add($"push {outReg}");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void OddballReplace1(Int64 functionAddress, string pattern)
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

            instruction_list.Add($"or rsi, [ rbp + 0x3C5DF11C ]");
            instruction_list.Add($"mov dil, [rsi]");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void OddballReplace2(Int64 functionAddress, string pattern)
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

            instruction_list.Add($"lea rsp, [rsp+8]");


            instruction_list.Add($"push rbx");
            //instruction_list.Add($"mov rcx, r8");
            instruction_list.Add($"sub r8, {_templateTable}");
            instruction_list.Add($"and r8, 0xFF");
            instruction_list.Add($"mov rbx, r8");
            instruction_list.Add($"shr r8, 2");

            instruction_list.Add($"push rax");
            instruction_list.Add($"mov rax, r8");
            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, 3");
            instruction_list.Add($"push rdx");
            instruction_list.Add($"mov rdx, 0");
            instruction_list.Add($"div rcx");
            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rcx");
            instruction_list.Add($"mov r8, rax");
            instruction_list.Add($"pop rax");

            // Gotta find out which value is
            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, r8");
            instruction_list.Add($"shl rcx, 2");
            instruction_list.Add($"sub rbx, rcx");
            instruction_list.Add($"pop rcx");

            GetTemplateEntryAddress(instruction_list, AccessorRegister.r8);
            // 
            instruction_list.Add($"add r8, rbx");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"mov r12l, [r8]");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void OddballReplace3(Int64 functionAddress, string pattern)
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

            instruction_list.Add($"mov rcx, r15");

            instruction_list.Add($"sub rax, {_templateTable}");
            instruction_list.Add($"and rax, 0xFF");
            instruction_list.Add($"shr rax, 2");

            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, 3");
            instruction_list.Add($"push rdx");
            instruction_list.Add($"mov rdx, 0");
            instruction_list.Add($"div rcx");
            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rcx");

            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, rax");
            instruction_list.Add($"shl rcx, 2");
            instruction_list.Add($"sub rbx, rcx");
            instruction_list.Add($"pop rcx");


            GetTemplateEntryAddress(instruction_list, AccessorRegister.rax);
            // 
            instruction_list.Add($"pop r14");
            instruction_list.Add($"or r14l, [rax+1]");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void OddballReplace4(Int64 functionAddress, string pattern)
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

            instruction_list.Add($"push rax");
            instruction_list.Add($"and rax, 0xFF");
            instruction_list.Add($"shr rax, 2");

            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, 3");
            instruction_list.Add($"push rdx");
            instruction_list.Add($"mov rdx, 0");
            instruction_list.Add($"div rcx");
            instruction_list.Add($"pop rdx");
            instruction_list.Add($"pop rcx");

            instruction_list.Add($"push rcx");
            instruction_list.Add($"mov rcx, {_currentTemplate}");
            instruction_list.Add($"mov [rcx], rax");
            instruction_list.Add($"pop rcx");
            GetTemplateEntryAddress(instruction_list, AccessorRegister.rax);
            instruction_list.Add($"mov [r9 + 0x110], rax");
            instruction_list.Add($"pop rax");
            // instruction_list.Add($"lea rsp, [rsp+0x8]");


            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }


        private void OddballReplace5(Int64 functionAddress, string pattern)
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

            /*
             Need to figure a way to determine to use roomcount or roomcountex
             */
            instruction_list.Add($"mov r14, {_currentTemplate}");
            instruction_list.Add($"mov r14, [r14]");
            GetTemplateEntryAddress(instruction_list, AccessorRegister.r14);
            // With this, it will always pick RoomCount
            // Count is a bit strange here, it's one higher than it should be
            instruction_list.Add($"movzx r14, byte [r14]");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.ExecuteFirst, _utils.GetPatternLength(pattern)).Activate());
        }

        private void OddballReplace6(Int64 functionAddress, string pattern)
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

            instruction_list.Add($"mov rax, {_currentTemplate}");
            instruction_list.Add($"mov rax, [rax]");
            GetTemplateEntryAddress(instruction_list, AccessorRegister.rax);
            // With this, it will always pick RoomCountEx
            instruction_list.Add($"add rax, 2");
            instruction_list.Add($"mov [r9 + 0xe8], rax");
            instruction_list.Add($"push rax");
            instruction_list.Add($"not rax");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void GetTemplateEntryAddress(List<string> functionList, AccessorRegister entryNum)
        {
            // entryNum is presumed to be the literal entry number (0, 1, or 2 in vanilla)
            AccessorRegister pushReg2;
            AccessorRegister pushReg;
            if (entryNum == AccessorRegister.rax)
            {
                pushReg = AccessorRegister.rbx;
            }
            else
            {
                pushReg = AccessorRegister.rax;
            }

            if (entryNum == AccessorRegister.rcx)
            {
                pushReg2 = AccessorRegister.rdx;
            }
            else
            {
                pushReg2 = AccessorRegister.rcx;
            }

            functionList.Add($"push {pushReg}");
            functionList.Add($"push {pushReg2}");


            functionList.Add($"mov {pushReg2}, {_templateLookupTable}");
            functionList.Add($"mov {pushReg}, 0");

            functionList.Add($"label add_loop");
            functionList.Add($"cmp {pushReg}, {entryNum}");
            functionList.Add($"je loop_end");
            functionList.Add($"add {pushReg}, 1");
            functionList.Add($"add {pushReg2}, 8");

            functionList.Add($"jmp add_loop");


            functionList.Add($"label loop_start");

            functionList.Add($"label loop_end");
            functionList.Add($"mov {entryNum}, [{pushReg2}]");

            functionList.Add($"pop {pushReg2}");
            functionList.Add($"pop {pushReg}");
        }
    }
}