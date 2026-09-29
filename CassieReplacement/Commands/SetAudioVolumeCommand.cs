namespace CassieReplacement.Commands
{
    using System;
    using System.Globalization;
    using CommandSystem;

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class SetAudioVolumeCommand : ICommand
    {
        public string Command => "customcassievolume";

        public string[] Aliases { get; } = { "cassievolume", "ccassievolume", "ccvolume" };

        public string Description => "Sets the volume of the custom CASSIE speakers.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.ServerConsoleCommands, out response))
            {
                return false;
            }

            if (Plugin.Singleton == null)
            {
                response = "CASSIE Replacement is not enabled.";
                return false;
            }

            // FIX: 'words.First()' rzucało InvalidOperationException przy braku argumentu (+ zbędna kopia do List).
            // FIX: InvariantCulture, odrzucamy NaN/Infinity/wartości ujemne.
            if (arguments.Count == 0
                || !float.TryParse(arguments.Array[arguments.Offset], NumberStyles.Float, CultureInfo.InvariantCulture, out float volume)
                || float.IsNaN(volume)
                || float.IsInfinity(volume)
                || volume < 0f)
            {
                response = "Usage: customcassievolume <non-negative number>";
                return false;
            }

            Plugin.Singleton.Config.CassieVolume = volume;
            response = $"CASSIE volume set to {volume.ToString(CultureInfo.InvariantCulture)}";
            return true;
        }
    }
}
