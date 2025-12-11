namespace LANCommander.Server.Logging
{
    public static class TerminalColor
    {
        public static string Black         { get { return GetControlCode(TerminalColorCode.Black); } }
        public static string Red           { get { return GetControlCode(TerminalColorCode.Red); } }
        public static string Green         { get { return GetControlCode(TerminalColorCode.Green); } }
        public static string Yellow        { get { return GetControlCode(TerminalColorCode.Yellow); } }
        public static string Blue          { get { return GetControlCode(TerminalColorCode.Blue); } }
        public static string Magenta       { get { return GetControlCode(TerminalColorCode.Magenta); } }
        public static string Cyan          { get { return GetControlCode(TerminalColorCode.Cyan); } }
        public static string White         { get { return GetControlCode(TerminalColorCode.White); } }
        public static string BrightBlack   { get { return GetControlCode(TerminalColorCode.BrightBlack); } }
        public static string BrightRed     { get { return GetControlCode(TerminalColorCode.BrightRed); } }
        public static string BrightGreen   { get { return GetControlCode(TerminalColorCode.BrightGreen); } }
        public static string BrightYellow  { get { return GetControlCode(TerminalColorCode.BrightYellow); } }
        public static string BrightBlue    { get { return GetControlCode(TerminalColorCode.BrightBlue); } }
        public static string BrightMagenta { get { return GetControlCode(TerminalColorCode.BrightMagenta); } }
        public static string BrightCyan    { get { return GetControlCode(TerminalColorCode.BrightCyan); } }
        public static string BrightWhite   { get { return GetControlCode(TerminalColorCode.BrightWhite); } }
        public static string Default       { get { return GetControlCode(TerminalColorCode.Default); } }

        private static string GetControlCode(TerminalColorCode code)
        {
            return $"\u001b[{(int)code}m";
        }
    }

    public enum TerminalColorCode
    {
        Default = 0,
        Black = 30,
        Red = 31,
        Green = 32,
        Yellow = 33,
        Blue = 34,
        Magenta = 35,
        Cyan = 36,
        White = 37,
        BrightBlack = 90,
        BrightRed = 91,
        BrightGreen = 92,
        BrightYellow = 93,
        BrightBlue = 94,
        BrightMagenta = 95,
        BrightCyan = 96,
        BrightWhite = 97
    }
}