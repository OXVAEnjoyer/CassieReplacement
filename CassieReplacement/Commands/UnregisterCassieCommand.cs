using CassieReplacement.Reader;
using CommandSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CassieReplacement.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class UnregisterCassieCommand : ICommand
    {
        public string Command => "unregistercassie";

        public string[] Aliases => new string[] { "customcassieunregister", "unregistercc", "unregister" };

        public string Description => "De-registers all custom CASSIE lines.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            CustomCassieReader.Singleton.ClipDatabase.UnregisterClips();
            response = "Unregistered clips.";
            return true;
        }
    }
}
