using p4gpc.dungeonloader.Configuration;

using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace p4gpc.dungeonloader
{
    /// <summary>
    /// The entirety of this file is stolen from AnimatedSwine37, specifically their XP Share mod for P4G.
    /// Link here: https://github.com/AnimatedSwine37/Persona-4-Golden-Xp-Share/blob/main/p4gpc.xpshare/XpShare.cs
    /// Names are slightly different because it was copied by hand to get a better understanding as to how it should be used.
    /// </summary>
    public class Utilities
    {
        public Config Configuration;
        private ILogger _logger;
        private int _processBaseAddress;

        public Utilities(Config configuration, ILogger logger, int processBaseAddress)
        {
            Configuration = configuration;
            _logger = logger;
            _processBaseAddress = processBaseAddress;
        }

        public void Log(string message)
        {
            _logger.WriteLine($"[DungeonLoader] {message}");
        }

        public void LogDebug(string message)
        {
            //Should give a proper debug condition later
            if (1 == 1)
            {
                _logger.WriteLine($"[DungeonLoader] {message}");
            }
        }

        public void LogError(string message, Exception e)
        {
            _logger.WriteLine($"[DungeonLoader] {message}: {e.Message}", System.Drawing.Color.Red);
        }

        public unsafe long SigScan(string pattern, string funcName)
        {
            try
            {
                using var currentProc = Process.GetCurrentProcess();
                var baseAddress = currentProc.MainModule.BaseAddress;
                var exeSize = currentProc.MainModule.ModuleMemorySize;
                using var sigscanner = new Scanner((byte*)baseAddress, exeSize);
                long funcAddress = sigscanner.FindPattern(pattern).Offset;
                if (funcAddress < 0) throw new Exception($"Unable to find byte pattern {pattern}");
                funcAddress += _processBaseAddress;
                return funcAddress;
            }
            catch (Exception ex)
            {
                LogError($"Error when searching for address of {funcName}", ex);
                return -1;
            }
        }

        public unsafe List<long> SigScan_FindCount(string pattern, string funcName, int funcCount)
        {
            using var currentProc = Process.GetCurrentProcess();
            var baseAddress = currentProc.MainModule.BaseAddress;
            var exeSize = currentProc.MainModule.ModuleMemorySize;
            List<long> return_list = new List<long>();
            for (int i = 0; i < funcCount; i++)
            {
                try
                {
                    using var sigscanner = new Scanner((byte*)baseAddress, exeSize);
                    long funcAddress = sigscanner.FindPattern(pattern).Offset;
                    if (funcAddress < 0)
                    {
                        if (return_list.Count == 0)
                            throw new Exception($"Unable to find byte pattern {pattern}");
                        break;
                    };
                    funcAddress += (long)baseAddress;
                    return_list.Add(funcAddress);
                    baseAddress = (IntPtr)funcAddress + 1;
                }
                catch (Exception ex)
                {
                    LogError($"Error when searching for address of {funcName}", ex);
                }
            }
            return return_list;
        }

        /// <summary>
        /// Our original idea for scanning the entire file. Runs into an issue when it runs out of stuff its supposed to
        /// scan for and ends up throwing an AccessViolationException. This requires some witchcraft to work around that
        /// probably should be implemented, but I can't be bothered at the moment, so this is vestigial for the moment.
        /// Keeping it around in case we decide to revisit the idea.
        /// 
        /// As note for future, AccessViolationException is not treated like other exceptions due to its relation to
        /// memory corruption. In .NET versions past 4, the error will skip past any catch statements and present itself to
        /// the IDE directly. There is a way to re-enable the old behavior, allowing AccessViolationException to be caught like
        /// any other exception, but getting that to work is the issue.
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="funcName"></param>
        /// <returns></returns>
        public unsafe List<long> SigScan_FindAll(string pattern, string funcName)
        {
            using var currentProc = Process.GetCurrentProcess();
            var baseAddress = currentProc.MainModule.BaseAddress;
            var exeSize = currentProc.MainModule.ModuleMemorySize;
            List<long> return_list = new List<long>();
            while(true)
            {
                try
                {
                    using var sigscanner = new Scanner((byte*)baseAddress, exeSize);
                    long funcAddress = 0;
                    try
                    {
                        var scanResult = sigscanner.FindPattern(pattern);
                        funcAddress = scanResult.Offset;
                    }
                    catch (AccessViolationException ex)
                    {
                        Log("Scanner reached AccessViolationException, presumed to be end of scanner");
                        break;
                    }
                    if (funcAddress < 0) {
                        if (return_list.Count == 0)
                            throw new Exception($"Unable to find byte pattern {pattern}");
                        break;
                    };
                    funcAddress += (long)baseAddress;
                    return_list.Add(funcAddress);
                    baseAddress = (IntPtr)funcAddress+1;
                }
                catch (Exception ex)
                {
                    LogError($"Error when searching for address of {funcName}", ex);
                }
            }
            return return_list;
        }
    }
}
