using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spi
{
    public class StatusLineWriter
    {
        private int PrevTextLen = -1;

        private readonly TextWriter tw = Console.Error;
        private static readonly string Dots = "...";

        public void Write(string Text)
        {
            string BlanksToAppend = Text.Length < PrevTextLen ? new string(' ', PrevTextLen - Text.Length) : String.Empty;
            tw.Write("{0}{1}\r", Text, BlanksToAppend);
            PrevTextLen = Text.Length;
        }
        public void WriteWithDots(string Text)
        {
            int currWidth = Console.WindowWidth - 1;

            if (Text.Length > currWidth)
            {
                int LenLeftPart = (currWidth - Dots.Length) / 2;
                int LenRightPart = currWidth - Dots.Length - LenLeftPart;

                string TextToPrint = String.Format("{0}{1}{2}\r",
                    Text.Substring(0, LenLeftPart),
                    Dots,
                    Text.Substring(Text.Length - LenRightPart, LenRightPart)
                    );
                Write(TextToPrint);
            }
            else
            {
                Write(Text);
            }
        }
    }
}
