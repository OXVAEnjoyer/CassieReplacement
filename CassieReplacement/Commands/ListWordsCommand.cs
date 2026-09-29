namespace CassieReplacement.Commands
{
    using System;
    using CassieReplacement.Reader;
    using CommandSystem;

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ListWordsCommand : ICommand
    {
        public string Command => "listwords";

        // FIX: alias identyczny z nazwą komendy był zbędny.
        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description => "Lists all registered CUSTOMCASSIE words.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            CustomCassieReader reader = CustomCassieReader.Singleton;
            if (reader == null)
            {
                response = "CASSIE Replacement is not enabled.";
                return false;
            }

            // Usunięto ukrytą funkcję debugową (listwords <słowo> zwracało szacowany czas trwania) - nie była opisana nigdzie.
            // OPTYMALIZACJA: string.Join zamiast konkatenacji w pętli.
            response = "The available words are:\n" + string.Join(", ", reader.ClipDatabase.GetListableClipNames());
            return true;
        }
    }
}
