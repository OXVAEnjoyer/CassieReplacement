namespace CassieReplacement.Commands
{
    using System;
    using System.Globalization;
    using CassieReplacement.Reader;
    using CommandSystem;

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ListWordsCommand : ICommand
    {
        public string Command => "listwords";

        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description => "Lists all registered CUSTOMCASSIE words. With words as arguments, prints how long they take to say. (usage: listwords [words])";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            CustomCassieReader reader = CustomCassieReader.Singleton;
            if (reader == null)
            {
                response = "CASSIE Replacement is not enabled.";
                return false;
            }

            if (arguments.Count > 0)
            {
                string[] words = new string[arguments.Count];
                Array.Copy(arguments.Array, arguments.Offset, words, 0, arguments.Count);
                response = reader.MeasureDuration(words).ToString("0.##", CultureInfo.InvariantCulture) + " s";
                return true;
            }

            response = "The available words are:\n" + string.Join(", ", reader.ClipDatabase.GetListableClipNames());
            return true;
        }
    }
}
