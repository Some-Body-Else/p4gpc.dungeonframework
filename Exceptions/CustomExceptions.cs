using Reloaded.Mod.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace p4gpc.dungeonframework.Exceptions
{
    public class CustomException : Exception
    {
        public CustomException(string msg, Utilities _utils) : base($"{msg}")
        {
            _utils.LogThrownException(msg);
        }
    }
    public class ToBeNamedException : CustomException
    {
        public ToBeNamedException(Utilities _utils) : base($"This is definitely a problem, but I don't know what to call it yet :/", _utils)
        {

        }
    }

    public class InvalidSAddressValueLoadException : CustomException
    {
        public InvalidSAddressValueLoadException(Utilities _utils, Int64 address) : base($"$Failed to load value from function at address {address.ToString("X8")}", _utils)
        {

        }
    
    }

    public class InvalidJsonPathException : CustomException
    {
        public InvalidJsonPathException(string json, Utilities _utils) : base($"Attempt to load {json} from Persona 4 Golden mod folder failed, defaulting to vanilla {json}", _utils)
        {
        }
    }
    public class InvalidAsmInstructionAccessTypeException : CustomException
    {
        public InvalidAsmInstructionAccessTypeException(Int64 address, Utilities _utils) : base($"Instruction at address {address.ToString("X8")} attempting to access invalid template address, contact mod developer.", _utils)
        {
        }
    }
    public class InvalidAsmInstructionModValueException : CustomException
    {
        public InvalidAsmInstructionModValueException(Int64 address, Utilities _utils) : base($"Instruction at address {address.ToString("X8")} contains invalid mod value, contact mod developer.", _utils)
        {
        }
    }
    public class InvalidAsmInstructionModAccessCombinationException: CustomException
    {
        public InvalidAsmInstructionModAccessCombinationException(Int64 address, Utilities _utils) : base($"Instruction at address {address.ToString("X8")} contains unexpected mod/address combination, contact mod developer.", _utils)
        {
        }
    }
    public class InvalidAsmInstructionRegisterReferenceException : CustomException
    {
        public InvalidAsmInstructionRegisterReferenceException(Int64 address, Utilities _utils) : base($"Instruction at address {address.ToString("X8")} contains unexpected register reference, contact mod developer.", _utils)
        {
        }
    }
    public class InvalidAsmInstructionTypeException : CustomException
    {
        public InvalidAsmInstructionTypeException(Int64 address, Utilities _utils) : base($"Instruction at address {address.ToString("X8")} is unexpected, contact mod developer.", _utils)
        {
        }
    }
    public class InvalidTemplateAccessorTypeException : CustomException
    {
        public InvalidTemplateAccessorTypeException(Int64 address, Utilities _utils) : base($"Instruction at address {address.ToString("X8")} accessing unexpected address, contact mod developer.", _utils)
        {
        }
    }
    public class InvalidTemplateAccessorAddressException : CustomException
    {
        public InvalidTemplateAccessorAddressException(Int64 address, Utilities _utils) : base($"TemplateAccessor given invalid address: {address.ToString("X8")}", _utils)
        {
        }
    }
    public class InvalidRoomIndexException : CustomException
    {
        public InvalidRoomIndexException(int index, Utilities _utils) : base($"RoomAccessor given invalid index: {index}", _utils)
        {
        }
    }
    public class InvalidMinimapIdException : CustomException
    {
        public InvalidMinimapIdException(int ID, Utilities _utils) : base($"MinimapAccessor given invalid room ID: {ID}", _utils)
        {
        }
    }
    public class MinimapIdMismatchException : CustomException
    {
        public MinimapIdMismatchException(int gameID, int foundID, Utilities _utils) : base($"MinimapAccessor attempted to find minimap with ID {gameID}, found {foundID} instead", _utils)
        {
        }
    }
    public class MissingMinimapImageException : CustomException
    {
        public MissingMinimapImageException(string textureName, Utilities _utils) : base($"MinimapAccessor attempted to find minimap texture {textureName} in smap.bin and failed", _utils)
        {
        }
    }
    public class MinimapInfoIdOutOfRangeException : CustomException
    {
        public MinimapInfoIdOutOfRangeException(int id, Utilities _utils) : base($"MinimapAccessor given ID {id}, but could not find corresponding tilemap name", _utils)
        {
        }
    }
    public class MinimapStringOutOfRangeException : CustomException
    {
        public MinimapStringOutOfRangeException(string textureName, Utilities _utils) : base($"MinimapAccessor could not find information for minimap texture {textureName}", _utils)
        {
        }
    }
    public class RoomCompareInvalidIndexException: CustomException 
    {
        public RoomCompareInvalidIndexException(int index, string register, Utilities _utils) : base($"RoomCompareAccessor given invalid index {index} from register {register}", _utils)
        {
        }
    }
}
