namespace CassieReplacement.Commands
{
    using System;
    using CassieReplacement.Reader;
    using CommandSystem;

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class UnregisterCassieCommand : ICommand
    {
        public string Command => "unregistercassie";

        public string[] Aliases { get; } = { "customcassieunregister", "unregistercc", "unregister" };

        public string Description => "De-registers all custom CASSIE lines.";

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

            // Wyczyszczenie bazy zmienia Version -> cache próbek unieważnia się automatycznie.
            reader.CancelAll();
            reader.ClipDatabase.UnregisterClips();
            response = "Unregistered clips.";
            return true;
        }
    }
}
