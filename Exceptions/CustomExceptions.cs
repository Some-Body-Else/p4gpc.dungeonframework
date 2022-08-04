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
    public class InvalidTemplateAccessorAddressException : Exception
    {
        public InvalidTemplateAccessorAddressException(int address) : base($"TemplateAccessor given invalid address: {address.ToString("X8")}")
        {
        }
    }
}
