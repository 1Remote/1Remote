using CommandLine;

namespace PRM
{
    public class AppOptions
    {
        [Option('r', "start-session-by-id", Required = false, HelpText = "Start session by id")]
        public int StartId { get; set; }

        [Option('d', "debug", Required = false, HelpText = "Show debug info.")]
        public bool IsDebug { get; set; }
    }
}
