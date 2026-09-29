using CassieReplacement.Reader;
using CassieReplacement.Reader.Models;
using CommandSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CassieReplacement.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class RegisterFolderCassieCommand : ICommand
    {
        public string Command => "registercassie";

        public string[] Aliases => new string[] { "customcassieregister", "registercc", "register" };

        public string Description => "Registers a specific folder of CASSIE lines. (usage: register (path) (bleed) (prefix)";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count == 0)
            {
                response = "Not enough arguments.";
                return false;
            }

            string path = CassiePaths.Resolve(arguments.At(0));
            float bleedTime = 0f;
            float.TryParse(arguments.Count > 1 ? arguments.At(1) : "0", out bleedTime);
            string prefix = arguments.Count > 2 ? arguments.At(2) : string.Empty;
            CassieDirectorySerializable cassieDirectory = new CassieDirectorySerializable() { Path = path, BleedTime = bleedTime, Prefix = prefix };
            Task.Run(() => CustomCassieReader.Singleton.ClipDatabase.RegisterFolder(cassieDirectory));
            response = $"Registered cassie directory, path {path}, prefix {prefix}, bleed {bleedTime}";
            return true;
        }
    }
}
