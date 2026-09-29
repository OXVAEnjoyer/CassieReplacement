namespace CassieReplacement.Commands
{
    using System;
    using CassieReplacement.Playback;
    using CommandSystem;

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ClearCustomCassieCommand : ICommand
    {
        public string Command => "clearcustomcassie";

        public string[] Aliases { get; } = { "customcassieclear", "clearcc", "clearcustom" };

        public string Description => "Clears custom CASSIE lines.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            // FIX: brak jakiegokolwiek sprawdzenia uprawnień w oryginale - komenda RA była dostępna dla każdego z dostępem do panelu.
            if (!sender.CheckPermission(PlayerPermissions.ServerConsoleCommands, out response))
            {
                return false;
            }

            // Jedno wywołanie czyści kolejkę gry i własną kolejkę audio (bez DateTime.Now).
            CassiePlayback.ClearAll();
            response = "Cleared cassie.";
            return true;
        }
    }
}
