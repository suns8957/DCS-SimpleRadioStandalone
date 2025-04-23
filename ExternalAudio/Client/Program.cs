using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using Ciribob.SRS.Common.Network.Models;
using CommandLine;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Ciribob.FS3D.SimpleRadio.Standalone.ExternalAudioClient.Client;

public class Program
{
    private const int SW_MINIMIZE = 6;

    [DllImport("Kernel32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);

    private static void MinimizeConsoleWindow()
    {
        var hWndConsole = GetConsoleWindow();
        ShowWindow(hWndConsole, SW_MINIMIZE);
    }

    private static void ConfigureLogging()
    {
        // If there is a configuration file then this will already be set
        if (LogManager.Configuration != null) return;

        var config = new LoggingConfiguration();
        var logconsole = new ConsoleTarget("logconsole");
        logconsole.Layout = "${longdate}|${level:uppercase=true}|${message}";
        config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);

        LogManager.Configuration = config;
    }

    public static void Main(string[] args)
    {
        ConfigureLogging();

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(ProcessArgs).WithNotParsed(HandleParseError);
    }

    private static void ProcessArgs(Options opts)
    {
        if (opts.Minimise) MinimizeConsoleWindow();

        //process freqs
        var freqStr = opts.Freqs.Split(',');

        var freqDouble = new List<double>();
        foreach (var s in freqStr) freqDouble.Add(double.Parse(s, CultureInfo.InvariantCulture) * 1000000d);

        var modulationStr = opts.Modulations.Split(',');

        var modulation = new List<Modulation>();
        foreach (var s in modulationStr)
        {
            Modulation mod;
            if (Enum.TryParse(s.Trim().ToUpper(), out mod)) modulation.Add(mod);
        }

        if (modulation.Count != freqDouble.Count)
        {
            Console.WriteLine(
                $"Number of frequencies ({freqDouble.Count}) does not match number of modulations ({modulation.Count}) - They must match!" +
                "\n\nFor example: --freq=251.0,252.0 --modulations=AM,AM ");
            Console.WriteLine("QUITTING!");
        }

        var client = new ExternalAudioClient(freqDouble.ToArray(), modulation.ToArray(), opts);
        client.Start();
    }

    private static void HandleParseError(IEnumerable errs)
    {
        Console.WriteLine("");
        Console.WriteLine(
            "Example:\n --file=\"C:\\FULL\\PATH\\TO\\File.mp3\" --freqs=251.0 --modulations=AM --port=5002 --name=\"FS3D-robot\" --volume=0.5");
        Console.WriteLine(
            "Example:\n --text=\"I want this read out over this frequency - hello world! \" --freqs=251.0 --modulations=AM --port=5002 --name=\"FS3D-robot\" --volume=0.5");
        Console.WriteLine(
            "Example:\n --text=\"I want this read out over TWO frequencies - hello world! \" --freqs=251.0,252.0 --modulations=AM,AM --port=5002 --name=\"FS3D-robot\" --volume=0.5");

        Console.WriteLine("");
        Console.WriteLine("Currently compatible voices on this system: \n");
        var synthesizer = new SpeechSynthesizer();
        foreach (var voice in synthesizer.GetInstalledVoices())
            if (voice.Enabled)
                Console.WriteLine(
                    $"Name: {voice.VoiceInfo.Name}, Culture: {voice.VoiceInfo.Culture},  Gender: {voice.VoiceInfo.Gender}, Age: {voice.VoiceInfo.Age}, Desc: {voice.VoiceInfo.Description}");

        Console.WriteLine("");

        var first = synthesizer.GetInstalledVoices().First();
        Console.WriteLine(
            $"Example:\n --text=\"I want a specific voice \" --freqs=251.0 --modulations=AM  --voice=\"{first.VoiceInfo.Name}\"");

        Console.WriteLine(
            "Example:\n --text=\"I want any female voice \" --freqs=251.0 --modulations=AM  --gender=female");

        Console.WriteLine(
            "Example:\n --text=\"I want any female voice at a location \" --freqs=251.0 --modulations=AM  --gender=female --latitude=50.82653 --longitude=-0.15210 --altitude=20");

        Console.WriteLine(
            "Example:\n --text=\"I want any male voice \" --freqs=251.0 --modulations=AM  --gender=male");

        Console.WriteLine("");
        Console.WriteLine(
            "Google Cloud Text to Speech Examples - see locale and voices https://cloud.google.com/text-to-speech/docs/voices  : \n");

        Console.WriteLine(
            "Example:\n --text=\"Ahoj, jak se máš - Specific Czech voice\" --freqs=251.0 --modulations=AM  --googleCredentials=\"C:\\\\folder\\\\credentials.json\" --voice=\"cs-CZ-Wavenet-A\"");

        Console.WriteLine(
            "Example:\n --text=\"I want any female voice \" --freqs=251.0 --modulations=AM  --gender=female --googleCredentials=\"C:\\\\folder\\\\credentials.json\" ");

        Console.WriteLine(
            "Example:\n --text=\"I want any male voice \" --freqs=251.0 --modulations=AM  --gender=male --googleCredentials=\"C:\\\\folder\\\\credentials.json\" ");

        Console.WriteLine(
            "Example:\n --text=\"I want any male voice with a French accent \" --freqs=251.0 --modulations=AM  --gender=male --locale=fr-FR --googleCredentials=\"C:\\\\folder\\\\credentials.json\" ");

        Console.WriteLine(
            "Example:\n --text=\"I want any female voice with a German accent \" --freqs=251.0 --modulations=AM  --gender=male --locale=de-DE --googleCredentials=\"C:\\\\folder\\\\credentials.json\" ");
    }

    public class Options
    {
        [Option('i', "file",
            SetName = "file",
            HelpText = "Full path to MP3 - File must end .mp3",
            Required = true)]
        public string File { get; set; }

        [Option('t', "text",
            HelpText = "Text to say",
            SetName = "TTS",
            Required = true)]
        public string Text { get; set; }

        [Option('z', "ssml",
            HelpText = "Text is SSML - this is only for Google TTS",
            SetName = "TTS",
            Default = false,
            Required = false)]
        public bool SSML { get; set; }

        [Option('I', "textFile",
            SetName = "textFile",
            HelpText = "Path to text file for TTS ",
            Required = true)]
        public string TextFile { get; set; }

        [Option('f', "freqs",
            HelpText = "Frequency in MHz comma separated - 251.0,252.0 or just 252.0 ",
            Required = true)]
        public string Freqs { get; set; }


        [Option('m', "modulations",
            HelpText = "Modulation AM or FM comma separated - AM,FM or just AM  ",
            Required = true)]
        public string Modulations { get; set; }


        [Option('s', "speed",
            Default = 1,
            HelpText = "Speed - 1 is normal -10 to 10 is the range",
            Required = false)]
        public int speed { get; set; }

        [Option('p', "port",
            HelpText = "Port - 5002 is the default",
            Default = 5002,
            Required = false)]
        public int Port { get; set; }

        [Option('n', "name",
            HelpText = "Name - name of your transmitter - no spaces",
            Default = "FS3D-STTS",
            Required = false)]
        public string Name { get; set; }

        [Option('v', "volume",
            HelpText = "Volume - 1.0 is max, 0.0 is silence",
            Default = 1.0f,
            Required = false)]
        public float Volume { get; set; }

        [Option('l', "culture",
            HelpText = "TTS culture - local for the voice",
            Required = false,
            Default = "en-GB")]
        public string Culture { get; set; }

        [Option('g', "gender",
            HelpText = "TTS Gender - male/female",
            Required = false,
            Default = "female")]
        public string Gender { get; set; }

        [Option('V', "voice",
            HelpText =
                "The voice NAME - see the list from --help or if using google see: https://cloud.google.com/text-to-speech/docs/voices ",
            Required = false)]
        public string Voice { get; set; }

        [Option('h', "minimise",
            HelpText = "Minimise the command line window on run",
            Required = false,
            Default = false)]
        public bool Minimise { get; set; }

        [Option('G', "googleCredentials",
            HelpText =
                "Full path to Google JSON Credentials file - see https://cloud.google.com/text-to-speech/docs/quickstart-client-libraries",
            Required = false)]
        public string GoogleCredentials { get; set; }

        [Option('S', "server",
            HelpText =
                "Address of the server - if not supplied localhost is assumed",
            Default = "127.0.0.1",
            Required = false)]
        public string Server { get; set; }
    }
}