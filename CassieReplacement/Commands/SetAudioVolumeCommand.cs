namespace CassieReplacement.Commands
{
    using CassieReplacement.Reader;
    using CommandSystem;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class SetAudioVolumeCommand : ICommand
    {
        public string Command => "customcassievolume";

        public string[] Aliases => new string[] { "cassievolume", "ccassievolume", "ccvolume" };

        public string Description => "Sets the volume of the custom CASSIE speakers.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            List<string> words = arguments.ToList();

            string firstarg = words.First();
            if (float.TryParse(firstarg, out float vol))
            {
                Plugin.Singleton.Config.CassieVolume = vol;
                response = $"CASSIE volume set to {vol}";
                return true;
            }

            response = "No volume provided.";
            return false;
        }
    }
}
