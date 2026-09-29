using CassieReplacement.Reader;
using CommandSystem;
using LabApi.Features.Wrappers;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CassieReplacement.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ClearCustomCassieCommand : ICommand
    {
        public string Command => "clearcustomcassie";

        public string[] Aliases => new string[] { "customcassieclear", "clearcc", "clearcustom" };

        public string Description => "Clears custom CASSIE lines.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            global::Cassie.CassieAnnouncementDispatcher.ClearAll();
            CustomCassieReader.Singleton.TimeBeforeWhichToPause = DateTime.Now;
            CustomCassieReader.Singleton.StopAllPlayback();

            response = "Cleared cassie.";
            return true;
        }
    }
}
