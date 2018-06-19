// https://codereview.stackexchange.com/questions/20260/rle-encoding-decoding-tool-review-source

namespace PersistenceList
{
    /*
    * GNU General Public License version 2 (GPLv2) http://rle.codeplex.com/license
    * Oleg Orlov, 2013 (c), RLE encoding/decoding tool, version 1.0.1 (v1.01)
    * 
    * C#, .NET 2.0 by default. It could be upgraded to any version of the .NET framework.
    * I have downgraded the .NET version only for the compatibility aims
    * and for the easy reproduction of the program to any other language.
    * 
    * http://rle.codeplex.com/
    */

    using System;
    using System.Globalization;
    using System.Text;

    public enum EncodingFormat
    {
        Old,
        New,
    }

    public static class RLE
    {
        private static bool HasChar(StringBuilder input)
        {
            for (var i = 0; i < input.Length; i++)
            {
                if (char.IsLetter(input[i]))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string Encode(string input, EncodingFormat format)
        {
            var runLengthEncodedString = new StringBuilder();
            var baseString = input;

            switch (format)
            {
                case EncodingFormat.New:
                    for (var i = 0; i < baseString.Length; i++)
                    {
                        var symbol = baseString[i];
                        var count = 1;

                        for (var j = i; j < baseString.Length - 1; j++)
                        {
                            if (baseString[j + 1] != symbol)
                            {
                                break;
                            }

                            count++;
                            i++;
                        }

                        if (count == 1)
                        {
                            runLengthEncodedString.Append(symbol);
                        }
                        else
                        {
                            runLengthEncodedString.Append(count.ToString(CultureInfo.InvariantCulture) + symbol);
                        }
                    }

                    break;
                case EncodingFormat.Old:
                    for (var i = 0; i < baseString.Length; i++)
                    {
                        var symbol = baseString[i];
                        var count = 1;

                        for (var j = i; j < baseString.Length - 1; j++)
                        {
                            if (baseString[j + 1] != symbol)
                            {
                                break;
                            }

                            count++;
                            i++;
                        }

                        runLengthEncodedString.Append(count.ToString(CultureInfo.InvariantCulture) + symbol);
                    }

                    break;
            }

            return runLengthEncodedString.ToString();
        }

        internal static string Decode(string input)
        {
            var runLengthEncodedString = new StringBuilder();
            var baseString = input;

            var radix = 0;

            for (var i = 0; i < baseString.Length; i++)
            {
                if (char.IsNumber(baseString[i]))
                {
                    radix++;
                }
                else
                {
                    if (radix > 0)
                    {
                        var valueRepeat = Convert.ToInt32(baseString.Substring(i - radix, radix));

                        for (var j = 0; j < valueRepeat; j++)
                        {
                            runLengthEncodedString.Append(baseString[i]);
                        }

                        radix = 0;
                    }
                    else if (radix == 0)
                    {
                        runLengthEncodedString.Append(baseString[i]);
                    }
                }
            }

            if (!HasChar(runLengthEncodedString))
            {
                throw new Exception("\r\nCan't to decode! Input string has the wrong syntax. There isn't any char (e.g. 'a'->'z') in your input string, there was/were only number(s).\r\n");
            }

            return runLengthEncodedString.ToString();
        }

        internal static double GetPercentage(double x, double y)
        {
            return (100 * (x - y)) / x;
        }
    }
/*
    internal static class Program
    {
        private const string Welcome = "\r\nRLE encoding/decoding tool, Oleg Orlov 2013(c).";

        private const string Notice = "\r\nPlease, use the next syntax: <action> <string>\r\n(e.g. \"encode my_string\" or \"decode my_string\").\r\n\r\nWarning! The 2nd parameter (the string for encoding/decoding)\r\nmust not content any whitespaces!\r\n\r\nYou may also use the option \"-old\" to encode your string\r\n(e.g. \"encode my_string -old\") in such way, where before\r\nsingle char inserting the value: '1' (e.g. \"abbcddd\" -> \"1a2b1c3d\").";

        private static void EncodeString(string unencodedString, EncodingFormat format)
        {
            var encodedString = Rle.Encode(unencodedString, format);

            switch (format)
            {
                case EncodingFormat.New:
                    Console.WriteLine(
                        "\r\nBase string ({0} chars): {1}\r\nAfter RLE-encoding ({2} chars): {3}\r\nCompression percentage: %{4}",
                        unencodedString.Length,
                        unencodedString,
                        encodedString.Length,
                        encodedString,
                        Rle.GetPercentage(unencodedString.Length, encodedString.Length));
                    break;
                case EncodingFormat.Old:
                    Console.WriteLine(
                        "\r\nBase string ({0} chars): {1}\r\nAfter RLE-encoding with the \"-old\" option ({2} chars): {3}\r\nCompression percentage: %{4}",
                        unencodedString.Length,
                        unencodedString,
                        encodedString.Length,
                        encodedString,
                        Rle.GetPercentage(unencodedString.Length, encodedString.Length));
                    break;
            }
        }

        private static void DecodeString(string encodedString)
        {
            var decodedString = Rle.Decode(encodedString);

            Console.WriteLine(
                "\r\nBase string ({0} chars): {1}\r\nAfter RLE-decoding ({2} chars): {3}\r\nDecompression percentage: %{4}",
                encodedString.Length,
                encodedString,
                decodedString.Length,
                decodedString,
                Math.Abs(Rle.GetPercentage(encodedString.Length, decodedString.Length)));
        }

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                Console.WriteLine(Welcome);

                if (args.Length > 1)
                {
                    switch (args[0])
                    {
                        case "encode":
                            if (args.Length == 3)
                            {
                                if (args[2] == "-old")
                                {
                                    EncodeString(args[1], EncodingFormat.Old);
                                }
                            }
                            else
                            {
                                EncodeString(args[1], EncodingFormat.New);
                            }

                            break;
                        case "decode":
                            DecodeString(args[1]);
                            break;
                        default:
                            throw new Exception("\r\nThere are only two methods: encode (with the \"-old\" option), decode. No other actions are available.\r\n"
                                                 + Notice + "\r\n");
                    }

                    return 0;
                }

                Console.WriteLine(Notice);
                return 1;
            }
            catch (Exception exc)
            {
                Console.WriteLine("\r\n{0}", exc);
                return 1;
            }
        }
    }

*/
}
