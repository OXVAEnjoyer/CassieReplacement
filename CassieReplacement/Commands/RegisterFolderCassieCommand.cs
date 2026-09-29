namespace CassieReplacement.Commands
{
    using System;
    using System.Globalization;
    using System.IO;
    using CassieReplacement.Reader;
    using CassieReplacement.Reader.Models;
    using CommandSystem;

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class RegisterFolderCassieCommand : ICommand
    {
        public string Command => "registercassie";

        public string[] Aliases { get; } = { "customcassieregister", "registercc", "register" };

        public string Description => "Registers a specific folder of CASSIE lines. (usage: registercassie <path> [bleed] [prefix])";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.ServerConsoleCommands, out response))
            {
                return false;
            }

            CustomCassieReader reader = CustomCassieReader.Singleton;
            if (reader == null)
            {
                response = "CASSIE Replacement is not enabled.";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "Not enough arguments. Usage: registercassie <path> [bleed] [prefix]";
                return false;
            }

            string path = CassiePaths.Resolve(GetArgument(arguments, 0));
            if (!Directory.Exists(path))
            {
                response = $"Directory not found: {path}";
                return false;
            }

            float bleedTime = 0f;
            if (arguments.Count > 1 && !float.TryParse(GetArgument(arguments, 1), NumberStyles.Float, CultureInfo.InvariantCulture, out bleedTime))
            {
                response = $"Invalid bleed time: {GetArgument(arguments, 1)}";
                return false;
            }

            string prefix = arguments.Count > 2 ? GetArgument(arguments, 2) : string.Empty;
            CassieDirectorySerializable directory = new CassieDirectorySerializable { Path = path, BleedTime = bleedTime, Prefix = prefix };

            _ = reader.ClipDatabase.RegisterFolderAsync(directory);

            response = $"Registering cassie directory, path {path}, prefix {prefix}, bleed {bleedTime.ToString(CultureInfo.InvariantCulture)}";
            return true;
        }

        private static string GetArgument(ArraySegment<string> arguments, int index) => arguments.Array[arguments.Offset + index];
    }
}
