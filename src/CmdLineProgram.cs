using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using dragonrescue;
using dragonrescue.Api;
using dragonrescue.Util;
using dragonrescue.Schema;
using System.CommandLine;
using System.Reflection;
using System.Diagnostics;

class Program {
    enum ImportModes {
        dragons, stables, inventory, avatar, hideout, farm
    }
    
    enum ImportRoomModes {
        auto, replace, add
    }
    
    static async Task<int> Main(string[] args) {
        Assembly assembly = Assembly.GetExecutingAssembly();
        var informationalVersionAttribute = assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().FirstOrDefault();

        var rootCommand = new RootCommand(string.Format(
            "SoD account data import/export tool (version {0}).\n\nSee `{1} command --help` for command details.",
            informationalVersionAttribute.InformationalVersion, System.AppDomain.CurrentDomain.FriendlyName
        ));
        
        /// global options
        
        rootCommand.AddGlobalOption(
            new Option<string?>(
                name: "--userApiUrl",
                description: "SoD user API URL, for example:\n" + 
                " \"http://localhost:5000\" for local hosted SoDOff (with default settings)\n" + 
                " \"http://localhost:5321\" for local hosted Project Edge (with default settings)",
                parseArgument: result => {
                    Config.URL_USER_API = result.Tokens.Single().Value;
                    return Config.URL_USER_API;
                }
            ) {IsRequired = true}
        );
        rootCommand.AddGlobalOption(
            new Option<string?>(
                name: "--contentApiUrl",
                description: "SoD content API URL, for example:\n" + 
                " \"http://localhost:5000\" for local hosted SoDOff (with default settings)\n" + 
                " \"http://localhost:5320\" for local hosted Project Edge (with default settings)",
                parseArgument: result => {
                    Config.URL_CONT_API = result.Tokens.Single().Value;
                    return Config.URL_CONT_API;
                }
            ) {IsRequired = true}
        );
        
        var loginUser = new Option<string?>(
            name: "--username",
            description: "Login username"
        ) {IsRequired = true};
        var loginPassword = new Option<string?>(
            name: "--password",
            description: "Login password"
        ) {IsRequired = true};
        var loginViking = new Option<string?>(
            name: "--viking",
            description: "Viking (in-game) name / sub profile name"
        ) {IsRequired = true};
        
        rootCommand.AddGlobalOption(loginUser);
        rootCommand.AddGlobalOption(loginPassword);
        rootCommand.AddGlobalOption(loginViking);
        
        // import command
        
        var inputFile = new Option<string?>(
            name: "--file",
            description: "Input file (dragons or stables) - see --mode option for details.",
            parseArgument: result => {
                string? filePath = result.Tokens.Single().Value;
                if (!File.Exists(filePath)) {
                    result.ErrorMessage = "File does not exist";
                    return null;
                } else {
                    return filePath;
                }
            }
        ) {IsRequired = true};
        
        var importMode = new Option<ImportModes>(
            name: "--mode",
            description: 
                "Import mode:\n" +
                " * dragons (default) – import dragons and stables (if available).\n" +
                "   --file option argument is path to GetAllActivePetsByuserId.xml file from dragonrescue* dump\n"+
                "   (e.g. '../../mydragons/eba07882-0ae8-4965-9c39-07f409a1c415-GetAllActivePetsByuserId.xml')\n" +
                " * stables – only stables will be imported – can be used to organise / change order of stables.\n" +
                "   --file option argument is path to Stables.xml file from dragonrescue-import dump.\n" +
                " * inventory – only viking inventory, stables will be omitting until provide --stables-mode=add option\n" +
                "   --file option argument is path to GetCommonInventory.xml file from dragonrescue* dump.\n" +
                "   WARNING: item will be added, not replaced! So repeated use import multiply items quantity.\n" +
                "   WARNING: this is experimental feature, it can broke your account easily\n" +
                "   WARNING: importing battle backpack not working correctly\n" +
                " * avatar – only viking avatar data\n" +
                "   --file option argument is path to VikingProfileData.xml or GetDetailedChildList.xml file from dragonrescue* dump.\n" +
                "   if file contain multiple viking's profiles, then will imported profile with name provided by --import-name\n" +
                " * hideout – only viking hideout data\n" +
                "   --file option argument is path to GetUserItemPositions_MyRoomINT.xml file from dragonrescue-import dump.\n" +
                " * farm – only viking farm data\n" +
                "   --file option argument is path to GetUserRoomList.xml file from dragonrescue* dump.\n",
            getDefaultValue: () => ImportModes.dragons
        );
        
        var importRoomMode = new Option<ImportRoomModes>(
            aliases: new[] {"--stables-mode", "--room-mode"},
            description: 
                "Specifies the mode of importing stables / farm rooms - adding new ones or replacing existing ones.\n"+
                "Note: farm room replace does *not* delete rooms that are not in imported data, only reuse old room when possible.\n"+
                "Default is auto: replace for --mode=stable or --mode=farm, add for --mode=dragons\n",
            getDefaultValue: () => ImportRoomModes.auto
        );

        var importName = new Option<string?>(
            name: "--import-name",
            description: "Viking (in-game) name / sub profile name to import (used with --mode=avatar). When not set use value of --viking."
        );
        
        var skipInventory = new Option<bool>(
            name: "--skip-inventory",
            description: "Skip inventory update on hideout and farm import."
        );
        
        var importCommand = new Command("import", "Import profile into SoD.") {
            inputFile,
            importMode,
            importRoomMode,
            importName,
            skipInventory,
        };
        importCommand.SetHandler(
            async (username, password, viking, mode, roomMode, path, importName, skipInventory) => {
                switch (mode) {
                    case ImportModes.dragons:
                        await Importers.ImportDragons(username, password, viking, path, (roomMode == ImportRoomModes.replace));
                        break;
                    case ImportModes.stables:
                        await Importers.ImportOnlyStables(username, password, viking, path, (roomMode == ImportRoomModes.auto || roomMode == ImportRoomModes.replace));
                        break;
                    case ImportModes.inventory:
                        await Importers.ImportInventory(username, password, viking, path, (roomMode != ImportRoomModes.add));
                        break;
                    case ImportModes.avatar:
                        if (importName == null)
                            importName = viking;
                        await Importers.ImportAvatar(username, password, viking, path, importName);
                        break;
                    case ImportModes.hideout:
                        await Importers.ImportHideout(username, password, viking, path, !skipInventory);
                        break;
                    case ImportModes.farm:
                        await Importers.ImportFarm(username, password, viking, path, (roomMode == ImportRoomModes.auto || roomMode == ImportRoomModes.replace), !skipInventory);
                        break;
                }
            },
            loginUser, loginPassword, loginViking, importMode, importRoomMode, inputFile, importName, skipInventory
        );
        rootCommand.AddCommand(importCommand);
        
        
        // export command
        
        var outDir = new Option<string?>(
            name: "--path",
            description: "Path to directory to write data",
            parseArgument: result => {
                string? dirPath = result.Tokens.Single().Value;
                if (!Directory.Exists(dirPath)) {
                    Directory.CreateDirectory(dirPath);
                }
                return dirPath;
            }
        ) {IsRequired = true};
        
        var exportCommand = new Command("export", "Export profile from SoD.") {
            outDir,
        };
        exportCommand.SetHandler(
            async (username, password, viking, path) => {
                await Exporters.Export(username, password, viking, path);
            },
            loginUser, loginPassword, loginViking, outDir
        );
        rootCommand.AddCommand(exportCommand);

        return await rootCommand.InvokeAsync(args);
    }
}
