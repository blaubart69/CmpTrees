using System;
using System.IO;

namespace Spi
{
    public class StatusLineWriter
    {
        private readonly TextWriter tw = Console.Error;
        private static readonly string Dots = "...";

        private int  _PrevTextLen = -1;
        private bool _Console_available;

        public StatusLineWriter()
        {
            _Console_available = true;
            try
            {
                int x = Console.WindowWidth;
            }
            catch (Exception)
            {
                //Console.Error.WriteLine($"Could not format text to console. Seems Console is redirected. printing plain text. [{ex.Message}]");
                _Console_available = false;
            }
        }

        public void Write(string Text)
        {
            string TextToPrint = GetDottedText(Text);

            string BlanksToAppend = TextToPrint.Length < _PrevTextLen 
                    ? new string(' ', _PrevTextLen - TextToPrint.Length) 
                    : String.Empty;

            tw.Write("{0}{1}\r", TextToPrint, BlanksToAppend);

            _PrevTextLen = TextToPrint.Length;
        }
        private string GetDottedText(string Text)
        {
            if (!_Console_available)
            {
                return Text;
            }

            int currWidth = Console.WindowWidth - 1;
            string formattedText = Text;

            if (Text.Length > currWidth)
            {
                int lenToPrint   = currWidth - Dots.Length;
                int LenLeftPart  = lenToPrint / 2;
                int LenRightPart = lenToPrint - LenLeftPart;

                formattedText = String.Format("{0}{1}{2}",
                    Text.Substring(0, LenLeftPart),
                    Dots,
                    Text.Substring(Text.Length - LenRightPart, LenRightPart)
                    );
            }

            return formattedText;
        }
    }
}
