using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishBot3._3._5a
{
    public static class Offsets
    {
        public static IntPtr g_clientConnection = new IntPtr(0xC79CE0);                 // 3.3.5a 
        public static IntPtr s_curMgrOffset = new IntPtr(0x2ED0);                       // 3.3.5a 
        public static IntPtr FirstObjectOffset = new IntPtr(0xAC);                      // 3.3.5a
        public static IntPtr NextObjectOffset = new IntPtr(0x3C);                       // 3.3.5a

        public static IntPtr PlayerName = new IntPtr(0xC79D18);                         // 3.3.5a

        public static IntPtr TargetGUID = new IntPtr(0xBD07B0);

        public static IntPtr ClntObjMgrGetActivePlayerObj = new IntPtr(0x004038F0);     // 3.3.5a 
        public static IntPtr FrameScript__Execute = new IntPtr(0x819210);               // 3.3.5a 
        public static IntPtr FrameScript__GetLocalizedText = new IntPtr(0x007225E0);    // 3.3.5a
    }
}
