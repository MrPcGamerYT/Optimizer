using Guna.UI2.WinForms;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;



namespace Optimizer
{
    public partial class Optimizer : Form
    {
        // ===============================
        // 🔁 ORIGINAL PRIORITY STORAGE
        // ===============================
        private readonly ConcurrentDictionary<int, ProcessPriorityClass> originalPriorities =
            new ConcurrentDictionary<int, ProcessPriorityClass>();

        // Lock for safe restore
        private readonly object restoreLock = new object();

        // ================= MOUSE BACKUP =================
        private string originalMouseSpeed;
        private string originalThreshold1;
        private string originalThreshold2;
        private string originalSensitivity;

        // ===============================
        // 🧠 OPTIMIZER STATE FLAGS
        // ===============================
        private bool normalGameModeRunning = false;
        private bool advancedGameModeRunning = false;
        private bool emulatorModeRunning = false;
        private bool bgAppsModeRunning = false;

        // Master optimizer state
        private volatile bool optimizerActive = false;

        // Prevent optimizer from continuing after exit
        private volatile bool exitRequested = false;


        // ===============================
        // ⏹️ CANCELLATION TOKENS (CRITICAL)
        // ===============================
        private CancellationTokenSource normalGameCTS;
        private CancellationTokenSource advancedGameCTS;
        private CancellationTokenSource emulatorCTS;
        private CancellationTokenSource bgAppsCTS;


        // ===============================
        // 🖥️ UI / PANEL MEMORY
        // ===============================
        private bool rememberLastPanel = true;
        private string lastPanel = "homePnl";


        // ===============================
        // 💾 STORAGE ALERT TRACKING
        // ===============================
        private int lastAlertLevel = -1;


        // ===============================
        // 🖱️ MOUSE OPTIMIZATION STORAGE
        // ===============================
        private uint[] originalMouseParams = new uint[3];
        private bool mouseOptimized = false;


        // ===============================
        // 🎯 OVERALL OPTIMIZATION LEVEL
        // ===============================
        private float currentOverall = 0f;
        private int targetOverall = 0;


        // ===============================
        // 🔔 TRAY ICON SYSTEM
        // ===============================
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        private System.Windows.Forms.Timer trayBlinkTimer;
        private bool trayBlinkState = false;

        private Icon trayIconNormal;
        private Icon trayIconAlert;

        private bool suppressMinimizeEvent = false;
        private bool allowExit = false;


        // ===============================
        // 🌐 NETWORK / PING TIMER
        // ===============================
        private System.Windows.Forms.Timer pingTimer;


        // ===============================
        // 🛠️ ADMIN STATUS LABEL HELPER
        // ===============================
        private void SetAdminStatus(string text, Color color)
        {
            if (lblAdminStatus.InvokeRequired)
            {
                lblAdminStatus.Invoke(new Action(() =>
                {
                    lblAdminStatus.Text = text;
                    lblAdminStatus.ForeColor = color;
                }));
            }
            else
            {
                lblAdminStatus.Text = text;
                lblAdminStatus.ForeColor = color;
            }
        }


        // ===============================
        // 🪟 WINDOWS API IMPORTS
        // ===============================
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr hWnd,
            out int lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(
            uint uiAction,
            uint uiParam,
            object pvParam,
            uint fWinIni);

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uMilliseconds);


        // ===============================
        // 🎯 CONSTANTS
        // ===============================
        private const uint SPI_GETMOUSE = 0x0003;
        private const uint SPI_SETMOUSE = 0x0004;

        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;


        // ===============================
        // 🔒 SAFE EXIT CLEANUP FLAG
        // ===============================
        private bool cleanupCompleted = false;
        private static readonly HashSet<string> ProtectedProcessNames =
    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "system",
    "idle",
    "explorer",
    "dwm",
    "audiodg",
    "svchost",
    "services",
    "wininit",
    "winlogon",
    "lsass",
    "csrss",
    "smss",
    "fontdrvhost"
};

        private string[] gameProcesses =
{
    // 🔹 Emulators
    "HD-Player",        // BlueStacks
    "HD-Frontend",
    "AndroidEmulator",  // LDPlayer
    "dnplayer",
    "LdVBoxHeadless",
    "aow_exe",          // GameLoop
    "Nox",
    "NoxVMHandle",
    "MEmu",
    "MEmuHeadless",

    // 🔹 Riot Games
    "valorant",
    "VALORANT-Win64-Shipping",
    "LeagueClient",
    "League of Legends",

    // 🔹 CS / Valve
    "cs2",
    "csgo",
    "hl2",
    "steam",

    // 🔹 Epic Games
    "FortniteClient",
    "FortniteClient-Win64-Shipping",
    "EpicGamesLauncher",

    // 🔹 Battle Royale
    "TslGame",          // PUBG PC
    "PUBG",
    "pubg",
    "ApexLegends",
    "r5apex",

    // 🔹 Call of Duty
    "cod",
    "codmw",
    "codwarzone",
    "ModernWarfare",
    "BlackOpsColdWar",

    // 🔹 Minecraft
    "javaw",            // Minecraft Java
    "Minecraft.Windows",

    // 🔹 Roblox
    "RobloxPlayerBeta",

    // 🔹 GTA
    "GTA5",
    "PlayGTAV",

    // 🔹 Forza
    "ForzaHorizon5",
    "ForzaHorizon4",

    // 🔹 Racing
    "NFSHeat",
    "NeedForSpeedUnbound",

    // 🔹 Ubisoft
    "RainbowSix",
    "RainbowSix_Vulkan",
    "ACValhalla",

    // 🔹 Battlefield
    "bf1",
    "bfv",
    "bf2042",

    // 🔹 Overwatch
    "Overwatch",
    "Overwatch2",

    // 🔹 Destiny
    "destiny2",

    // 🔹 Escape from Tarkov
    "EscapeFromTarkov",

    // 🔹 The Finals
    "Discovery",

    // 🔹 Rust
    "RustClient",

    // 🔹 Warframe
    "Warframe",

    // 🔹 Genshin Impact
    "GenshinImpact",
    "YuanShen",

    // 🔹 Honkai Star Rail
    "StarRail",

    // 🔹 Generic Unity / Unreal games
    "Unity",
    "UnityCrashHandler64",
    "UnrealEngine",
    "UE4",
    "UE5"
};

        private bool IsProtectedProcess(Process p)
        {
            try
            {
                if (p == null || p.HasExited)
                    return true;

                string name = p.ProcessName.ToLowerInvariant();

                // Core Windows / System processes
                string[] protectedNames =
                {
            "system",
            "idle",
            "explorer",
            "dwm",
            "audiodg",
            "svchost",
            "services",
            "wininit",
            "winlogon",
            "lsass",
            "csrss",
            "smss",
            "fontdrvhost"
        };

                // Protect Optimizer itself
                if (name == Process.GetCurrentProcess().ProcessName.ToLowerInvariant())
                    return true;

                // Protect Windows core
                if (ProtectedProcessNames.Contains(name))
                    return true;

                // Protect active boosted target (game / emulator)
                if (!string.IsNullOrEmpty(activeBoostTarget) &&
                    name.Equals(activeBoostTarget, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Protect emulators
                if (emulatorProcesses.Any(e =>
                    name.Equals(e, StringComparison.OrdinalIgnoreCase)))
                    return true;

                return false;
            }
            catch
            {
                return true; // If unsure → PROTECT
            }
        }



        private int CalculateOverallCondition(int cpu, int ram, int drive)
        {
            // Weighting (realistic)
            int stress =
                (cpu * 40 / 100) +
                (ram * 35 / 100) +
                (drive * 25 / 100);

            int condition = 100 - stress;

            if (condition < 0) condition = 0;
            if (condition > 100) condition = 100;

            return condition;
        }


        // ===============================
        // NORMAL GAME MODE
        // ===============================
        private volatile string activeBoostTarget = null;

        // Known PC game executables
        private readonly string[] gameExecutables =
{
    // FPS / Shooters
    "csgo","cs2","valorant","fortnite","apex","pubg","pubg_lite",
    "overwatch","overwatch2","bf1","bfv","bf2042","bf4","bf3",
    "cod","codmw","codmw2","codmw3","codbo","codbo2","codbo3",
    "codwarzone","halo","halomcc","rainbowsix","r6siege",
    "quakechampions","doom","doom_eternal","insurgency",
    "insurgencysandstorm","paladins","splitgate","cs1.6","cs1.5",
    "quake3","teamfortress2","warface","crossfire","pointblank",
    "payday2","borderlands3","borderlands2","borderlands","bioshockinfinite",
    "bioshock2","bioshock","wolfenstein2","wolfensteintheoldblood",
    "wolfensteinneworder","titanfall2","planetside2","battlefieldonline",
    "bulletstorm","serioussam","suddenattack","specialforces","combatarms",
    "freespace2","quake1","quake2","quake4","unrealtournament","teamfortressclassic",
    "redorchestra2","redorchestra","verdun","hellletloose","risingstorm2",
    "risingstorm","callofdutymodernwarfare","callofduty4","callofduty2",
    "callofduty3","callofdutymw","callofdutymw2","callofdutymw3",
    "callofdutyblackops","callofdutyblackops2","callofdutyblackops3",
    "counterstrikeonline","counterstrikeonline2","doom64","duke3d",
    
    // Open World / RPG
    "gta5","gta_sa","gtaiv","gtav","rdr2","eldenring","cyberpunk2077",
    "witcher3","skyrim","fallout4","fallout76","starfield",
    "assassinscreed","acvalhalla","acodyssey","acorigins",
    "farcry3","farcry4","farcry5","farcry6","watchdogs",
    "watchdogs2","watchdogslegion","hogwartslegacy","dyinglight",
    "dyinglight2","mountandblade","mountandblade2","dragonageinquisition",
    "dragonageorigins","mass_effect","masseffect2","masseffect3",
    "dragonage2","divinity2","divinityoriginalsin2","pillars_of_eternity",
    "baldursgate3","baldursgate2","torment","tormenttidesofnumenera",
    "outerworlds","starwarsknights","falloutnewvegas","fallout3",
    "cyberpunk2077","witcher2","assassinscreedunity","assassinscreed3",
    "dishonored","dishonored2","prey","tombraider","shadowofthetombraider",
    "rage2","rage","metroexodus","metro2033","metrolastlight",
    "control","deathstranding","hitman3","hitman2","hitman","shadowofmordor",
    "shadowofwar","witcher2","mass_effect_andromeda","talesofarise",
    "nierreplicant","nierautomata","kingdomhearts3","kingdomhearts2",
    
    // Sandbox / Survival
    "minecraft","minecraftlauncher","terraria","valheim","rust","ark",
    "arkse","dayz","subnautica","subnautica_zeros","raft","theforest",
    "sonsforest","dontstarve","greenhell","7daystodie","arksurvival",
    "grounded","eco","noita","starbound","factorio","rimworld",
    "oxygen_not_included","satisfactory","astroneer","astroneeralpha",
    "empyrion","stationeers","thelongdark","conanexiles","conanexilesse",
    "projectzomboid","scum","strandeddeep","subsistence","theisland",
    "strandeddeep","theisland","survivalcraft","thehuntercallofthewild",
    
    // Racing / Sports
    "forzahorizon4","forzahorizon5","forzamotorsport","nfs","nfsheat",
    "nfsunbound","nfsmostwanted","assetto_corsa","assettocorsa_competizione",
    "f1_22","f1_23","dirt5","crew2","rocketleague","easportsfc","fifa23",
    "fifa22","pes2021","nba2k23","nba2k22","mlbtheshow22","tonyhawkproskater1",
    "tonyhawkproskater2","tonyhawkproskater3","tonyhawkproskater4","speedrunners",
    "trackmania","motogp22","wrc10","projectcars2","projectcars3","forza4",
    
    // Strategy / MOBA
    "dota2","leagueoflegends","lol","smite","heroesofthestorm",
    "starcraft2","warcraft3","ageofempires2","ageofempires4",
    "civilization6","totalwar","totalwarwarhammer","xcom2",
    "hearthstone","magicarenabattlegrounds","ironharvest","anno1800",
    "companyofheroes2","commandandconquer3","commandandconquer4",
    "supremecommander","warhammer40kdoa","ageofmythology",
    
    // Indie / Other
    "amongus","fallguys","cuphead","hades","deadcells","undertale",
    "stardewvalley","limbo","inside","celeste","slaythespire",
    "factorio","oxygen_not_included","roguelegacy","hyperlightdrifter",
    "bastion","transistor","hollowknight","bindingofisaac",
    "deadbydaylight","hotlinemiami","hotlinemiami2","katana_zero",
    "entertheshinobi","celeste","undertale","limbo","inside",

    // Chunk 2/5 – continued gameExecutables
    // MMO / Online
    "wow","worldofwarcraft","wowclassic","ffxiv","finalfantasyxiv",
    "eso","elderScrollsOnline","runescape","osrs","guildwars2",
    "blackdesertonline","tera","starwarsbattlefront2","everquest2",
    "everquest","rift","lineage2","lineage","wildstar","bns",
    "tera","starwarsoldrepublic","finalfantasyxi","everquestnext",
    "lostark","newworld","albiononline","trove","arknights","paladinsarena",
    
    // Horror / Thriller
    "phasmophobia","residentEvil2","residentEvil3","residentEvil7",
    "amnesia","soma","outlast","outlast2","alienisolation",
    "theevilwithin","theevilwithin2","silentHill2","silentHill3",
    "silentHill4","deadspace","deadspace2","deadspace3","layersoffear",
    "layersoffear2","blairwitch","littlehope","manofmedan","darkpicturesmanofmedan",
    "darkpictureshouseofashes","darkpicturesthedevilinme","amnesiarebirth",
    
    // Action / Adventure
    "control","deathstranding","metroexodus","metro2033","metrolastlight",
    "shadowofmordor","shadowofwar","hitman3","hitman2","hitman",
    "witcher2","witcher3","mass_effect","masseffect2","masseffect3",
    "assassinscreedodyssey","assassinscreedvalhalla","assassinscreedunity",
    "assassinscreed3","dishonored","dishonored2","prey","tombraider",
    "shadowofthetombraider","rage2","rage","control","quantumbreak",
    "alanwake","alanwake2","deathloop","wolfensteinyoungblood",
    
    // Indie / Casual
    "amongus","fallguys","stardewvalley","hades","deadcells","cuphead",
    "hyperlightdrifter","bindingofisaac","roguelegacy","slaythespire",
    "katana_zero","entertheshinobi","celeste","limbo","inside",
    "terraria","factorio","oxygen_not_included","satisfactory","astroneer",
    "astroneeralpha","grounded","subnautica","subnautica_zeros",
    "raft","theforest","sonsforest","dontstarve","greenhell","7daystodie",
    "thelongdark","survivalcraft","projectzomboid","scum","subsistence",
    
    // Racing / Sports
    "forzahorizon4","forzahorizon5","forzamotorsport","nfs","nfsheat",
    "nfsunbound","nfsmostwanted","assetto_corsa","assettocorsa_competizione",
    "f1_22","f1_23","dirt5","crew2","rocketleague","easportsfc","fifa23",
    "fifa22","pes2021","nba2k23","nba2k22","mlbtheshow22","tonyhawkproskater1",
    "tonyhawkproskater2","tonyhawkproskater3","tonyhawkproskater4","speedrunners",
    "trackmania","motogp22","wrc10","projectcars2","projectcars3","forza4",
    
    // Strategy / MOBA / Card
    "dota2","leagueoflegends","lol","smite","heroesofthestorm",
    "starcraft2","warcraft3","ageofempires2","ageofempires4",
    "civilization6","totalwar","totalwarwarhammer","xcom2",
    "hearthstone","magicarenabattlegrounds","ironharvest","anno1800",
    "companyofheroes2","commandandconquer3","commandandconquer4",
    "supremecommander","warhammer40kdoa","ageofmythology","riseofnations",
    "starcraftbroodwar","starcraftremastered","commandandconquerredalert2",
    
    // Classic / Old School
    "quake","quake2","quake3","doom","doom2","doom64","wolfenstein3d",
    "diablo2","diablo2lod","diablo3","warcraft2","warcraft1","starcraft",
    "commandandconquer","commandandconquerra","ageofempires","ageofempires2",
    "ageofempires3","heroesofmightandmagic3","heroesofmightandmagic5","baldursgate",
    "baldursgate2","planescape","icewinddale","icewinddale2","fallout","fallout2",
    
    // Misc / Simulation
    "theSims4","thesims3","thesims2","thesims","cities_skylines","planetcoaster",
    "planetzoo","survivingmars","rimworld","factorio","railwayempire",
    "farmingSimulator22","farmingSimulator19","trucksimulator","flightSimulator",
    "xplane11","xplane12","eliteDangerous","starcitizen","kerbalspaceprogram",
    
    // Total: ~200 games in this chunk
    // Chunk 3/5 – continued gameExecutables
    // FPS / Tactical
    "insurgency2","insurgencysandstorm","swat4","swat3","armedassault2",
    "armedassault","dayofinfamy","hellletloose","verdun","redorchestra",
    "redorchestra2","risingstorm","risingstorm2","battlefield2142","battlefield1942",
    "battlefield2","battlefield3","battlefield4","battlefieldv","battlefield1",
    "callofdutyghosts","callofdutyadvancedwarfare","callofdutyblackops4",
    "medalofhonor","medalofhonoralliedassault","medalofhonorairborne",
    "counterstrikeglobaloffensive","counterstrikesource","counterstrike",
    
    // Open World / RPG
    "gothic2","gothic3","gothic1","risen","risen2","risen3",
    "twoworlds","twoworlds2","fablethelostchap","fable2","fable3",
    "dragonageorigins","dragonage2","dragonageinquisition",
    "divinityoriginalsin","divinityoriginalsin2","divinity2",
    "pillars_of_eternity","pillars_of_eternity2","baldursgate2",
    "baldursgate3","tormenttidesofnumenera","torment","witcher1","witcher2",
    "witcher3","mass_effect","masseffect2","masseffect3","mass_effect_andromeda",
    "outerworlds","fallout1","fallout2","fallout3","falloutnewvegas",
    "fallout4","fallout76","cyberpunk2077","eldenring","starfield",
    
    // Survival / Sandbox
    "minecraft","minecraftlauncher","minecraftdungeons","terraria","starbound",
    "valheim","rust","ark","arkse","dayz","subnautica","subnautica_zeros",
    "raft","theforest","sonsforest","dontstarve","dontstarvetogether","greenhell",
    "grounded","eco","noita","7daystodie","projectzomboid","scum","subsistence",
    "survivalcraft","thelongdark","astroneer","astroneeralpha","satisfactory",
    "stationeers","rimworld","factorio","oxygen_not_included","conanexiles",
    
    // Racing / Sports
    "forzahorizon3","forzahorizon4","forzahorizon5","forzamotorsport7","nfsheat",
    "nfsunbound","nfsmostwanted","nfsunderground","nfsunderground2",
    "assetto_corsa","assettocorsa_competizione","f1_22","f1_23","dirt4",
    "dirt5","crew","crew2","rocketleague","easportsfc","fifa20","fifa21",
    "fifa22","fifa23","pes2020","pes2021","nba2k20","nba2k21","nba2k22","nba2k23",
    "mlbtheshow20","mlbtheshow21","mlbtheshow22","tonyhawkproskater1",
    "tonyhawkproskater2","tonyhawkproskater3","tonyhawkproskater4","trackmania",
    "speedrunners","wrc10","projectcars2","projectcars3","forza4",
    
    // Strategy / MOBA
    "dota2","leagueoflegends","lol","smite","heroesofthestorm","hearthstone",
    "magicarenabattlegrounds","starcraft","starcraft2","warcraft3","ageofempires1",
    "ageofempires2","ageofempires3","ageofempires4","totalwarshogun2","totalwarwarhammer",
    "totalwarwarhammer2","totalwarwarhammer3","xcom","xcom2","ironharvest","anno1800",
    "companyofheroes","companyofheroes2","commandandconquer","commandandconquer3",
    "commandandconquer4","supremecommander","riseofnations","warhammer40kdoa",
    "ageofmythology","starcraftbroodwar","starcraftremastered","commandandconquerra",
    
    // Indie / Casual
    "amongus","fallguys","cuphead","hades","deadcells","undertale",
    "stardewvalley","limbo","inside","celeste","slaythespire","katana_zero",
    "entertheshinobi","hotlinemiami","hotlinemiami2","bastion","transistor",
    "hyperlightdrifter","bindingofisaac","roguelegacy","factorio","oxygen_not_included",
    "satisfactory","astroneer","grounded","subnautica","subnautica_zeros","raft","theforest",
    "sonsforest","dontstarve","greenhell","7daystodie","thelongdark","survivalcraft",
    "projectzomboid","scum","subsistence",
    
    // Horror / Thriller
    "phasmophobia","residentEvil","residentEvil2","residentEvil3","residentEvil4",
    "residentEvil5","residentEvil6","residentEvil7","amnesia","soma","outlast",
    "outlast2","alienisolation","theevilwithin","theevilwithin2","layersoffear",
    "layersoffear2","blairwitch","littlehope","manofmedan","darkpicturesmanofmedan",
    "darkpictureshouseofashes","darkpicturesthedevilinme","amnesiarebirth",
    
    // Total: ~200 games in this chunk
    // Chunk 4/5 – continued gameExecutables
    // FPS / Tactical
    "insurgency","insurgency2","insurgencysandstorm","swat4","swat3",
    "armedassault","armedassault2","dayofinfamy","hellletloose","verdun",
    "redorchestra","redorchestra2","risingstorm","risingstorm2","battlefield2142",
    "battlefield1942","battlefield2","battlefield3","battlefield4","battlefieldv",
    "battlefield1","callofduty4","callofduty2","callofduty3","callofdutymw",
    "callofdutymw2","callofdutymw3","callofdutybo","callofdutybo2","callofdutybo3",
    "callofdutyghosts","callofdutyadvancedwarfare","callofdutyblackops4",
    "medalofhonor","medalofhonorairborne","medalofhonoralliedassault",
    "combatarms","freespace2","doom","doom2","doom64","duke3d","quake1",
    "quake2","quake3","quake4","unrealtournament","teamfortressclassic",
    
    // Open World / RPG
    "gothic1","gothic2","gothic3","risen","risen2","risen3","twoworlds",
    "twoworlds2","fablethelostchap","fable2","fable3","dragonageorigins",
    "dragonage2","dragonageinquisition","divinityoriginalsin","divinityoriginalsin2",
    "pillars_of_eternity","pillars_of_eternity2","baldursgate","baldursgate2",
    "torment","tormenttidesofnumenera","witcher1","witcher2","witcher3",
    "mass_effect","masseffect2","masseffect3","mass_effect_andromeda","outerworlds",
    "fallout1","fallout2","fallout3","falloutnewvegas","fallout4","fallout76",
    "cyberpunk2077","eldenring","starfield","kingdomhearts3","kingdomhearts2",
    
    // Survival / Sandbox
    "minecraft","minecraftlauncher","minecraftdungeons","terraria","starbound",
    "valheim","rust","ark","arkse","dayz","subnautica","subnautica_zeros",
    "raft","theforest","sonsforest","dontstarve","dontstarvetogether","greenhell",
    "grounded","eco","noita","7daystodie","projectzomboid","scum","subsistence",
    "survivalcraft","thelongdark","astroneer","astroneeralpha","satisfactory",
    "stationeers","rimworld","factorio","oxygen_not_included","conanexiles",
    
    // Racing / Sports
    "forzahorizon3","forzahorizon4","forzahorizon5","forzamotorsport7",
    "nfsheat","nfsunbound","nfsmostwanted","nfsunderground","nfsunderground2",
    "assetto_corsa","assettocorsa_competizione","f1_22","f1_23","dirt4",
    "dirt5","crew","crew2","rocketleague","easportsfc","fifa20","fifa21",
    "fifa22","fifa23","pes2020","pes2021","nba2k20","nba2k21","nba2k22","nba2k23",
    "mlbtheshow20","mlbtheshow21","mlbtheshow22","tonyhawkproskater1",
    "tonyhawkproskater2","tonyhawkproskater3","tonyhawkproskater4","trackmania",
    "speedrunners","wrc10","projectcars2","projectcars3","forza4",
    
    // Strategy / MOBA / Card
    "dota2","leagueoflegends","lol","smite","heroesofthestorm","hearthstone",
    "magicarenabattlegrounds","starcraft","starcraft2","warcraft3","ageofempires1",
    "ageofempires2","ageofempires3","ageofempires4","totalwarshogun2",
    "totalwarwarhammer","totalwarwarhammer2","totalwarwarhammer3","xcom",
    "xcom2","ironharvest","anno1800","companyofheroes","companyofheroes2",
    "commandandconquer","commandandconquer3","commandandconquer4",
    "supremecommander","riseofnations","warhammer40kdoa","ageofmythology",
    
    // Indie / Casual
    "amongus","fallguys","cuphead","hades","deadcells","undertale",
    "stardewvalley","limbo","inside","celeste","slaythespire","katana_zero",
    "entertheshinobi","hotlinemiami","hotlinemiami2","bastion","transistor",
    "hyperlightdrifter","bindingofisaac","roguelegacy","factorio",
    "oxygen_not_included","satisfactory","astroneer","grounded","subnautica",
    "subnautica_zeros","raft","theforest","sonsforest","dontstarve","greenhell",
    "7daystodie","thelongdark","survivalcraft","projectzomboid","scum","subsistence",
    
    // Horror / Thriller
    "phasmophobia","residentEvil","residentEvil2","residentEvil3","residentEvil4",
    "residentEvil5","residentEvil6","residentEvil7","amnesia","soma","outlast",
    "outlast2","alienisolation","theevilwithin","theevilwithin2","layersoffear",
    "layersoffear2","blairwitch","littlehope","manofmedan","darkpicturesmanofmedan",
    "darkpictureshouseofashes","darkpicturesthedevilinme","amnesiarebirth",
    
    // Total: ~200 games in this chunk
    // Chunk 5/5 – final gameExecutables
    // Classic / Retro
    "doom","doom2","doom64","wolfenstein3d","quake","quake2","quake3","quake4",
    "duke3d","unrealtournament","unreal","hexen","heretic","blood","carmageddon",
    "carmageddon2","ageofempires","ageofempires2","ageofempires3","heroesofmightandmagic3",
    "heroesofmightandmagic4","heroesofmightandmagic5","baldursgate","baldursgate2",
    "icewinddale","icewinddale2","planescape","fallout","fallout2","fallout3",
    "falloutnewvegas","diablo","diablo2","diablo2lod","diablo3","warcraft1",
    "warcraft2","warcraft3","starcraft","starcraftbroodwar","starcraftremastered",
    "commandandconquer","commandandconquerra","commandandconquer3","commandandconquer4",
    "redalert","redalert2","redalert3",
    
    // Action / Adventure
    "tombraider","tombraider2","tombraider3","tombraider4","tombraider5",
    "shadowofthetombraider","unchartedlegacy","uncharted2","uncharted3","uncharted4",
    "assassinscreed","acorigins","acodyssey","acvalhalla","assassinscreedunity",
    "assassinscreed3","dishonored","dishonored2","prey","control","deathloop",
    "wolfensteinyoungblood","wolfenstein2","alanwake","alanwake2","quantumbreak",
    "hitman","hitman2","hitman3","rage","rage2","metro2033","metrolastlight",
    "metroexodus","shadowofmordor","shadowofwar","mass_effect","masseffect2",
    "masseffect3","mass_effect_andromeda","witcher1","witcher2","witcher3","outerworlds",
    "kingdomhearts3","kingdomhearts2","nierautomata","nierreplicant","talesofarise",
    
    // Survival / Sandbox
    "minecraft","minecraftlauncher","minecraftdungeons","terraria","starbound",
    "valheim","rust","ark","arkse","dayz","subnautica","subnautica_zeros","raft",
    "theforest","sonsforest","dontstarve","dontstarvetogether","greenhell","grounded",
    "eco","noita","7daystodie","projectzomboid","scum","subsistence","survivalcraft",
    "thelongdark","astroneer","astroneeralpha","satisfactory","stationeers","rimworld",
    "factorio","oxygen_not_included","conanexiles",
    
    // Racing / Sports
    "forzahorizon4","forzahorizon5","forzamotorsport7","nfsheat","nfsunbound",
    "nfsmostwanted","nfsunderground","nfsunderground2","assetto_corsa",
    "assettocorsa_competizione","f1_22","f1_23","dirt4","dirt5","crew","crew2",
    "rocketleague","easportsfc","fifa20","fifa21","fifa22","fifa23","pes2020",
    "pes2021","nba2k20","nba2k21","nba2k22","nba2k23","mlbtheshow20","mlbtheshow21",
    "mlbtheshow22","tonyhawkproskater1","tonyhawkproskater2","tonyhawkproskater3",
    "tonyhawkproskater4","trackmania","speedrunners","wrc10","projectcars2",
    "projectcars3","forza4",
    
    // Strategy / MOBA / Card
    "dota2","leagueoflegends","lol","smite","heroesofthestorm","hearthstone",
    "magicarenabattlegrounds","starcraft","starcraft2","warcraft3","ageofempires1",
    "ageofempires2","ageofempires3","ageofempires4","totalwarshogun2",
    "totalwarwarhammer","totalwarwarhammer2","totalwarwarhammer3","xcom","xcom2",
    "ironharvest","anno1800","companyofheroes","companyofheroes2","commandandconquer",
    "commandandconquer3","commandandconquer4","supremecommander","riseofnations",
    "warhammer40kdoa","ageofmythology","starcraftbroodwar","starcraftremastered",
    "commandandconquerra","redalert","redalert2","redalert3",
    
    // Indie / Casual
    "amongus","fallguys","cuphead","hades","deadcells","undertale","stardewvalley",
    "limbo","inside","celeste","slaythespire","katana_zero","entertheshinobi",
    "hotlinemiami","hotlinemiami2","bastion","transistor","hyperlightdrifter",
    "bindingofisaac","roguelegacy","factorio","oxygen_not_included","satisfactory",
    "astroneer","grounded","subnautica","subnautica_zeros","raft","theforest",
    "sonsforest","dontstarve","greenhell","7daystodie","thelongdark","survivalcraft",
    "projectzomboid","scum","subsistence",
    
    // Horror / Thriller
    "phasmophobia","residentEvil","residentEvil2","residentEvil3","residentEvil4",
    "residentEvil5","residentEvil6","residentEvil7","amnesia","soma","outlast",
    "outlast2","alienisolation","theevilwithin","theevilwithin2","layersoffear",
    "layersoffear2","blairwitch","littlehope","manofmedan","darkpicturesmanofmedan",
    "darkpictureshouseofashes","darkpicturesthedevilinme","amnesiarebirth"
    
    // Total: ~200+ games, final chunk
};
        // Fast + no-duplicate game list
        private HashSet<string> gameExecutablesSet;

        // ===============================
        // GAME BOOST UTILS
        // ===============================
        private readonly ConcurrentDictionary<int, IntPtr> originalAffinity
    = new ConcurrentDictionary<int, IntPtr>();

        private int lastGamePID = -1;

        private void ApplyGameBoost(Process game, bool isAdvancedMode)
        {
            try
            {
                if (game == null || game.HasExited)
                    return;

                // Prevent duplicate boost
                if (game.Id == lastGamePID)
                    return;

                lastGamePID = game.Id;

                activeBoostTarget = game.ProcessName;

                // Save original priority safely
                originalPriorities.TryAdd(game.Id, game.PriorityClass);

                // Save original affinity safely
                originalAffinity.TryAdd(game.Id, game.ProcessorAffinity);

                // NEXT-LEVEL PRIORITY BOOST
                if (isAdvancedMode)
                    game.PriorityClass = ProcessPriorityClass.High;
                else
                    game.PriorityClass = ProcessPriorityClass.AboveNormal;

                // FULL CPU UNLOCK
                IntPtr fullAffinity =
                    (IntPtr)((1 << Environment.ProcessorCount) - 1);

                game.ProcessorAffinity = fullAffinity;

                // LOWER BACKGROUND PROCESSES SAFELY
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.HasExited)
                            continue;

                        if (p.Id == game.Id)
                            continue;

                        if (IsProtectedProcess(p))
                            continue;

                        originalPriorities.TryAdd(p.Id, p.PriorityClass);

                        if (p.PriorityClass != ProcessPriorityClass.BelowNormal)
                            p.PriorityClass = ProcessPriorityClass.BelowNormal;
                    }
                    catch { }
                }

                // Safe UI update
                if (isAdvancedMode && IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke((Action)(() =>
                    {
                        lblGameModeStatus.Text =
                            $"Advanced Boost Applied: {game.ProcessName} 🚀";

                        lblGameModeStatus.ForeColor = Color.Lime;
                    }));
                }
            }
            catch
            {
                lastGamePID = -1;
            }
        }





        // ===============================
        // NORMAL GAME MODE (OPTIMIZED)
        // ===============================
        private async Task NormalGameModeLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool found = false;

                    foreach (string game in gameExecutablesSet)
                    {
                        var process = Process.GetProcessesByName(game).FirstOrDefault();

                        if (process != null && !process.HasExited)
                        {
                            found = true;

                            if (activeBoostTarget != process.ProcessName)
                                ApplyGameBoost(process, false);

                            break;
                        }
                    }

                    if (!found && activeBoostTarget != null)
                    {
                        RestoreAllPriorities();
                    }

                    await Task.Delay(1500, token); // Faster detection
                }
            }
            catch (OperationCanceledException) { }
        }



        // ===============================
        // ADVANCED GAME MODE (OPTIMIZED)
        // ===============================
        private async Task AdvancedGameModeLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool found = false;

                    foreach (string game in gameExecutablesSet)
                    {
                        var process = Process.GetProcessesByName(game).FirstOrDefault();

                        if (process != null && !process.HasExited)
                        {
                            found = true;

                            if (activeBoostTarget != process.ProcessName)
                                ApplyGameBoost(process, true);

                            break;
                        }
                    }

                    if (!found)
                    {
                        if (activeBoostTarget != null)
                            RestoreAllPriorities();

                        if (IsHandleCreated && !IsDisposed)
                        {
                            BeginInvoke((Action)(() =>
                            {
                                lblGameModeStatus.Text =
                                    "Advanced Game Mode: Waiting for Game…";
                                lblGameModeStatus.ForeColor = Color.DeepSkyBlue;
                            }));
                        }
                    }

                    await Task.Delay(1000, token); // Faster advanced scan
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke((Action)(() =>
                    {
                        lblGameModeStatus.Text =
                            "Advanced Game Mode: DISABLED";
                        lblGameModeStatus.ForeColor = Color.Orange;
                    }));
                }
            }
        }


        // ===============================
        // SAFE RESTORE (IMPROVED)
        // ===============================
        private void RestoreAllPriorities()
        {
            foreach (var item in originalPriorities.ToArray())
            {
                try
                {
                    var p = Process.GetProcessById(item.Key);
                    if (!p.HasExited)
                        p.PriorityClass = item.Value;
                }
                catch { }
            }

            originalPriorities.Clear();
            activeBoostTarget = null;
        }


        // ===============================
        // MODE CHECK (CLEAN)
        // ===============================
        private bool AnyBoostModeActive()
        {
            return tgAdvancedGame.Checked
                   || tgAdvancedEmulator.Checked
                   || tgNormalGame.Checked;
        }




        // ===============================
        // EMULATOR BOOST MODE
        // ===============================
        private readonly string[] emulatorProcesses =
        {
        "HD-Player",        // BlueStacks / MSI App Player
        "dnplayer",         // LDPlayer
        "Nox",
        "MEmu",
        "AndroidEmulator"   // GameLoop
        };

        private void UpdateEmulatorStatus()
        {
            if (!tgAdvancedGame.Checked)
                return;

            if (!IsHandleCreated || IsDisposed)
                return;

            BeginInvoke((Action)(() =>
            {
                foreach (string game in gameExecutablesSet)
                {
                    if (Process.GetProcessesByName(game).Length > 0)
                    {
                        lblGameModeStatus.Text = $"Game Mode Applied On {game}";
                        lblGameModeStatus.ForeColor = Color.Lime;
                        return;
                    }
                }

                lblGameModeStatus.Text = "Waiting for Game…";
                lblGameModeStatus.ForeColor = Color.Orange;
            }));
        }


        // ===============================
        // EMULATOR BOOST MODE (OPTIMIZED)
        // ===============================
        private async Task EmulatorBoostLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool foundEmulator = false;

                    foreach (string emu in emulatorProcesses)
                    {
                        var emuProc = Process.GetProcessesByName(emu).FirstOrDefault();

                        if (emuProc != null && !emuProc.HasExited)
                        {
                            foundEmulator = true;

                            if (activeBoostTarget != emuProc.ProcessName)
                                ApplyGameBoost(emuProc, true);

                            if (IsHandleCreated && !IsDisposed)
                            {
                                BeginInvoke((Action)(() =>
                                {
                                    lblGameModeStatus.Text =
                                        $"Advanced Emulator Mode: {emuProc.ProcessName} 🚀";
                                    lblGameModeStatus.ForeColor = Color.Lime;
                                }));
                            }

                            break;
                        }
                    }

                    if (!foundEmulator && activeBoostTarget != null)
                    {
                        RestoreAllPriorities();

                        if (IsHandleCreated && !IsDisposed)
                        {
                            BeginInvoke((Action)(() =>
                            {
                                lblGameModeStatus.Text =
                                    "Advanced Emulator Mode: Waiting for Emulator…";
                                lblGameModeStatus.ForeColor = Color.DeepSkyBlue;
                            }));
                        }
                    }

                    await Task.Delay(1200, token); // Faster emulator detection
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (activeBoostTarget != null)
                    RestoreAllPriorities();

                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke((Action)(() =>
                    {
                        lblGameModeStatus.Text =
                            "Advanced Emulator Mode: DISABLED";
                        lblGameModeStatus.ForeColor = Color.Orange;
                    }));
                }
            }
        }




        // ===============================
        // ADVANCED GAME MODE - NEXT LEVEL REALTIME SAFE SYSTEM
        // FULL REPLACEABLE BLOCK
        // ===============================

        #region VARIABLES

        private bool advancedGameModeEnabled = false;

        private string backupPowerPlan = "";
        private int backupVisualFX = 0;
        private int backupThrottle = 0;

        private System.Windows.Forms.Timer realtimeGuardTimer;

        #endregion


        #region INIT (CALL THIS IN FORM CONSTRUCTOR AFTER InitializeComponent)

        private void InitAdvancedGameModeSystem()
        {
            // Backup current settings at startup
            BackupCurrentSettings();

            // Create realtime guard
            realtimeGuardTimer = new System.Windows.Forms.Timer();
            realtimeGuardTimer.Interval = 2000; // every 2 sec
            realtimeGuardTimer.Tick += RealtimeGuardTimer_Tick;

            // Restore automatically if app crashes or closes
            Application.ApplicationExit += OnAppExitRestore;
            AppDomain.CurrentDomain.ProcessExit += OnAppExitRestore;
            AppDomain.CurrentDomain.UnhandledException += OnAppCrashRestore;

            // Guna2 Close Button protection
            this.FormClosing += OnAppExitRestore;
        }

        #endregion


        #region ENABLE ADVANCED GAME MODE

        private void EnableAdvancedGameMode()
        {
            if (advancedGameModeEnabled)
                return;

            advancedGameModeEnabled = true;

            BackupCurrentSettings();

            try
            {
                // Ultimate Performance Power Plan
                RunCommand("powercfg", "-setactive e9a42b02-d5df-448d-aa00-03f14749eb61");

                // Disable CPU Throttle
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                    "PowerThrottlingOff",
                    1,
                    RegistryValueKind.DWord);

                // Best Performance Visual Effects
                Registry.SetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                    "VisualFXSetting",
                    2,
                    RegistryValueKind.DWord);

                // High Priority for this optimizer
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

                // Start realtime protection
                realtimeGuardTimer.Start();
            }
            catch { }
        }

        #endregion


        #region DISABLE ADVANCED GAME MODE

        private void DisableAdvancedGameMode()
        {
            if (!advancedGameModeEnabled)
                return;

            advancedGameModeEnabled = false;

            try
            {
                // Restore power plan
                if (!string.IsNullOrEmpty(backupPowerPlan))
                    RunCommand("powercfg", "-setactive " + backupPowerPlan);

                // Restore throttle
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                    "PowerThrottlingOff",
                    backupThrottle,
                    RegistryValueKind.DWord);

                // Restore visual FX
                Registry.SetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                    "VisualFXSetting",
                    backupVisualFX,
                    RegistryValueKind.DWord);

                realtimeGuardTimer.Stop();
            }
            catch { }
        }

        #endregion


        #region REALTIME GUARD (PREVENT WINDOWS RESET)

        private void RealtimeGuardTimer_Tick(object sender, EventArgs e)
        {
            if (!advancedGameModeEnabled)
                return;

            try
            {
                // Force ultimate performance continuously
                RunCommand("powercfg", "-setactive e9a42b02-d5df-448d-aa00-03f14749eb61");

                // Keep optimizer priority high
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            }
            catch { }
        }

        #endregion


        #region BACKUP SYSTEM

        private void BackupCurrentSettings()
        {
            try
            {
                backupPowerPlan = GetActivePowerPlan();

                object fx = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                    "VisualFXSetting",
                    1);

                backupVisualFX = Convert.ToInt32(fx);

                object throttle = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                    "PowerThrottlingOff",
                    0);

                backupThrottle = Convert.ToInt32(throttle);
            }
            catch { }
        }

        #endregion


        #region AUTO RESTORE ON EXIT OR CRASH

        private void OnAppExitRestore(object sender, EventArgs e)
        {
            DisableAdvancedGameMode();
        }

        private void OnAppCrashRestore(object sender, UnhandledExceptionEventArgs e)
        {
            DisableAdvancedGameMode();
        }

        #endregion


        #region POWER PLAN DETECTOR

        private string GetActivePowerPlan()
        {
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = "powercfg";
                p.StartInfo.Arguments = "/getactivescheme";
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.Start();

                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                int start = output.IndexOf(":") + 1;
                int end = output.IndexOf("(");

                return output.Substring(start, end - start).Trim();
            }
            catch
            {
                return "";
            }
        }

        #endregion


        #region COMMAND RUNNER

        private void RunCommand(string file, string args)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        #endregion

        // ===============================
        // BACKGROUND APPS BOOST (REALTIME SAFE)
        // ===============================
        private async Task BackgroundAppsBoostLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 🚫 Pause if any other boost mode is active
                    if (AnyBoostModeActive())
                    {
                        await Task.Delay(2000, token);
                        continue;
                    }

                    Process[] processes;

                    try
                    {
                        processes = Process.GetProcesses();
                    }
                    catch
                    {
                        await Task.Delay(2000, token);
                        continue;
                    }

                    foreach (Process p in processes)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        try
                        {
                            if (p == null || p.HasExited)
                                continue;

                            if (IsProtectedProcess(p))
                                continue;

                            // Save original priority ONLY once
                            if (!originalPriorities.ContainsKey(p.Id))
                            {
                                originalPriorities[p.Id] = p.PriorityClass;
                            }

                            // Apply boost only if needed
                            if (p.PriorityClass != ProcessPriorityClass.BelowNormal)
                            {
                                p.PriorityClass = ProcessPriorityClass.BelowNormal;
                            }
                        }
                        catch
                        {
                            // Ignore access denied / system process errors
                        }
                    }

                    // Faster refresh = more realtime
                    await Task.Delay(2500, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancel
            }
            finally
            {
                // Restore priorities safely when stopped
                RestoreAllPriorities();
            }
        }




        // ===============================
        // TOGGLE HANDLERS
        // ===============================
        private void tgNormalGame_CheckedChanged(object sender, EventArgs e)
        {
            if (tgNormalGame.Checked)
            {
                tgAdvancedGame.Checked = false;
                tgAdvancedEmulator.Checked = false;

                advancedGameCTS?.Cancel();
                emulatorCTS?.Cancel();

                normalGameCTS = new CancellationTokenSource();
                activeBoostTarget = null;

                _ = NormalGameModeLoopAsync(normalGameCTS.Token);

                lblGameModeStatus.Text = "Normal Game Mode: ENABLED";
                lblGameModeStatus.ForeColor = Color.DeepSkyBlue;
            }
            else
            {
                normalGameCTS?.Cancel();
                normalGameCTS = null;

                if (!AnyBoostModeActive())
                    RestoreAllPriorities();

                lblGameModeStatus.Text = "Normal Game Mode: DISABLED";
                lblGameModeStatus.ForeColor = Color.Orange;
            }

            UpdateTrayBlinkState();
        }



        private void tgAdvancedGame_CheckedChanged(object sender, EventArgs e)
        {
            if (tgAdvancedGame.Checked)
            {
                tgNormalGame.Checked = false;
                tgAdvancedEmulator.Checked = false;

                normalGameCTS?.Cancel();
                emulatorCTS?.Cancel();

                advancedGameCTS = new CancellationTokenSource();
                EnableAdvancedGameMode();

                _ = AdvancedGameModeLoopAsync(advancedGameCTS.Token);
            }
            else
            {
                advancedGameCTS?.Cancel();
                advancedGameCTS = null;

                DisableAdvancedGameMode();
                if (!AnyBoostModeActive())
                    RestoreAllPriorities();
                activeBoostTarget = null;
            }

            UpdateTrayBlinkState();
        }



        private void tgAdvancedEmulator_CheckedChanged(object sender, EventArgs e)
        {
            if (tgAdvancedEmulator.Checked)
            {
                tgNormalGame.Checked = false;
                tgAdvancedGame.Checked = false;

                normalGameCTS?.Cancel();
                advancedGameCTS?.Cancel();

                emulatorCTS = new CancellationTokenSource();
                _ = EmulatorBoostLoopAsync(emulatorCTS.Token);
            }
            else
            {
                emulatorCTS?.Cancel();
                emulatorCTS = null;

                if (!AnyBoostModeActive())
                    RestoreAllPriorities();

            }

            UpdateTrayBlinkState();
        }


        private void tgBgApps_CheckedChanged(object sender, EventArgs e)
        {
            if (tgBgApps.Checked)
            {
                // Prevent double-start
                if (bgAppsCTS != null)
                    return;

                bgAppsCTS = new CancellationTokenSource();
                _ = BackgroundAppsBoostLoopAsync(bgAppsCTS.Token);

                lblGameModeStatus.Text = "Background Apps Boost ENABLED";
                lblGameModeStatus.ForeColor = Color.DeepSkyBlue;
            }
            else
            {
                if (bgAppsCTS != null)
                {
                    bgAppsCTS.Cancel();
                    bgAppsCTS.Dispose();
                    bgAppsCTS = null;
                }

                if (!AnyBoostModeActive())
                    RestoreAllPriorities();


                lblGameModeStatus.Text = "Background Apps Boost DISABLED";
                lblGameModeStatus.ForeColor = Color.Orange;
            }

            // ✅ ALWAYS update tray state
            UpdateTrayBlinkState();
        }



        // ===============================
        // TRAY ICON & BLINK STATE
        // ===============================
        private void UpdateTrayBlinkState()
        {
            if (trayIcon == null)
                return;

            bool active =
                tgNormalGame.Checked ||
                tgAdvancedGame.Checked ||
                tgAdvancedEmulator.Checked ||
                tgBgApps.Checked;

            // Set tray text safely (max 63 chars recommended)
            if (tgAdvancedEmulator.Checked)
                trayIcon.Text = "Advanced Emulator Mode ACTIVE";
            else if (tgAdvancedGame.Checked)
                trayIcon.Text = "Advanced Game Mode ACTIVE";
            else if (tgNormalGame.Checked)
                trayIcon.Text = "Normal Game Mode ACTIVE";
            else if (tgBgApps.Checked)
                trayIcon.Text = "Background Boost ACTIVE";
            else
                trayIcon.Text = "Game Mode OFF";

            if (active)
            {
                trayIcon.Visible = true;

                if (!trayBlinkTimer.Enabled)
                    StartTrayBlink();
            }
            else
            {
                StopTrayBlink();
                trayIcon.Visible = false;
            }
        }


        // ===============================
        // TRAY BLINK CONTROLS
        // ===============================
        private void StartTrayBlink()
        {
            if (trayBlinkTimer == null)
                return;

            trayBlinkTimer.Start();
        }

        private void StopTrayBlink()
        {
            if (trayBlinkTimer == null)
                return;

            trayBlinkTimer.Stop();

            if (trayIcon != null)
                trayIcon.Icon = trayIconNormal;
        }





        // ===============================
        // GENERAL
        // ===============================
        private ToolTip tip;

        // ===============================
        // SMOOTHING VARIABLES
        // ===============================
        private const float smoothing = 0.12f;

        private float currentCpu = 0f;
        private int targetCpu = 0;

        private float currentRam = 0f;
        private int targetRam = 0;

        private float currentDrive = 0f;
        private int targetDriveUsage = 0;

        // ===============================
        // CPU & SYSTEM
        // ===============================
        private PerformanceCounter cpuCounter;
        private DriveInfo systemDrive;
        private System.Windows.Forms.Timer animationTimer;



        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);


        private void LoadSavedSettings()
        {
            try
            {
                // Prevent toggle events while loading
                suppressMinimizeEvent = true;

                // ===============================
                // LOAD UI SETTINGS SAFELY
                // ===============================
                if (Properties.Settings.Default != null)
                {
                    tgMinimizeToTray.Checked =
                        Properties.Settings.Default.MinimizeToTray;

                    tgReduceAnimations.Checked =
                        Properties.Settings.Default.ReduceAnimations;

                    tgAimOptimize.Checked =
                        Properties.Settings.Default.AimOptimize;
                }
            }
            catch (Exception)
            {
                // If settings corrupted → reset safely
                try
                {
                    Properties.Settings.Default.Reset();
                }
                catch { }
            }
            finally
            {
                // Re-enable events
                suppressMinimizeEvent = false;
            }
        }


        public Optimizer()
        {
            InitializeComponent();

            try
            {
                this.FormClosing += Optimizer_FormClosing;

                InitCounters();
                LoadSystemInfo();

                // ===============================
                // SET ICONS FIRST
                // ===============================
                trayIconNormal = this.Icon;
                trayIconAlert = Properties.Resources.Icon; // your alert icon

                // ===============================
                // INITIALIZE TRAY ICON AND MENU
                // ===============================
                InitTray();

                tip = new ToolTip();

                systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));

                // ===============================
                // SAFE UPDATER
                // ===============================
                try
                {
                    Updater.CheckAndUpdate();
                }
                catch { }

                lblDriveCTitle.Text =
                    $"{systemDrive.VolumeLabel} ({systemDrive.Name.TrimEnd('\\')})";

                // ===============================
                // TIMERS
                // ===============================

                pingTimer = new System.Windows.Forms.Timer();
                pingTimer.Interval = 1000;
                pingTimer.Tick += PingTimer_Tick;
                pingTimer.Start();

                usageTimer = new System.Windows.Forms.Timer();
                usageTimer.Interval = 1000;
                usageTimer.Tick += UsageTimer_Tick;
                usageTimer.Start();

                animationTimer = new System.Windows.Forms.Timer();
                animationTimer.Interval = 16;
                animationTimer.Tick += AnimationTimer_Tick;
                animationTimer.Start();

                trayBlinkTimer = new System.Windows.Forms.Timer();
                trayBlinkTimer.Interval = 500;
                trayBlinkTimer.Tick += (s, e) =>
                {
                    if (trayIcon == null) return;

                    trayBlinkState = !trayBlinkState;
                    trayIcon.Icon = trayBlinkState
                        ? trayIconAlert
                        : trayIconNormal;
                };

                trayIconNormal = this.Icon;
                trayIconAlert = Properties.Resources.Icon;

                LoadSavedSettings();

                this.MaximumSize = this.Size;
                this.MinimumSize = this.Size;

                lblVersion.Text = Application.ProductVersion;

                tgAimOptimize.Checked = Properties.Settings.Default.AimOptimize;

                gameExecutablesSet = new HashSet<string>(
                    gameExecutables.Select(g => g.ToLower())
                );

                // ===============================
                // SAFE EXIT RESTORE
                // ===============================
                AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                {
                    try
                    {
                        DisableProAimOptimization();
                    }
                    catch { }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Startup error:\n" + ex.Message,
                    "Optimizer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void Optimizer_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // ===============================
                // MINIMIZE TO TRAY MODE
                // ===============================
                if (tgMinimizeToTray.Checked && !allowExit)
                {
                    e.Cancel = true;

                    this.Hide();

                    if (trayIcon != null)
                    {
                        trayIcon.Visible = true;
                        trayIcon.Icon = trayIconNormal;
                        trayIcon.Text = "Optimizer (Running in Background)";
                    }

                    SetAdminStatus("Optimizer Minimized to Tray", Color.DeepSkyBlue);

                    return;
                }

                // ===============================
                // REAL EXIT MODE
                // ===============================

                allowExit = true;

                DisableProAimOptimization();

                normalGameCTS?.Cancel();
                advancedGameCTS?.Cancel();
                emulatorCTS?.Cancel();
                bgAppsCTS?.Cancel();

                normalGameCTS?.Dispose();
                advancedGameCTS?.Dispose();
                emulatorCTS?.Dispose();
                bgAppsCTS?.Dispose();

                RestoreAllPriorities();

                DisableAdvancedGameMode();

                usageTimer?.Stop();
                animationTimer?.Stop();
                pingTimer?.Stop();
                trayBlinkTimer?.Stop();

                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MAXIMIZE = 0xF030;

            if (m.Msg == WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == SC_MAXIMIZE)
            {
                return; // Block maximize completely
            }

            base.WndProc(ref m);
        }


        private void InitTray()
        {
            if (trayIcon != null) return;

            // ===============================
            // TRAY ICON
            // ===============================
            trayIcon = new NotifyIcon
            {
                Icon = trayIconNormal,
                Text = "Optimizer",
                Visible = false
            };

            // ===============================
            // CONTEXT MENU
            // ===============================
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open", null, (s, e) => RestoreFromTray());
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, (s, e) =>
            {
                allowExit = true;
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
                Application.Exit();
            });
            trayIcon.ContextMenuStrip = trayMenu;

            // ===============================
            // LEFT CLICK RESTORE
            // ===============================
            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    RestoreFromTray();
            };

            // ===============================
            // BLINK TIMER
            // ===============================
            trayBlinkTimer = new System.Windows.Forms.Timer { Interval = 500 };
            trayBlinkTimer.Tick += (s, e) =>
            {
                if (trayIcon == null) return;
                trayBlinkState = !trayBlinkState;
                trayIcon.Icon = trayBlinkState ? trayIconAlert : trayIconNormal;
            };
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();

            if (trayIcon != null)
                trayIcon.Visible = false;

            allowExit = false;

            SetAdminStatus("Restored from Tray", Color.Lime);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (tgMinimizeToTray.Checked && this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                trayIcon.Visible = true;

                trayIcon.ShowBalloonTip(
                    1000,
                    "Optimizer",
                    "Running in system tray",
                    ToolTipIcon.Info
                );
            }
        }


        private void PingTimer_Tick(object sender, EventArgs e)
        {
            Task.Run(() => UpdatePing());
        }


        private void InitCounters()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
                cpuCounter.NextValue();
            }
            catch
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
        }

        private void UsageTimer_Tick(object sender, EventArgs e)
        {
            targetCpu = (int)Math.Min(100, cpuCounter.NextValue());
            targetRam = GetRamUsage();
            targetDriveUsage = GetDriveUsage();

            UpdateDriveTooltip();
            UpdateEmulatorStatus();
            targetOverall = CalculateOverallCondition(targetCpu, targetRam, targetDriveUsage);
            animationTimer.Interval = tgReduceAnimations.Checked ? 40 : 16;
        }

        // RENDER SMOOTH ANIMATION (Runs every 16ms)
        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (tgReduceAnimations.Checked)
            {
                animationTimer.Interval = 40; // slower = less CPU
            }
            else
            {
                animationTimer.Interval = 16; // smooth
            }

            // 1. Smooth CPU
            currentCpu += (targetCpu - currentCpu) * smoothing;
            cpubar.Value = (int)currentCpu;
            cpuusage.Text = (int)currentCpu + "%";
            ApplyNeon(cpubar, (int)currentCpu);

            // 2. Smooth RAM
            currentRam += (targetRam - currentRam) * smoothing;
            rambar.Value = (int)currentRam;
            ramusage.Text = (int)currentRam + "%";
            ApplyNeon(rambar, (int)currentRam);

            // 3. Smooth Drive
            currentDrive += (targetDriveUsage - currentDrive) * smoothing;
            int dValue = Math.Max(0, Math.Min(100, (int)currentDrive));
            driveCBar.Value = dValue;
            lblDriveC.Text = dValue + "% Used";

            ApplyStorageNeon(driveCBar, dValue);
            UpdateStorageHealth(dValue);

            // 4. Smooth Overall Condition
            currentOverall += (targetOverall - currentOverall) * smoothing;
            int oValue = Math.Max(0, Math.Min(100, (int)currentOverall));

            overallBar.Value = oValue;
            lblOverallPercent.Text = oValue + "%";

            if (oValue >= 75)
            {
                lblOverallStatus.Text = "Overall PC Condition:EXCELLENT";
                overallBar.ProgressColor = Color.Lime;
                overallBar.ProgressColor2 = Color.Cyan;
            }
            else if (oValue >= 50)
            {
                lblOverallStatus.Text = "Overall PC Condition:GOOD";
                overallBar.ProgressColor = Color.Gold;
                overallBar.ProgressColor2 = Color.Orange;
            }
            else if (oValue >= 30)
            {
                lblOverallStatus.Text = "Overall PC Condition:STRESSED";
                overallBar.ProgressColor = Color.OrangeRed;
                overallBar.ProgressColor2 = Color.DarkOrange;
            }
            else
            {
                lblOverallStatus.Text = "Overall PC Condition:CRITICAL";
                overallBar.ProgressColor = Color.Red;
                overallBar.ProgressColor2 = Color.DarkRed;
            }

        }

        private int GetRamUsage()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus)) return (int)memStatus.dwMemoryLoad;
            return 0;
        }

        private int GetDriveUsage()
        {
            try
            {
                long used = systemDrive.TotalSize - systemDrive.TotalFreeSpace;
                return (int)(used * 100 / systemDrive.TotalSize);
            }
            catch { return 0; }
        }

        private void UpdateDriveTooltip()
        {
            try
            {
                double total = systemDrive.TotalSize / 1024d / 1024 / 1024;
                double free = systemDrive.TotalFreeSpace / 1024d / 1024 / 1024;
                double used = total - free;

                string text = $"{systemDrive.VolumeLabel} ({systemDrive.Name.TrimEnd('\\')})\n\n" +
                              $"Used: {used:F1} GB\n" +
                              $"Free: {free:F1} GB\n" +
                              $"Total: {total:F1} GB";

                tip.SetToolTip(driveCBar, text);
                tip.SetToolTip(lblDriveC, text);
            }
            catch { }
        }

        private void ApplyNeon(Guna2CircleProgressBar bar, int value)
        {
            if (value < 40) { bar.ProgressColor = Color.FromArgb(0, 255, 170); bar.ProgressColor2 = Color.FromArgb(0, 150, 255); }
            else if (value < 80) { bar.ProgressColor = Color.Gold; bar.ProgressColor2 = Color.Orange; }
            else { bar.ProgressColor = Color.Red; bar.ProgressColor2 = Color.DarkRed; }
        }

        private void ApplyStorageNeon(Guna2CircleProgressBar bar, int value)
        {
            if (value < 60) { bar.ProgressColor = Color.FromArgb(0, 255, 170); bar.ProgressColor2 = Color.FromArgb(0, 150, 255); }
            else if (value < 85) { bar.ProgressColor = Color.Gold; bar.ProgressColor2 = Color.Orange; }
            else { bar.ProgressColor = Color.Red; bar.ProgressColor2 = Color.DarkRed; }
        }

        private void UpdateStorageHealth(int usage)
        {
            if (usage < 70) { lblStorageHealth.Text = "Storage Health: Good"; lblStorageHealth.ForeColor = Color.Lime; }
            else if (usage < 90) { lblStorageHealth.Text = "Storage Health: Warning"; lblStorageHealth.ForeColor = Color.Gold; }
            else { lblStorageHealth.Text = "Storage Health: CRITICAL"; lblStorageHealth.ForeColor = Color.Red; }
        }

        private void LoadSystemInfo()
        {
            lblPCName.Text = "PC Name: " + Environment.MachineName;
            lblWindows.Text = "Windows: " + GetWindowsEdition();
            lblOS.Text = "OS Version: " + GetOSVersion();
            lblCPU.Text = "CPU: " + GetCPUName();
            lblGPU.Text = "GPU: " + GetGPUName();
            lblTotalRAM.Text = "Total RAM: " + GetTotalRam() + " GB";
            lblArch.Text = Environment.Is64BitOperatingSystem ? "64-bit OS" : "32-bit OS";
        }

        private string GetCPUName()
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                foreach (ManagementObject o in s.Get()) return o["Name"].ToString();
            return "Unknown";
        }

        private string GetGPUName()
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                foreach (ManagementObject o in s.Get()) return o["Name"].ToString();
            return "Unknown";
        }

        private string GetTotalRam()
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                foreach (ManagementObject o in s.Get()) return Math.Round(Convert.ToDouble(o["TotalPhysicalMemory"]) / 1024 / 1024 / 1024, 1).ToString();
            return "0";
        }

        private string GetWindowsEdition()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
                foreach (ManagementObject os in searcher.Get()) return os["Caption"]?.ToString() ?? "Unknown";
            return "Unknown";
        }

        private string GetOSVersion()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT Version FROM Win32_OperatingSystem"))
                foreach (ManagementObject os in searcher.Get()) return os["Version"]?.ToString() ?? "Unknown";
            return "Unknown";
        }

        private void ShowPanel(Panel p, string panelName)
        {
            // Hide all panels
            Homepnl.Visible = Cleanerpnl.Visible = boostpnl.Visible = gamemodpnl.Visible = settingspnl.Visible = infopnl.Visible = false;

            // Show selected panel
            p.Visible = true;

            // ✅ Save last panel only if toggle is ON
            if (rememberLastPanel)
            {
                lastPanel = panelName;
                Properties.Settings.Default.LastPanel = lastPanel;
                Properties.Settings.Default.Save();
            }
        }


        void CleanFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                foreach (string file in Directory.GetFiles(path)) try { File.Delete(file); } catch { }
                foreach (string dir in Directory.GetDirectories(path)) try { Directory.Delete(dir, true); } catch { }
            }
            catch { }
        }

        [DllImport("shell32.dll")]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, RecycleFlags dwFlags);

        enum RecycleFlags { SHERB_NOCONFIRMATION = 0x00000001, SHERB_NOPROGRESSUI = 0x00000002, SHERB_NOSOUND = 0x00000004 }

        private void guna2Button1_Click(object s, EventArgs e) => ShowPanel(Homepnl, "Homepnl");
        private void guna2Button2_Click(object s, EventArgs e) => ShowPanel(boostpnl, "boostpnl");
        private void guna2Button3_Click(object s, EventArgs e) => ShowPanel(Cleanerpnl, "Cleanerpnl");
        private void guna2Button4_Click(object s, EventArgs e) => ShowPanel(gamemodpnl, "gamemodpnl");
        private void guna2Button5_Click(object s, EventArgs e) => ShowPanel(settingspnl, "settingspnl");
        private void guna2Button6_Click(object s, EventArgs e) => ShowPanel(infopnl, "infopnl");


        // ===============================
        // CLEAN BUTTON
        // ===============================
        private void btnCleanNow_Click_1(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                bool anyChecked =
                    chkTemp.Checked ||
                    chkWinTemp.Checked ||
                    chkPrefetch.Checked ||
                    chkBrowser.Checked ||
                    chkRecycle.Checked;

                if (!anyChecked)
                {
                    SafeUI(() =>
                    {
                        ShowCleanPopup("Nothing Selected ❌", Color.OrangeRed);
                    });
                    return;
                }

                int cleaned = 0;

                try
                {
                    if (chkTemp.Checked)
                    {
                        CleanFolder(Path.GetTempPath());
                        cleaned++;
                    }

                    if (chkWinTemp.Checked)
                    {
                        CleanFolder(@"C:\Windows\Temp");
                        cleaned++;
                    }

                    if (chkPrefetch.Checked)
                    {
                        CleanFolder(@"C:\Windows\Prefetch");
                        cleaned++;
                    }

                    if (chkBrowser.Checked)
                    {
                        string chrome =
                            Path.Combine(
                                Environment.GetFolderPath(
                                    Environment.SpecialFolder.LocalApplicationData),
                                @"Google\Chrome\User Data\Default\Cache");

                        CleanFolder(chrome);
                        cleaned++;
                    }

                    if (chkRecycle.Checked)
                    {
                        SHEmptyRecycleBin(
                            IntPtr.Zero,
                            null,
                            RecycleFlags.SHERB_NOCONFIRMATION |
                            RecycleFlags.SHERB_NOPROGRESSUI |
                            RecycleFlags.SHERB_NOSOUND);

                        cleaned++;
                    }
                }
                catch { }

                SafeUI(() =>
                {
                    ShowCleanPopup(
                        $"Clean Completed ✔ ({cleaned})",
                        Color.Lime);

                    ResetCleanerCheckboxes();
                });

            });
        }

        private async void ShowCleanPopup(string message, Color color)
        {
            if (IsDisposed) return;

            lblCleanStatus.Text = message;
            lblCleanStatus.ForeColor = color;
            lblCleanStatus.Visible = true;

            await Task.Delay(1800);

            for (int i = 255; i >= 0; i -= 8)
            {
                if (IsDisposed) return;

                lblCleanStatus.ForeColor =
                    Color.FromArgb(i, color);

                await Task.Delay(15);
            }

            lblCleanStatus.Visible = false;
        }

        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);

        private void btnRamBoost_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                int boosted = 0;

                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.HasExited)
                            continue;

                        EmptyWorkingSet(p.Handle);
                        boosted++;
                    }
                    catch { }
                }

                SafeUI(() =>
                {
                    ShowBoostPopup(
                        $"RAM Boosted 🚀 ({boosted})",
                        Color.DeepSkyBlue);
                });
            });
        }

        private void btnBgApps_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                Process foreground = null;
                int killedCount = 0;

                try
                {
                    IntPtr hwnd = GetForegroundWindow();

                    if (hwnd != IntPtr.Zero)
                    {
                        GetWindowThreadProcessId(hwnd, out int pid);
                        foreground = Process.GetProcessById(pid);
                    }

                    foreach (Process p in Process.GetProcesses())
                    {
                        try
                        {
                            if (p.HasExited)
                                continue;

                            if (foreground != null &&
                                p.Id == foreground.Id)
                                continue;

                            if (p.Id ==
                                Process.GetCurrentProcess().Id)
                                continue;

                            if (IsProtectedProcess(p))
                                continue;

                            if (p.PriorityClass ==
                                ProcessPriorityClass.RealTime ||
                                p.PriorityClass ==
                                ProcessPriorityClass.High)
                                continue;

                            p.Kill();
                            killedCount++;
                        }
                        catch { }
                    }
                }
                catch { }

                SafeUI(() =>
                {
                    lblGameModeStatus.Text =
                        $"Background Apps KILLED ({killedCount})";

                    lblGameModeStatus.ForeColor =
                        Color.Red;

                    ShowBoostPopup(
                        $"Killed {killedCount} Background Apps 💀",
                        Color.Red);
                });

            });
        }


        private void SafeUI(Action action)
        {
            if (IsDisposed) return;

            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }


        private void btnHighPerf_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = "-setactive SCHEME_MIN",
                        Verb = "runas",
                        CreateNoWindow = true,
                        UseShellExecute = true
                    });

                    SafeUI(() =>
                    {
                        ShowBoostPopup(
                            "High Performance Enabled ⚡",
                            Color.Lime);
                    });
                }
                catch { }
            });
        }

        private async void ShowBoostPopup(string message, Color color)
        {
            if (IsDisposed) return;

            lblBoostStatus.Text = message;
            lblBoostStatus.ForeColor = color;
            lblBoostStatus.Visible = true;

            await Task.Delay(1800);

            for (int i = 255; i >= 0; i -= 8)
            {
                if (IsDisposed) return;

                lblBoostStatus.ForeColor =
                    Color.FromArgb(i, color);

                await Task.Delay(15);
            }

            lblBoostStatus.Visible = false;
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void guna2GradientTileButton1_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    IntPtr hwnd = GetForegroundWindow();
                    if (hwnd == IntPtr.Zero)
                        return;

                    GetWindowThreadProcessId(hwnd, out int pid);

                    Process fg = Process.GetProcessById(pid);

                    if (fg == null || fg.HasExited)
                        return;

                    // 🔥 Set foreground to HIGH (safe limit)
                    if (fg.PriorityClass != ProcessPriorityClass.High)
                        fg.PriorityClass = ProcessPriorityClass.High;

                    int lowered = 0;

                    foreach (Process p in Process.GetProcesses())
                    {
                        try
                        {
                            if (p.HasExited)
                                continue;

                            if (p.Id == fg.Id)
                                continue;

                            // ❌ Skip system critical processes
                            if (IsProtectedProcess(p))
                                continue;

                            // ❌ Skip explorer
                            if (p.ProcessName.Equals("explorer",
                                StringComparison.OrdinalIgnoreCase))
                                continue;

                            // Only lower NORMAL processes
                            if (p.PriorityClass == ProcessPriorityClass.Normal)
                            {
                                p.PriorityClass = ProcessPriorityClass.BelowNormal;
                                lowered++;
                            }
                        }
                        catch { }
                    }

                    SafeUI(() =>
                    {
                        ShowBoostPopup(
                            $"CPU Boost Applied ({lowered} lowered)",
                            Color.DeepSkyBlue);
                    });
                }
                catch { }
            });
        }

        private void btnNetBoost_Click(object sender, EventArgs e)
        {
            if (!IsRunningAsAdmin())
            {
                ShowBoostPopup("Admin Rights Required ⚠", Color.Red);
                SetAdminStatus("Network Boost Failed (No Admin)", Color.Red);
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    const string key =
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

                    Registry.SetValue(
                        key,
                        "NetworkThrottlingIndex",
                        unchecked((int)0xFFFFFFFF),
                        RegistryValueKind.DWord);

                    Registry.SetValue(
                        key,
                        "SystemResponsiveness",
                        0,
                        RegistryValueKind.DWord);

                    SafeUI(() =>
                    {
                        ShowBoostPopup("Network Boost Enabled 🚀", Color.Lime);
                        SetAdminStatus("Network Boost: ENABLED", Color.Lime);
                    });
                }
                catch
                {
                    SafeUI(() =>
                    {
                        ShowBoostPopup("Network Boost Failed ❌", Color.OrangeRed);
                        SetAdminStatus("Network Boost Error", Color.OrangeRed);
                    });
                }
            });
        }


        private void btnQuickFlush_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    SafeUI(() =>
                    {
                        ShowBoostPopup(
                            "Quick Memory Flush Done 🧹",
                            Color.Lime);
                    });
                }
                catch { }
            });
        }

        private void ResetCleanerCheckboxes()
        {
            chkTemp.Checked = false;
            chkWinTemp.Checked = false;
            chkPrefetch.Checked = false;
            chkBrowser.Checked = false;
            chkRecycle.Checked = false;
        }

        private void UpdatePing()
        {
            try
            {
                Ping ping = new Ping();
                PingReply reply = ping.Send("8.8.8.8", 1000); // Google DNS

                if (reply.Status == IPStatus.Success)
                {
                    int ms = (int)reply.RoundtripTime;

                    this.Invoke((Action)(() =>
                    {
                        lblPing.Text = $"Ping: {ms} ms";
                        pingBar.Value = Math.Max(pingBar.Minimum,
                         Math.Min(ms, pingBar.Maximum));
                        ApplyPingColor(ms);
                    }));
                }
                else
                {
                    SetPingOffline();
                }
            }
            catch
            {
                SetPingOffline();
            }
        }

        private void SetPingOffline()
        {
            this.Invoke((Action)(() =>
            {
                lblPing.Text = "Ping: -- ms";
                pingBar.Value = 0;
                pingBar.ProgressColor = Color.Gray;
                pingBar.ProgressColor2 = Color.DarkGray;
            }));
        }

        private void ApplyPingColor(int ms)
        {
            if (ms < 60)
            {
                pingBar.ProgressColor = Color.Lime;
                pingBar.ProgressColor2 = Color.GreenYellow;
            }
            else if (ms < 120)
            {
                pingBar.ProgressColor = Color.Gold;
                pingBar.ProgressColor2 = Color.Orange;
            }
            else
            {
                pingBar.ProgressColor = Color.Red;
                pingBar.ProgressColor2 = Color.DarkRed;
            }
        }



        private bool IsRunningAsAdmin()
        {
            return new WindowsPrincipal(
                WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void btnRestoreDefaults_Click(object sender, EventArgs e)
        {
            tgMinimizeToTray.Checked = false;
            tgReduceAnimations.Checked = false;
            tgAimOptimize.Checked = false;

            Properties.Settings.Default.Reset();
            Properties.Settings.Default.Save();

            SetAdminStatus(
                "Settings Restored to Default",
                Color.DeepSkyBlue
            );
        }

        private void tgMinimizeToTray_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressMinimizeEvent)
                return;

            if (tgMinimizeToTray.Checked)
            {
                SetAdminStatus(
                    "Minimize to Tray: ENABLED",
                    Color.Lime
                );
            }
            else
            {
                trayIcon.Visible = false;

                SetAdminStatus(
                    "Minimize to Tray: DISABLED",
                    Color.Orange
                );
            }
            Properties.Settings.Default.MinimizeToTray = tgMinimizeToTray.Checked;
            Properties.Settings.Default.Save();


        }

        private void guna2ControlBox1_Click(object sender, EventArgs e)
        {
            if (tgMinimizeToTray.Checked)
            {
                this.Hide();
                trayIcon.Visible = true;
                return;
            }

            ExitApplication();
        }

        private void ExitApplication()
        {
            allowExit = true;
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (tgMinimizeToTray.Checked && !allowExit)
            {
                e.Cancel = true;

                this.Hide();
                trayIcon.Visible = true;

                trayIcon.ShowBalloonTip(
                    1000,
                    "Optimizer",
                    "Still running in system tray",
                    ToolTipIcon.Info
                );

                SetAdminStatus("Running in Tray", Color.DeepSkyBlue);
            }

            base.OnFormClosing(e);
        }

        private void guna2ToggleSwitch1_CheckedChanged(object sender, EventArgs e)
        {
            if (tgReduceAnimations.Checked)
            {
                SetAdminStatus("Animations Reduced",
                Color.Gold
                );
            }
            else
            {
                SetAdminStatus("Animations Restored",
                Color.DeepSkyBlue
                );
            }
            Properties.Settings.Default.ReduceAnimations = tgReduceAnimations.Checked;
            Properties.Settings.Default.Save();

        }

        private void label19_Click(object sender, EventArgs e)
        {

        }

        private void guna2ImageButton1_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://www.youtube.com/@MR.PC_GAMER_YT",
                UseShellExecute = true
            });
        }

        private void guna2ImageButton2_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/XbqcMzwfQQ",
                UseShellExecute = true
            });
        }

        private void Optimizer_Load(object sender, EventArgs e)
        {
            if (rememberLastPanel)
            {
                // Load last panel
                string panelToShow = Properties.Settings.Default.LastPanel ?? "Homepnl";

                switch (panelToShow)
                {
                    case "Homepnl": ShowPanel(Homepnl, "Homepnl"); break;
                    case "boostpnl": ShowPanel(boostpnl, "boostpnl"); break;
                    case "Cleanerpnl": ShowPanel(Cleanerpnl, "Cleanerpnl"); break;
                    case "gamemodpnl": ShowPanel(gamemodpnl, "gamemodpnl"); break;
                    case "settingspnl": ShowPanel(settingspnl, "settingspnl"); break;
                    case "infopnl": ShowPanel(infopnl, "infopnl"); break;
                    default: ShowPanel(Homepnl, "Homepnl"); break;
                }
            }
        }

        private void label9_Click(object sender, EventArgs e)
        {

        }

        private void gamemodpnl_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click_1(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        // ================= AIM OPTIMIZER STATE =================

        private CancellationTokenSource aimBoostCTS = null;
        private bool aimOptimizerActive = false;
        private bool timerResolutionActive = false;
        private int lastBoostedPID = -1;


        // ================= AIM TOGGLE =================

        private void tgAimOptimize_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                Properties.Settings.Default.AimOptimize = tgAimOptimize.Checked;
                Properties.Settings.Default.Save();

                if (tgAimOptimize.Checked)
                {
                    EnableProAimOptimization();
                    SetAdminStatus("Aim Optimization ENABLED 🎯", Color.Lime);
                }
                else
                {
                    DisableProAimOptimization();
                    SetAdminStatus("Aim Optimization DISABLED ❌", Color.Orange);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Aim Optimization Error:\n" + ex.Message);
            }
        }


        // ================= ENABLE =================

        private void EnableProAimOptimization()
        {
            lock (restoreLock)
            {
                if (aimOptimizerActive)
                    return;

                aimOptimizerActive = true;

                SaveOriginalMouseSettings();   // 🔥 IMPORTANT

                DisableMouseAccelerationInstant();

                if (!timerResolutionActive)
                {
                    timeBeginPeriod(1);
                    timerResolutionActive = true;
                }

                StartAimBoostLoop();
            }
        }

        //==============Save original mouse settings to restore later (important for user experience)================

        private void SaveOriginalMouseSettings()
        {
            try
            {
                originalMouseSpeed =
                    Registry.GetValue(
                        @"HKEY_CURRENT_USER\Control Panel\Mouse",
                        "MouseSpeed",
                        "1")?.ToString();

                originalThreshold1 =
                    Registry.GetValue(
                        @"HKEY_CURRENT_USER\Control Panel\Mouse",
                        "MouseThreshold1",
                        "6")?.ToString();

                originalThreshold2 =
                    Registry.GetValue(
                        @"HKEY_CURRENT_USER\Control Panel\Mouse",
                        "MouseThreshold2",
                        "10")?.ToString();

                originalSensitivity =
                    Registry.GetValue(
                        @"HKEY_CURRENT_USER\Control Panel\Mouse",
                        "MouseSensitivity",
                        "10")?.ToString();
            }
            catch { }
        }


        // ================= DISABLE =================

        private void DisableProAimOptimization()
        {
            lock (restoreLock)
            {
                aimOptimizerActive = false;

                try
                {
                    aimBoostCTS?.Cancel();
                    aimBoostCTS?.Dispose();
                    aimBoostCTS = null;
                }
                catch { }

                RestorePriorities();
                RestoreMouseDefaultsInstant();

                if (timerResolutionActive)
                {
                    timeEndPeriod(1);
                    timerResolutionActive = false;
                }

                lastBoostedPID = -1;
            }
        }


        // ================= SAFE CLOSE SUPPORT =================

        // Call this in your FormClosing event OR Guna2 close button event
        private void SafeStopAimOptimizer()
        {
            try
            {
                DisableProAimOptimization();
            }
            catch { }
        }


        // ================= REALTIME LOOP =================

        private void StartAimBoostLoop()
        {
            if (aimBoostCTS != null)
                return;

            aimBoostCTS = new CancellationTokenSource();
            var token = aimBoostCTS.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && aimOptimizerActive)
                {
                    BoostActiveGameRealtime();

                    try
                    {
                        await Task.Delay(8, token); // 125 updates/sec (NEXT LEVEL)
                    }
                    catch
                    {
                        break;
                    }
                }
            }, token);
        }


        // ================= REALTIME BOOST =================

        private void BoostActiveGameRealtime()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();

                if (hwnd == IntPtr.Zero)
                    return;

                int pid;
                GetWindowThreadProcessId(hwnd, out pid);

                if (pid <= 0)
                    return;

                if (pid == lastBoostedPID)
                    return;

                Process p = Process.GetProcessById(pid);

                if (!gameProcesses.Contains(p.ProcessName))
                    return;

                lastBoostedPID = pid;

                // Save original priority
                if (!originalPriorities.ContainsKey(pid))
                    originalPriorities.TryAdd(pid, p.PriorityClass);

                // Apply HIGH priority safely
                if (p.PriorityClass != ProcessPriorityClass.High)
                    p.PriorityClass = ProcessPriorityClass.High;

                ForceMouseRefresh();
            }
            catch
            {
                lastBoostedPID = -1;
            }
        }


        // ================= MOUSE DISABLE ACCEL =================

        private void DisableMouseAccelerationInstant()
        {
            try
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\\Control Panel\\Mouse", "MouseSpeed", "0");
                Registry.SetValue(@"HKEY_CURRENT_USER\\Control Panel\\Mouse", "MouseThreshold1", "0");
                Registry.SetValue(@"HKEY_CURRENT_USER\\Control Panel\\Mouse", "MouseThreshold2", "0");
                Registry.SetValue(@"HKEY_CURRENT_USER\\Control Panel\\Mouse", "MouseSensitivity", "10");

                int[] mouseParams = new int[] { 0, 0, 0 };

                SystemParametersInfo(
                    SPI_SETMOUSE,
                    0,
                    mouseParams,
                    SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                ForceMouseRefresh();
            }
            catch { }
        }


        // ================= RESTORE =================

        private void RestoreMouseDefaultsInstant()
        {
            try
            {
                if (originalMouseSpeed != null)
                    Registry.SetValue(
                        @"HKEY_CURRENT_USER\Control Panel\Mouse",
                        "MouseSpeed",
                        originalMouseSpeed);

                if (originalThreshold1 != null)
                    Registry.SetValue(
                        @"HKEY_CURRENT_USER\Control Panel\Mouse",
                        "MouseThreshold1",
                        originalThreshold1);

                if (originalThreshold2 != null)
                    Registry.SetValue(
                        @"HKEY_CURRENT_USER\Control Panel\Mouse",
                        "MouseThreshold2",
                        originalThreshold2);

                if (originalSensitivity != null)
                    Registry.SetValue(
                        @"HKEY_CURRENT_USER\Control Panel\Mouse",
                        "MouseSensitivity",
                        originalSensitivity);

                int[] mouseParams =
                {
    int.Parse(originalThreshold1 ?? "6"),
    int.Parse(originalThreshold2 ?? "10"),
    int.Parse(originalMouseSpeed ?? "1")
};

                SystemParametersInfo(
                    SPI_SETMOUSE,
                    0,
                    mouseParams,
                    SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                ForceMouseRefresh();
            }
            catch { }
        }


        // ================= RESTORE PRIORITY =================

        private void RestorePriorities()
        {
            foreach (var entry in originalPriorities)
            {
                try
                {
                    Process.GetProcessById(entry.Key).PriorityClass = entry.Value;
                }
                catch { }
            }

            originalPriorities.Clear();
        }


        // ================= FORCE REFRESH =================

        private void ForceMouseRefresh()
        {
            try
            {
                Point pos = Cursor.Position;

                Cursor.Position = new Point(pos.X + 1, pos.Y);
                Cursor.Position = pos;
            }
            catch { }
        }

    }
}
