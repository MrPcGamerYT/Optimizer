using Guna.UI2.WinForms;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Forms;



namespace Optimizer
{
    public partial class Optimizer : Form
    {

        // Should we remember the last panel? (toggle ON/OFF)
        private bool rememberLastPanel = true; // user can change this later

        // Store the last visible panel's name
        private string lastPanel = "homePnl"; // default panel at first start

        // Storage alert tracking (add this)
        private int lastAlertLevel = -1; // -1 = no alert shown yet

        private void SetAdminStatus(string text, Color color)
        {
            lblAdminStatus.Text = text;
            lblAdminStatus.ForeColor = color;
        }

        private Timer trayBlinkTimer;
        private bool trayBlinkState = false;
        private Icon trayIconNormal;
        private Icon trayIconAlert;


        private NotifyIcon trayIcon;
        private bool suppressMinimizeEvent = false;
        private bool allowExit = false;
        private ContextMenuStrip trayMenu;
        private bool bgAppBoostRunning = false;
        private Timer pingTimer;

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        private float currentOverall = 0;
        private int targetOverall = 0;

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
        private bool normalGameModeRunning = false;
        private string currentGame = null;

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
    "entertheshinobi","celeste","undertale","limbo","inside"

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
    "darkpictureshouseofashes","darkpicturesthedevilinme","amnesiarebirth"
    
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


        private async void NormalGameModeLoop()
        {
            while (normalGameModeRunning)
            {
                bool found = false;

                foreach (string game in gameExecutables)
                {
                    Process[] p = Process.GetProcessesByName(game);
                    if (p.Length > 0)
                    {
                        found = true;

                        if (currentGame != game)
                        {
                            currentGame = game;
                            ApplyNormalGameBoost(p[0]);

                            this.Invoke((Action)(() =>
                            {
                                lblGameModeStatus.Text = $"Game Mode: {game.ToUpper()} Running 🎮";
                                lblGameModeStatus.ForeColor = Color.Lime;
                            }));
                        }
                        break;
                    }
                }

                if (!found && currentGame != null)
                {
                    currentGame = null;

                    this.Invoke((Action)(() =>
                    {
                        lblGameModeStatus.Text = "Game Mode: Waiting for Game…";
                        lblGameModeStatus.ForeColor = Color.Gold;
                    }));
                }

                await Task.Delay(2000);
            }
        }

        private void ApplyNormalGameBoost(Process game)
        {
            try
            {
                game.PriorityClass = ProcessPriorityClass.High;

                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (!p.ProcessName.Equals(game.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                            !p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                            !p.ProcessName.StartsWith("System"))
                        {
                            p.PriorityClass = ProcessPriorityClass.BelowNormal;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }


        // ===============================
        // EMULATOR GAME MODE
        // ===============================
        private readonly string[] emulatorProcesses =
        {
    "HD-Player",        // BlueStacks / MSI App Player
    "dnplayer",         // LDPlayer
    "Nox",
    "MEmu",
    "AndroidEmulator"  // GameLoop
};

        private bool emulatorBoostRunning = false;


        private async Task EmulatorBoostLoopAsync()
        {
            while (emulatorBoostRunning)
            {
                foreach (string emu in emulatorProcesses)
                {
                    foreach (Process p in Process.GetProcessesByName(emu))
                    {
                        try
                        {
                            p.PriorityClass = ProcessPriorityClass.High;
                            p.ProcessorAffinity =
                                (IntPtr)((1 << Environment.ProcessorCount) - 1);
                        }
                        catch { }
                    }
                }

                await Task.Delay(2000);
            }
        }

        private void UpdateEmulatorStatus()
        {
            if (!tgAdvancedGame.Checked || !emulatorBoostRunning)
                return; // ✅ already good, DO NOTHING

            foreach (string emu in emulatorProcesses)
            {
                if (Process.GetProcessesByName(emu).Length > 0)
                {
                    lblGameModeStatus.Text = $"Game Mode Applied On {emu}";
                    lblGameModeStatus.ForeColor = Color.Lime;
                    return;
                }
            }

            lblGameModeStatus.Text = "Waiting for Emulator…";
            lblGameModeStatus.ForeColor = Color.Orange;
        }



        // ===============================
        // GENERAL
        // ===============================
        private ToolTip tip;

        // ===============================
        // SMOOTHING VARIABLES
        // ===============================
        // Higher value = faster/snappier, Lower value = smoother/slower
        private const float smoothing = 0.12f; 

        private float currentCpu = 0;
        private int targetCpu = 0;

        private float currentRam = 0;
        private int targetRam = 0;

        private float currentDrive = 0;
        private int targetDriveUsage = 0;

        // ===============================
        // CPU & SYSTEM
        // ===============================
        private PerformanceCounter cpuCounter;
        private DriveInfo systemDrive;
        private Timer animationTimer;

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
            public MEMORYSTATUSEX() { this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);


        private void LoadSavedSettings()
        {
            suppressMinimizeEvent = true;

            tgMinimizeToTray.Checked = Properties.Settings.Default.MinimizeToTray;
            tgReduceAnimations.Checked = Properties.Settings.Default.ReduceAnimations;

            suppressMinimizeEvent = false;
        }


        public Optimizer()
        {
            InitializeComponent();
            InitCounters();
            LoadSystemInfo();
            // ===============================
            // PING TIMER (1s)
            // ===============================
            pingTimer = new Timer();
            pingTimer.Interval = 1000; // 1 second
            pingTimer.Tick += PingTimer_Tick;
            pingTimer.Start();
            InitTray();
            tip = new ToolTip();
            systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            Updater.CheckAndUpdate();
            lblDriveCTitle.Text = $"{systemDrive.VolumeLabel} ({systemDrive.Name.TrimEnd('\\')})";
            // Main Data Fetch Timer (1 second)
            usageTimer.Interval = 1000;
            usageTimer.Tick += UsageTimer_Tick;
            usageTimer.Start();
            // High-Speed Animation Timer (16ms ~ 60 FPS)
            animationTimer = new Timer();
            animationTimer.Interval = 16;
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
            trayIconNormal = this.Icon;
            trayIconAlert = Properties.Resources.Icon;
            LoadSavedSettings();
            trayBlinkTimer = new Timer();
            trayBlinkTimer.Interval = 500; // blink speed (ms)
            trayBlinkTimer.Tick += (s, e) =>
            {
                trayBlinkState = !trayBlinkState;
                trayIcon.Icon = trayBlinkState ? trayIconAlert : trayIconNormal;
            };
            this.MaximumSize = this.Size;
            this.MinimumSize = this.Size;
            lblVersion.Text = Application.ProductVersion; // ✅ SAFE HERE TOO

        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MAXIMIZE = 0xF030;

            if (m.Msg == WM_SYSCOMMAND && (int)m.WParam == SC_MAXIMIZE)
            {
                return; // ❌ block maximize
            }

            base.WndProc(ref m);
        }

        private void StartTrayBlink()
        {
            trayIcon.Visible = true;   // 🔥 REQUIRED
            trayBlinkTimer.Start();
        }


        private void StopTrayBlink()
        {
            trayBlinkTimer.Stop();
            trayIcon.Icon = trayIconNormal;
        }



        private void InitTray()
        {
            trayMenu = new ContextMenuStrip();

            // Restore
            trayMenu.Items.Add("Show Optimizer", null, (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                trayIcon.Visible = false;

                SetAdminStatus("Restored from Tray", Color.Lime);
            });

            trayMenu.Items.Add(new ToolStripSeparator());

            // Exit
            trayMenu.Items.Add("Exit", null, (s, e) =>
            {
                allowExit = true;
                trayIcon.Visible = false;
                Application.Exit();
            });

            trayIcon = new NotifyIcon
            {
                Text = "Optimizer",
                Icon = this.Icon, // uses your app icon
                ContextMenuStrip = trayMenu,
                Visible = false
            };

            // Double-click to restore
            trayIcon.DoubleClick += (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                trayIcon.Visible = false;
            };
            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                    trayIcon.Visible = false;
                }
            };

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


        private void btnCleanNow_Click_1(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                bool anyChecked = chkTemp.Checked || chkWinTemp.Checked || chkPrefetch.Checked || chkBrowser.Checked || chkRecycle.Checked;
                try
                {
                    if (chkTemp.Checked) CleanFolder(Path.GetTempPath());
                    if (chkWinTemp.Checked) CleanFolder(@"C:\Windows\Temp");
                    if (chkPrefetch.Checked) CleanFolder(@"C:\Windows\Prefetch");
                    if (chkBrowser.Checked) CleanFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Google\Chrome\User Data\Default\Cache");
                    if (chkRecycle.Checked) SHEmptyRecycleBin(IntPtr.Zero, null, RecycleFlags.SHERB_NOCONFIRMATION | RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND);
                }
                catch { }

                this.Invoke((Action)(() =>
                {
                    if (anyChecked) ShowCleanPopup("Clean Completed ✔", Color.Lime);
                    else ShowCleanPopup("Nothing Selected ❌", Color.OrangeRed);

                    // ✅ RESET CHECKBOXES
                    ResetCleanerCheckboxes();

                }));
            });
        }

        private async void ShowCleanPopup(string message, Color color)
        {
            lblCleanStatus.Text = message;
            lblCleanStatus.ForeColor = color;
            lblCleanStatus.Visible = true;
            await Task.Delay(2000);
            for (int i = 100; i >= 0; i -= 5)
            {
                lblCleanStatus.ForeColor = Color.FromArgb(i, color.R, color.G, color.B);
                await Task.Delay(30);
            }
            lblCleanStatus.Visible = false;
            lblCleanStatus.ForeColor = color;
        }

        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);

        private void btnRamBoost_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        EmptyWorkingSet(p.Handle);
                    }
                    catch { }
                }

                this.Invoke((Action)(() =>
                {
                    ShowBoostPopup("RAM Boosted 🚀", Color.DeepSkyBlue);
                }));
            });
        }

        private void btnBgApps_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (!p.ProcessName.ToLower().Contains("system") &&
                            !p.ProcessName.ToLower().Contains("explorer"))
                        {
                            p.Kill();
                        }
                    }
                    catch { }
                }

                this.Invoke((Action)(() =>
                {
                    ShowBoostPopup("Background Apps Closed ✔", Color.Lime);
                }));
            });
        }

        private void btnHighPerf_Click(object sender, EventArgs e)
        {
            Process.Start("cmd.exe", "/c powercfg -setactive SCHEME_MIN");
            ShowBoostPopup("High Performance Enabled ⚡", Color.Orange);
        }

        private async void ShowBoostPopup(string message, Color color)
        {
            lblBoostStatus.Text = message;
            lblBoostStatus.ForeColor = color;
            lblBoostStatus.Visible = true;
            await Task.Delay(2000);
            for (int i = 100; i >= 0; i -= 5)
            {
                lblBoostStatus.ForeColor = Color.FromArgb(i, color.R, color.G, color.B);
                await Task.Delay(30);
            }
            lblBoostStatus.Visible = false;
            lblBoostStatus.ForeColor = color;
        }



        private void EnableAdvancedGameMode()
        {
            try
            {
                // 1. Ultimate Performance
                Process.Start("cmd.exe", "/c powercfg -setactive e9a42b02-d5df-448d-aa00-03f14749eb61");

                // 2. Disable CPU Throttling
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                    "PowerThrottlingOff",
                    1,
                    RegistryValueKind.DWord
                );

                // 3. Reduce visual effects
                Registry.SetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                    "VisualFXSetting",
                    2,
                    RegistryValueKind.DWord
                );

                // 4. Kill background apps (safe)
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (!p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                            !p.ProcessName.StartsWith("System"))
                        {
                            p.PriorityClass = ProcessPriorityClass.BelowNormal;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void DisableAdvancedGameMode()
        {
            try
            {
                // Balanced Power Plan
                Process.Start("cmd.exe", "/c powercfg -setactive SCHEME_BALANCED");

                // Enable CPU Throttling back
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                    "PowerThrottlingOff",
                    0,
                    RegistryValueKind.DWord
                );

                // Restore visuals
                Registry.SetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                    "VisualFXSetting",
                    0,
                    RegistryValueKind.DWord
                );
            }
            catch { }
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
                    GetWindowThreadProcessId(hwnd, out int pid);

                    Process fg = Process.GetProcessById(pid);
                    fg.PriorityClass = ProcessPriorityClass.High;

                    foreach (Process p in Process.GetProcesses())
                    {
                        try
                        {
                            if (p.Id != fg.Id &&
                                !p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                                !p.ProcessName.StartsWith("System"))
                            {
                                p.PriorityClass = ProcessPriorityClass.BelowNormal;
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                this.Invoke((Action)(() =>
                {
                    ShowBoostPopup("CPU Priority Boost Applied", Color.DeepSkyBlue);
                }));
            });
        }

        private void btnNetBoost_Click(object sender, EventArgs e)
        {
            // 🔒 HARD ADMIN CHECK FIRST
            if (!IsRunningAsAdmin())
            {
                ShowBoostPopup("Admin Rights Required ⚠", Color.Red);
                SetAdminStatus("Network Boost Failed (No Admin)", Color.Red);
                return;
            }

            try
            {
                // Network Throttling OFF
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "NetworkThrottlingIndex",
                    unchecked((int)0xFFFFFFFF),   // ✅ IMPORTANT
                    RegistryValueKind.DWord
                );

                // System responsiveness max
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "SystemResponsiveness",
                    0,
                    RegistryValueKind.DWord
                );

                ShowBoostPopup("Network Boost Enabled 🚀", Color.Lime);
                SetAdminStatus("Network Boost: ENABLED", Color.Lime);
            }
            catch (Exception ex)
            {
                // ❌ REAL ERROR (not admin related)
                ShowBoostPopup("Network Boost Failed ❌", Color.OrangeRed);
                SetAdminStatus("Network Boost Error", Color.OrangeRed);

                // OPTIONAL: debug only
                Debug.WriteLine(ex.Message);
            }
        }


        private void btnQuickFlush_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                this.Invoke((Action)(() =>
                {
                    ShowBoostPopup("Quick Memory Flush Done", Color.Lime);
                }));
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

        private void tgNormalGame_CheckedChanged(object sender, EventArgs e)
        {
            // ❌ Turn off Advanced Emulator Mode if running
            if (tgNormalGame.Checked && tgAdvancedGame.Checked)
            {
                tgAdvancedGame.Checked = false;
            }

            if (tgNormalGame.Checked)
            {
                normalGameModeRunning = true;
                currentGame = null;
                trayIcon.Visible = true;
                UpdateTrayBlinkState();
                // 🔥 NOW IT WILL BLINK

                Task.Run(() => NormalGameModeLoop());

                lblGameModeStatus.Text = "Normal Game Mode: ENABLED";
                lblGameModeStatus.ForeColor = Color.DeepSkyBlue;
            }
            else
            {
                normalGameModeRunning = false;
                currentGame = null;
                UpdateTrayBlinkState();
                lblGameModeStatus.Text = "Normal Game Mode: DISABLED";
                lblGameModeStatus.ForeColor = Color.Orange;
            }

        }

        private void tgAdvancedGame_CheckedChanged(object sender, EventArgs e)
        {
            // Prevent conflict with Normal Game Mode
            if (tgAdvancedGame.Checked && tgNormalGame.Checked)
            {
                tgNormalGame.Checked = false;
            }

            if (tgAdvancedGame.Checked)
            {
                emulatorBoostRunning = true;

                trayIcon.Visible = true;
                UpdateTrayBlinkState();                // 🔥 NOW IT WILL BLINK

                lblGameModeStatus.Text = "Waiting for Emulator…";
                lblGameModeStatus.ForeColor = Color.Gold;

                EnableAdvancedGameMode();

                // ✅ DO NOT AWAIT — run in background
                Task.Run(() => EmulatorBoostLoopAsync());
            }
            else
            {
                emulatorBoostRunning = false;

                UpdateTrayBlinkState();

                DisableAdvancedGameMode();

                lblGameModeStatus.Text = "Advanced Emulator Game Mode: DISABLED";
                lblGameModeStatus.ForeColor = Color.Orange;
            }
        }

        private void UpdateTrayBlinkState()
        {
            if (normalGameModeRunning || emulatorBoostRunning || bgAppBoostRunning)
            {
                StartTrayBlink();
            }
            else
            {
                StopTrayBlink();
            }
        }



        private void tgBgApps_CheckedChanged(object sender, EventArgs e)
        {
            if (tgBgApps.Checked)
            {
                bgAppBoostRunning = true;

                Task.Run(() => BackgroundAppsBoostLoop());

                lblGameModeStatus.Text = "Background Apps Disabled";
                lblGameModeStatus.ForeColor = Color.DeepSkyBlue;
            }
            else
            {
                bgAppBoostRunning = false;

                lblGameModeStatus.Text = "Background Apps Restored";
                lblGameModeStatus.ForeColor = Color.Orange;
            }
        }

        private void BackgroundAppsBoostLoop()
        {
            while (bgAppBoostRunning)
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        string name = p.ProcessName.ToLower();

                        // ❌ NEVER TOUCH SYSTEM / GAME / EMULATOR
                        if (name.Contains("system") ||
                            name.Contains("explorer") ||
                            name.Contains("svchost") ||
                            name.Contains("wininit") ||
                            name.Contains("winlogon"))
                            continue;

                        // Skip game & emulator processes
                        if (currentGame != null && name.Contains(currentGame.ToLower()))
                            continue;

                        foreach (string emu in emulatorProcesses)
                            if (name.Contains(emu.ToLower()))
                                goto SKIP;

                        p.PriorityClass = ProcessPriorityClass.BelowNormal;
                    }
                    catch { }

                SKIP:;
                }

                System.Threading.Thread.Sleep(3000); // every 3 sec
            }
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
    }
}
