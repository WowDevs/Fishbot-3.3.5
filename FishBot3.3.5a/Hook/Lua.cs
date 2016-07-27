using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishBot3._3._5a
{
    public class Lua
    {
        private readonly Hook _wowHook;
        public Lua(Hook wowHook)
        {
            _wowHook = wowHook;
        }

        public void DoString(string command)
        {
            if (_wowHook.Installed)
            {
                // Allocate memory
                IntPtr doStringArgCodecave = _wowHook.Memory.AllocateMemory(Encoding.UTF8.GetBytes(command).Length + 1);
                // Write value:
                _wowHook.Memory.WriteBytes(doStringArgCodecave, Encoding.UTF8.GetBytes(command));

                // Write the asm stuff for Lua_DoString
                var asm = new[] 
                {
                    "mov eax, " + doStringArgCodecave,
                    "push 0",
                    "push eax",
                    "push eax",
                    //"mov eax, " + ( (uint) Offsets.FrameScript__Execute + _wowHook.Process.BaseOffset()) , // Lua_DoString
                    "mov eax, " + ( (uint) Offsets.FrameScript__Execute ), // Lua_DoString
                    "call eax",
                    "add esp, 0xC",
                    "retn"
                };
                // Inject
                _wowHook.InjectAndExecute(asm);
                // Free memory allocated 
                _wowHook.Memory.FreeMemory(doStringArgCodecave);
            }
        }

        internal string GetLocalizedText(string localVar)
        {
            if (_wowHook.Installed)
            {
                IntPtr Lua_GetLocalizedText_Space = _wowHook.Memory.AllocateMemory(Encoding.UTF8.GetBytes(localVar).Length + 1);

                // If you building for MoP or higher then you need to use 
                // ClntObjMgrGetActivePlayerObj = Memory.MainModule.BaseAddress + new IntPtr(0xADDRESS);   
                // FrameScript__GetLocalizedText = Memory.MainModule.BaseAddress + new IntPtr(0xADDRESS);   

                _wowHook.Memory.Write<byte>(Lua_GetLocalizedText_Space, Encoding.UTF8.GetBytes(localVar), false);

                String[] asm = new String[] 
                {
                    "call " + (uint) Offsets.ClntObjMgrGetActivePlayerObj,
                    "mov ecx, eax",
                    "push -1",
                    "mov edx, " + Lua_GetLocalizedText_Space + "",
                    "push edx",
                    //"call " + ((uint) Offsets.FrameScript__GetLocalizedText + _wowHook.Process.BaseOffset()) ,
                    "call " + ((uint) Offsets.FrameScript__GetLocalizedText) ,
                    "retn",
                };

                string sResult = Encoding.UTF8.GetString(_wowHook.InjectAndExecute(asm));

                // Free memory allocated 
                _wowHook.Memory.FreeMemory(Lua_GetLocalizedText_Space);
                return sResult;
            }
            return "WoW Hook not installed";
        }

        public void SendTextMessage(string message)
        {
            //DoString(string.Format("SendChatMessage(\"" + message + "\", \"EMOTE\", nil, \"General\")"));

            DoString("RunMacroText('/me " + message + "')");
        }

        public void CastSpellByName(string spell)
        {
            var debuff = 0.0; // Spell must be cast

            // Check if the spell must be cast because its debuff has worn off
            if (spell == "Icy Touch")
            {
                debuff = DebuffRemainingTime("Frost Fever");
            }

            if (debuff > 0.5)
                return;

            // Check if spell is ready, if not skip this spell
            DoString("start, duration, enabled = GetSpellCooldown('" + spell + "')");
            var result = GetLocalizedText("duration");

            if (result != "0")
                return;
                       
            DoString(string.Format("CastSpellByName('{0}')", spell));
            SendTextMessage("Casting: " + spell);
        }

        public double DebuffRemainingTime(string debuffName)
        {
            var luaStr = string.Format("name, rank, icon, count, debuffType, duration, expirationTime, unitCaster, isStealable, shouldConsolidate, spellId = UnitAura('target','{0}',nil,'HARMFUL')", debuffName);
            DoString(luaStr);
            var result = GetLocalizedText("expirationTime");

            if (result == "")
                return 0;

            DoString("time = GetTime()");
            var currentTime = GetLocalizedText("time");

            double timeInSeconds = double.Parse(result) - double.Parse(currentTime);

            if (timeInSeconds < 0)
                return 0;

            return timeInSeconds;
        }

        public void ctm(float x, float y, float z, ulong guid, int action, float precision, IntPtr playerBaseAddress)
        {
            // Offset:
            uint CGPlayer_C__ClickToMove = 0x727400;

            // Allocate Memory:
            IntPtr GetNameVMT_Codecave = _wowHook.Memory.AllocateMemory(0x3332);
            IntPtr Pos_Codecave = _wowHook.Memory.AllocateMemory(0x4 * 3);
            IntPtr GUID_Codecave = _wowHook.Memory.AllocateMemory(0x8);
            IntPtr Angle_Codecave = _wowHook.Memory.AllocateMemory(0x4);

            // Write value:
            _wowHook.Memory.Write<UInt64>(GUID_Codecave, guid);
            _wowHook.Memory.Write<float>(Angle_Codecave, precision);
            _wowHook.Memory.Write<float>(Pos_Codecave, x);
            _wowHook.Memory.Write<float>(Pos_Codecave + 0x4, y);
            _wowHook.Memory.Write<float>(Pos_Codecave + 0x8, z);

            try
            {
                // BOOL __thiscall CGPlayer_C__ClickToMove(WoWActivePlayer *this, CLICKTOMOVETYPE clickType, WGUID *interactGuid, WOWPOS *clickPos, float precision)
                _wowHook.Memory.Asm.Clear();

                _wowHook.Memory.Asm.AddLine("mov edx, [" + Angle_Codecave + "]");
                _wowHook.Memory.Asm.AddLine("push edx");

                _wowHook.Memory.Asm.AddLine("push " + Pos_Codecave);
                _wowHook.Memory.Asm.AddLine("push " + GUID_Codecave);
                _wowHook.Memory.Asm.AddLine("push " + action);

                _wowHook.Memory.Asm.AddLine("mov ecx, " + playerBaseAddress);
                _wowHook.Memory.Asm.AddLine("call " + CGPlayer_C__ClickToMove);
                _wowHook.Memory.Asm.AddLine("retn");

                _wowHook.Memory.Asm.InjectAndExecute((uint)GetNameVMT_Codecave);
            }
            catch { }

            _wowHook.Memory.FreeMemory(GetNameVMT_Codecave);
            _wowHook.Memory.FreeMemory(Pos_Codecave);
            _wowHook.Memory.FreeMemory(GUID_Codecave);
            _wowHook.Memory.FreeMemory(Angle_Codecave);
        }
    }
}
