/*
Copyright 2012 HighVoltz

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.Diagnostics;

namespace FishBot3._3._5a
{
    public static class Utility
    {
        public static readonly Random Rand = new Random();

        public static bool Is64BitProcess(Process proc)
        {
            bool retVal;
            return Environment.Is64BitOperatingSystem &&
                   !(NativeMethods.IsWow64Process(proc.Handle, out retVal) && retVal);
        }

        // returns base offset for main module
        public static uint BaseOffset(this Process proc)
        {
            return (uint)proc.MainModule.BaseAddress.ToInt32();
        }

        public static string VersionString(this Process proc)
        {
            return proc.MainModule.FileVersionInfo.FileVersion;
        }
    }
}