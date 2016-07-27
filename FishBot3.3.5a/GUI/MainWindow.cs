using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FishBot3._3._5a
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public partial class MainWindow : Form
    {
        private static Lua lua;

        public static List<ulong> lastBobberGuid;
        private int Caught;
        private IntPtr FirstObj;
        private bool Fish;
        private IntPtr playerbase;
        private Hook wowHook;

        public MainWindow()
        {
            InitializeComponent();
        }

        private static string Exe_Version => File.GetLastWriteTime(System.Reflection.Assembly.GetEntryAssembly().Location).ToString("yyyy.MM.dd");

        private readonly int LocalVersion = int.Parse(Application.ProductVersion.Split('.')[0]);

        private static int GetInventorySlotsLeft
        {
            get
            {
                var totalSlotsLeft = 0;

                for (var i = 0; i <= 4; i++)
                {
                    lua.DoString($"numberOfFreeSlots = GetContainerNumFreeSlots({i})");
                    var numberOfFreeSlots = Convert.ToInt32(lua.GetLocalizedText("numberOfFreeSlots"));
                    totalSlotsLeft += numberOfFreeSlots;
                }

                return totalSlotsLeft;
            }
        }

        public static bool IsFishing
        {
            get
            {
                lua.DoString("spellData = UnitChannelInfo('player')");
                var spell = lua.GetLocalizedText("spellData");

                if (spell.Length == 0)
                    return false;
                return true;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = string.Format(toolStripStatusLabel1.Text, Exe_Version);
            toolStripStatusLabel3.Text = string.Format(toolStripStatusLabel3.Text, LocalVersion);

            Shown += MainWindow_Shown;
            FormClosing += MainWindow_FormClosing;

            Log.Initialize(LogTextBox, this);
        }

        private void MainWindow_Shown(object sender, EventArgs e)
        {
            try
            {
                Log.Write("Attempting to connect to running WoW.exe process...", Color.Black);

                var proc = Process.GetProcessesByName("WoW").FirstOrDefault();

                while (proc == null)
                {
                    var res = MessageBox.Show("Please open WoW, and login, and select your character before using the bot.", "FishBot", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

                    if (res == DialogResult.Cancel)
                    {
                        Application.Exit();
                        return;
                    }

                    proc = Process.GetProcessesByName("WoW").FirstOrDefault();
                }

                wowHook = new Hook(proc);
                wowHook.InstallHook();
                lua = new Lua(wowHook);

                Log.Write("Connected to process with ID = " + proc.Id, Color.Black);

                textBox1.Text = wowHook.Memory.ReadString(Offsets.PlayerName, Encoding.UTF8);

                var s_curMgr = wowHook.Memory.Read<IntPtr>(IntPtr.Add(wowHook.Memory.Read<IntPtr>(Offsets.g_clientConnection), (int)Offsets.s_curMgrOffset));
                var curObj = wowHook.Memory.Read<IntPtr>(IntPtr.Add(s_curMgr, (int)Offsets.FirstObjectOffset));

                FirstObj = curObj;

                //Log("First object located @ memory location 0x" + FirstObj.ToString("X"), Color.Black);

                lua.DoString("zoneData = GetZoneText()");
                // Thread.Sleep(100);
                Log.Write("Zone: " + lua.GetLocalizedText("zoneData"), Color.Black);

                var PlayerBase = new IntPtr(0x00CD87A8);

                playerbase = wowHook.Memory.Read<IntPtr>(
                    wowHook.Memory.Read<IntPtr>(
                        wowHook.Memory.Read<IntPtr>(PlayerBase) + 0x34) + 0x24);

                //var x = wowHook.Memory.Read<float>(playerbase + 0x798);
                //var y = wowHook.Memory.Read<float>(playerbase + 0x79C);
                //var z = wowHook.Memory.Read<float>(playerbase + 0x7A0);

                //lua.ctm(5734.542f, 510.5383f, 647.4522f, 0, 4, 1.0f, playerbase);
                //lua.ctm(x, y, z, 0xF130000FEB01CD9F, 4, 1.0f, playerbase);


                //                public enum ClickToMoveType : uint
                //{
                //    FaceTarget = 0x1,
                //    Face = 0x2,
                //    Stop_ThrowsException = 0x3,
                //    Move = 0x4,
                //    NpcInteract = 0x5,
                //    Loot = 0x6,
                //    ObjInteract = 0x7,
                //    FaceOther = 0x8,
                //    Skin = 0x9,
                //    AttackPosition = 0xA,
                //    AttackGuid = 0xB,
                //    ConstantFace = 0xC,
                //    None = 0xD,

                //    Attack = 0x10,
                //    Idle = 0x13,
                //}

                Log.Write("Click 'Fish' to begin fishing.", Color.Green);
            }
            catch (Exception ex)
            {
                Log.Write(ex.Message, Color.Red);
            }
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            wowHook?.DisposeHooking();
        }

        private void cmdFish_Click(object sender, EventArgs e)
        {
            lastBobberGuid = new List<ulong>();

            cmdStop.Enabled = true;
            cmdFish.Enabled = false;

            SystemSounds.Asterisk.Play();

            Fish = !Fish;

            while (Fish)
            {
                try
                {
                    Application.DoEvents();

                    if (!IsFishing)
                    {
                        Log.Write("Fishing...", Color.Black);
                        lua.CastSpellByName("Fishing");
                        Thread.Sleep(200); // Give the lure a chance to be placed in the water before we start scanning for it
                                           // 200 ms is a good length, most people play with under that latency
                    }

                    var curObj = FirstObj;

                    while (curObj.ToInt64() != 0 && (curObj.ToInt64() & 1) == 0)
                    {
                        var type = wowHook.Memory.Read<int>(curObj + 0x14);
                        var cGUID = wowHook.Memory.Read<ulong>(curObj + 0x30);

                        if (lastBobberGuid.Count == 5) // Only keep the last 5 bobber GUID's (explained below * )
                        {
                            lastBobberGuid.RemoveAt(0);
                            lastBobberGuid.TrimExcess();
                        }

                        if ((type == 5) && !lastBobberGuid.Contains(cGUID)) // 5 = Game Object, and ensure that we not finding a bobber we already clicked
                        {
                            // * wow likes leaving the old bobbers in the game world for a while
                            var objectName = wowHook.Memory.ReadString(
                                wowHook.Memory.Read<IntPtr>(
                                    wowHook.Memory.Read<IntPtr>(curObj + 0x1A4) + 0x90
                                    ),
                                Encoding.UTF8, 50
                                );

                            if (objectName == "Fishing Bobber")
                            {
                                // /run for bag=0,4,1 do for slot=1,36,1 do local name=GetContainerItemLink(bag,slot);if (name and string.find(name,"Partially Rusted File")) then PickupContainerItem(bag,slot);DeleteCursorItem();end;end;end

                                var bobberState = wowHook.Memory.Read<byte>(curObj + 0xBC);

                                if (bobberState == 1) // Fish has been caught
                                {
                                    Caught++;
                                    textBox2.Text = Caught.ToString();

                                    Log.Write("Caught something, hopefully a fish!", Color.Black);

                                    // Check if we have inventory space for the fish, if not delete item "Partially Rusted File" which likes to waste space in bags
                                    //if (GetInventorySlotsLeft == 0)
                                    //{
                                    //    lua.DoString("RunMacroText(\"run for bag=0,4,1 do for slot=1,36,1 do local name=GetContainerItemLink(bag,slot);if (name and string.find(name,\"Partially Rusted File\")) then PickupContainerItem(bag,slot);DeleteCursorItem();end;end;end\")");
                                    //}

                                    var MouseOverGUID = new IntPtr(0x00BD07A0);
                                    wowHook.Memory.Write(MouseOverGUID, cGUID);
                                    Thread.Sleep(100);

                                    lua.DoString("InteractUnit(\'mouseover\')");

                                    lastBobberGuid.Add(cGUID);

                                    Thread.Sleep(200);

                                    if (Caught == 10)
                                    {
                                        Log.Write("Dont worry this only happens @ 10 fish, i thought it would be a nice change", Color.Green);

                                        lua.DoString("SendChatMessage(\"Tired of fishing... taking a nap for a while...\", \"EMOTE\", nil, \"General\")");
                                        lua.DoString("DoEmote('sleep')");
                                        Thread.Sleep(10000);
                                        lua.DoString("JumpOrAscendStart()");
                                        Thread.Sleep(1000);
                                    }

                                    break;
                                }
                            }
                        }

                        var nextObj = wowHook.Memory.Read<IntPtr>(IntPtr.Add(curObj, (int) Offsets.NextObjectOffset));
                        if (nextObj == curObj)
                            break;
                        curObj = nextObj;
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message == "Couldn't read 4 byte(s) from 0x90.")
                    {
                        // Do nothing
                    }
                    else
                    {
                        Log.Write(ex.Message, Color.Red);
                    }
                }
            }
        }

        private void cmdStop_Click(object sender, EventArgs e)
        {
            cmdStop.Enabled = false;
            cmdFish.Enabled = true;

            SystemSounds.Asterisk.Play();
            Fish = false;
        }

        private void cmdRotation_Click(object sender, EventArgs e)
        {
            while (wowHook.Memory.Read<ulong>(Offsets.TargetGUID) != 0)
            {
                lua.CastSpellByName("Icy Touch");
                lua.CastSpellByName("Plague Strike");
                lua.CastSpellByName("Heart Strike");
                lua.CastSpellByName("Death Strike");
                lua.CastSpellByName("Death Coil");

                Thread.Sleep(100);
                Application.DoEvents();
            }
        }

        [SuppressMessage("ReSharper", "FunctionNeverReturns")]
        private void cmdFollow_Click(object sender, EventArgs e)
        {
            while (true)
            {
                try
                {
                    Application.DoEvents();

                    Thread.Sleep(200);

                    var curObj = FirstObj;

                    while (curObj.ToInt64() != 0 && (curObj.ToInt64() & 1) == 0)
                    {
                        var type = wowHook.Memory.Read<int>(curObj + 0x14);
                        var cGUID = wowHook.Memory.Read<ulong>(curObj + 0x30);

                        if (type == 3) // NPC  (4 == player) (5 == Object)
                        {
                            if (cGUID == wowHook.Memory.Read<ulong>(Offsets.TargetGUID))
                            {
                                var myx = wowHook.Memory.Read<float>(playerbase + 0x798);
                                var myy = wowHook.Memory.Read<float>(playerbase + 0x79C);
                                var myz = wowHook.Memory.Read<float>(playerbase + 0x7A0);

                                var px = wowHook.Memory.Read<float>(curObj + 0x798);
                                var py = wowHook.Memory.Read<float>(curObj + 0x79C);
                                var pz = wowHook.Memory.Read<float>(curObj + 0x7A0);

                                if ((Math.Abs(Math.Abs(px) - Math.Abs(myx)) > 5) ||
                                    (Math.Abs(Math.Abs(py) - Math.Abs(myy)) > 5) ||
                                    (Math.Abs(Math.Abs(pz) - Math.Abs(myz)) > 5))
                                {
                                    lua.ctm(px, py, pz, 0, 4, 1.0f, playerbase);

                                    Log.Write("Moving to " + px + ", " + py + ", " + pz);
                                }
                            }
                        }

                        var nextObj = wowHook.Memory.Read<IntPtr>(IntPtr.Add(curObj, (int) Offsets.NextObjectOffset));
                        if (nextObj == curObj)
                            break;
                        curObj = nextObj;
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }
}