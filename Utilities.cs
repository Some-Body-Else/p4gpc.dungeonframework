using p4gpc.dungeonloader.Configuration;

using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
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

        /// <summary>
        /// Scans the executable to find the first instance of the given pattern. Function name is for potential exceptions.
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="funcName"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Scans the executable to find X instances of the given pattern, starting from the beginning of the executable,
        /// where X is defined as the funcCount parameter. Currently has no use, but keeping it around for now in case it pops up.
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="funcName"></param>
        /// <param name="funcCount"></param>
        /// <returns></returns>
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
        /// Scans the executable for every instance of the given pattern
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="funcName"></param>
        /// <returns></returns>
        public unsafe List<long> SigScan_FindAll(string pattern, string funcName)
        {
            using var currentProc = Process.GetCurrentProcess();
            var baseAddress = currentProc.MainModule.BaseAddress;
            var exeSize = currentProc.MainModule.ModuleMemorySize;
            using var sigscanner = new Scanner((byte*)baseAddress, exeSize);
            int funcOffset = 0;
            long funcAddress = 0;
            List<long> return_list = new List<long>();
            while(true)
            {
                try
                {
                    var scanResult = sigscanner.FindPattern(pattern, funcOffset+1);
                    funcOffset = scanResult.Offset;
                    if (funcOffset < 0)
                    {
                        if (return_list.Count == 0)
                            throw new Exception($"Unable to find byte pattern {pattern}");
                        break;
                    };
                    funcAddress = (long)baseAddress+funcOffset;
                    return_list.Add(funcAddress);
                }
                catch (Exception ex)
                {
                    LogError($"Error when searching for address of {funcName}", ex);
                    break;
                }
            }
            return return_list;
        }

        /// <summary>
        /// Returns the length of a byte pattern, ignores whitespace.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public int GetPatternLength(string pattern)
        {
            pattern = pattern.Replace(" ", "");
            return pattern.Length/2;
        }
    }
}
