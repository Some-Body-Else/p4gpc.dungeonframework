using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonloader.Exceptions
{
    public class ToBeNamedExcpetion : Exception
    {
        public ToBeNamedExcpetion() : base($"This is definitely a problem, but I don't know what to call it yet :/")
        {
        }
    }
    public class InvalidAsmInstructionAccessTypeException : Exception
    {
        public InvalidAsmInstructionAccessTypeException(int address) : base($"Instruction at address {address.ToString("X8")} attempting to access invalid template address, contact mod developer.")
        {
        }
    }
    public class InvalidAsmInstructionModValueException : Exception
    {
        public InvalidAsmInstructionModValueException(int address) : base($"Instruction at address {address.ToString("X8")} contains invalid mod value, contact mod developer.")
        {
        }
    }
    public class InvalidAsmInstructionModAccessCombinationException: Exception
    {
        public InvalidAsmInstructionModAccessCombinationException(int address) : base($"Instruction at address {address.ToString("X8")} contains unexpected mod/address combination, contact mod developer.")
        {
        }
    }
    public class InvalidAsmInstructionRegisterReferenceException : Exception
    {
        public InvalidAsmInstructionRegisterReferenceException(int address) : base($"Instruction at address {address.ToString("X8")} contains unexpected register reference, contact mod developer.")
        {
        }
    }
    public class InvalidAsmInstructionTypeException : Exception
    {
        public InvalidAsmInstructionTypeException(int address) : base($"Instruction at address {address.ToString("X8")} is unexpected, contact mod developer.")
        {
        }
    }
    public class InvalidTemplateAccessorTypeException : Exception
    {
        public InvalidTemplateAccessorTypeException(int address) : base($"Instruction at address {address.ToString("X8")} accessing unexpected address, contact mod developer.")
        {
        }
    }
    public class InvalidTemplateAccessorAddressException : Exception
    {
        public InvalidTemplateAccessorAddressException(int address) : base($"TemplateAccessor given invalid address: {address.ToString("X8")}")
        {
        }
    }
}
