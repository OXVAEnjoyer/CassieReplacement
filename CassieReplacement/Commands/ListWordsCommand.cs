namespace CassieReplacement.Commands
{
    using CassieReplacement.Reader;
    using CommandSystem;
    using System;

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ListWordsCommand : ICommand
    {
        public string Command => "listwords";

        public string[] Aliases => new string[] { "listwords" };

        public string Description => "Lists all registered CUSTOMCASSIE words.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count != 0)
            {
                response = $"{NineTailedFoxAnnouncer.singleton.CalculateDuration(arguments.At(0))}";
                return true;
            }

            string words = "The available words are:\n";
            foreach (string word in CustomCassieReader.Singleton.ClipDatabase.ListableClipNames)
            {
                words += $"{word}, ";
            }

            response = words;
            return true;
        }
    }
}
