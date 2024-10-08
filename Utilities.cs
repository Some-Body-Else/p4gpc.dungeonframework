﻿using p4gpc.dungeonframework.Configuration;

using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sigscan.Definitions;
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
using static p4gpc.dungeonframework.Configuration.Config;

namespace p4gpc.dungeonframework
{
    /// <summary>
    /// The entirety of this file is stolen from AnimatedSwine37, specifically their XP Share mod for P4G
    /// <br></br>
    /// Link here: https://github.com/AnimatedSwine37/Persona-4-Golden-Xp-Share/blob/main/p4gpc.xpshare/XpShare.cs
    /// <br></br>
    /// Names are slightly different because it was copied by hand to get a better understanding as to how it should be used.
    /// Possibly subject to change as the project moves forward.
    /// </summary>
    public class Utilities
    {
        private Dictionary<int, System.Drawing.Color> debugLevelDict = new Dictionary<int, System.Drawing.Color>()
        {
            { 0, System.Drawing.Color.AliceBlue },
            { 1, System.Drawing.Color.Gold },
            { 2, System.Drawing.Color.BlueViolet },
            { 3, System.Drawing.Color.Cornsilk },
            { 4, System.Drawing.Color.DarkGoldenrod },
            { 5, System.Drawing.Color.BurlyWood },
        };

        public Config Configuration;
        private ILogger _logger;
        private Int64 _processBaseAddress;

        public Utilities(Config configuration, ILogger logger, Int64 processBaseAddress)
        {
            Configuration = configuration;
            _logger = logger;
            _processBaseAddress = processBaseAddress;
        }

        public void Log(string message)
        {
            _logger.WriteLine($"[DungeonFramework] {message}");
        }

        public void LogDebug(string message, DebugLevels debugLevel)
        {
            if ((byte)Configuration.logDebug >= (byte)debugLevel)
            {
                _logger.WriteLine($"[DungeonFramework] {message}", debugLevelDict[(int)debugLevel]);
            }
        }

        public void LogWarning(string message)
        {
            _logger.WriteLine($"[DungeonFramework] Warning: {message}", System.Drawing.Color.Yellow);
        }

        public void LogError(string message)
        {
            _logger.WriteLine($"[DungeonFramework] Error: {message}", System.Drawing.Color.Red);
        }

        public void LogThrownException(string message)
        {
            _logger.WriteLine($"[DungeonFramework] {message}", System.Drawing.Color.DarkRed);
        }

        /// <summary>
        /// Scans the executable to find the first instance of the given pattern. Function name is for potential exceptions.
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="funcName"></param>
        /// <returns></returns>
        public unsafe Int64 SigScan(string pattern, string funcName)
        {
            try
            {
                using var currentProc = Process.GetCurrentProcess();
                var baseAddress = currentProc.MainModule.BaseAddress;
                var exeSize = currentProc.MainModule.ModuleMemorySize;
                using var sigscanner = new Scanner((byte*)baseAddress, exeSize);
                Int64 funcAddress = (Int64) sigscanner.FindPattern(pattern).Offset;
                if (funcAddress < 0) throw new Exception($"Unable to find byte pattern {pattern}");
                funcAddress += _processBaseAddress;
                return funcAddress;
            }
            catch (Exception ex)
            {
                LogError($"Error when searching for address of {funcName} : {ex.Message}");
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
        public unsafe List<Int64> SigScan_FindCount(string pattern, string funcName, int funcCount)
        {
            using var currentProc = Process.GetCurrentProcess();
            var baseAddress = currentProc.MainModule.BaseAddress;
            var exeSize = currentProc.MainModule.ModuleMemorySize;
            List<Int64> return_list = new List<Int64>();
            for (int i = 0; i < funcCount; i++)
            {
                try
                {
                    using var sigscanner = new Scanner((byte*)baseAddress, exeSize);
                    Int64 funcAddress = (Int64) sigscanner.FindPattern(pattern).Offset;
                    if (funcAddress < 0)
                    {
                        if (return_list.Count == 0)
                            throw new Exception($"Unable to find byte pattern {pattern}");
                        break;
                    };
                    funcAddress += (Int64)baseAddress;
                    return_list.Add(funcAddress);
                    baseAddress = (IntPtr)funcAddress + 1;
                }
                catch (Exception ex)
                {
                    LogError($"Error when searching for address of {funcName} : {ex.Message}");
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
        public unsafe List<Int64> SigScan_FindAll(string pattern, string funcName)
        {
            using var currentProc = Process.GetCurrentProcess();
            var baseAddress = currentProc.MainModule.BaseAddress;
            var exeSize = currentProc.MainModule.ModuleMemorySize;
            using var sigscanner = new Scanner((byte*)baseAddress, exeSize);
            int funcOffset = 0;
            Int64 funcAddress = 0;
            List<Int64> return_list = new List<Int64>();
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
                    funcAddress = ((Int64)baseAddress) + ((Int64)funcOffset);
                    return_list.Add(funcAddress);
                }
                catch (Exception ex)
                {
                    LogError($"Error when searching for address of {funcName} : {ex.Message}");
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

        /// <summary>
        /// Returns a value that adds the base address of the P4G process to the value.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public Int64 AccountForBaseAddress(Int64 address)
        {
            return address + _processBaseAddress;
        }

        /// <summary>
        /// Returns a value that removes the base address of the P4G process from the value.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public Int64 StripBaseAddress(Int64 address)
        {
            return address - _processBaseAddress;
        }
    }
}
