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

            string path = CassiePaths.Resolve(Arg(arguments, 0));

            // FIX: komenda nie tworzy już katalogów na dysku serwera (Resolve wcześniej robiło Directory.CreateDirectory).
            if (!Directory.Exists(path))
            {
                response = $"Directory not found: {path}";
                return false;
            }

            // FIX: parsowanie niezależne od kultury systemu (na serwerze z przecinkiem "0.5" dawało 0).
            float bleedTime = 0f;
            if (arguments.Count > 1 && !float.TryParse(Arg(arguments, 1), NumberStyles.Float, CultureInfo.InvariantCulture, out bleedTime))
            {
                response = $"Invalid bleed time: {Arg(arguments, 1)}";
                return false;
            }

            string prefix = arguments.Count > 2 ? Arg(arguments, 2) : string.Empty;
            CassieDirectorySerializable directory = new CassieDirectorySerializable { Path = path, BleedTime = bleedTime, Prefix = prefix };

            // THREAD SAFETY: ClipDatabase jest bezpieczna wątkowo (snapshot), a wyjątki są logowane.
            _ = reader.ClipDatabase.RegisterFolderAsync(directory);

            response = $"Registering cassie directory in background, path {path}, prefix {prefix}, bleed {bleedTime}";
            return true;
        }

        private static string Arg(ArraySegment<string> arguments, int index) => arguments.Array[arguments.Offset + index];
    }
}
