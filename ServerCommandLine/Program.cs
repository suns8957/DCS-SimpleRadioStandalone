using System.Runtime;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using CommandLine;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Sentry;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server;

internal class Program
{
    private readonly EventAggregator _eventAggregator = new();
    private ServerState _serverState;

    public Program()
    {
        SentrySdk.Init("https://0935ffeb7f9c46e28a420775a7f598f4@o414743.ingest.sentry.io/5315043");
        SetupLogging();
    }

    private static void Main(string[] args)
    {
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(ProcessArgs);
    }

    private static void ProcessArgs(Options options)
    {
        Console.WriteLine($"Settings: \n{options}");

        var p = new Program();
        new Thread(() => { p.StartServer(options); }).Start();

        var waitForProcessShutdownStart = new ManualResetEventSlim();
        var waitForMainExit = new ManualResetEventSlim();

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            // We got a SIGTERM, signal that graceful shutdown has started
            waitForProcessShutdownStart.Set();

            Console.WriteLine("Shutting down gracefully...");
            // Don't unwind until main exists
            waitForMainExit.Wait();
        };

        Console.WriteLine("Waiting for shutdown SIGTERM");
        // Wait for shutdown to start
        waitForProcessShutdownStart.Wait();

        // This is where the application performs graceful shutdown
        p.StopServer();

        Console.WriteLine("Shutdown complete");
        // Now we're done with main, tell the shutdown handler
        waitForMainExit.Set();
    }

    private void StopServer()
    {
        _serverState.StopServer();
    }


    public void StartServer(Options options)
    {
        ServerSettingsStore.Instance.SetServerSetting(ServerSettingsKeys.SERVER_PORT, options.Port);

        var profileSettings = GlobalSettingsStore.Instance.ProfileSettingsStore;

        profileSettings.SetClientSettingBool(ProfileSettingsKeys.NATOTone, options.FMRadioTone);
        profileSettings.SetClientSettingBool(ProfileSettingsKeys.RadioEffects, options.RadioEffect);
        profileSettings.SetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping, options.RadioEffect);
        profileSettings.SetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume, options.FMRadioToneVolume);

        profileSettings.SetClientSettingFloat(ProfileSettingsKeys.FMNoiseVolume, options.FMRadioEffectVolume);
        profileSettings.SetClientSettingFloat(ProfileSettingsKeys.HFNoiseVolume, options.HFRadioEffectVolume);
        profileSettings.SetClientSettingFloat(ProfileSettingsKeys.UHFNoiseVolume, options.UHFRadioEffectVolume);
        profileSettings.SetClientSettingFloat(ProfileSettingsKeys.VHFNoiseVolume, options.VHFRadioEffectVolume);

        profileSettings.SetClientSettingBool(ProfileSettingsKeys.RadioEffects, options.RadioEffect);

        profileSettings.SetClientSettingBool(ProfileSettingsKeys.RadioBackgroundNoiseEffect,
            options.RadioBackgroundEffects);
        
        _serverState = new ServerState(_eventAggregator);
    }

    private void SetupLogging()
    {
        // If there is a configuration file then this will already be set
        if (LogManager.Configuration != null) return;

        var config = new LoggingConfiguration();
        var fileTarget = new FileTarget
        {
            FileName = "serverlog.txt",
            ArchiveFileName = "serverlog.old.txt",
            MaxArchiveFiles = 1,
            ArchiveAboveSize = 104857600,
            Layout =
                @"${longdate} | ${logger} | ${message} ${exception:format=toString,Data:maxInnerExceptionLevel=1}"
        };

        var wrapper = new AsyncTargetWrapper(fileTarget, 5000, AsyncTargetWrapperOverflowAction.Discard);
        config.AddTarget("asyncFileTarget", wrapper);
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, wrapper));

        LogManager.Configuration = config;
    }
}

public class Options
{
    [Option('p', "port",
        HelpText = "Port - 5002 is the default",
        Default = 5002,
        Required = false)]
    public int Port { get; set; }

    [Option('f', "recordingFreqs",
        HelpText = "Frequency in MHz comma separated 121.5,122.3,152 - if set, recording is enabled ",
        Required = false)]
    public string RecordingFrequencies { get; set; }

    [Option("audioClippingEffect",
        HelpText =
            "Audio Clipping Effect",
        Default = true,
        Required = false)]
    public bool RadioClipping { get; set; }

    [Option("radioEffect",
        HelpText =
            "Radio Effect - if not enabled other effects are ignored",
        Default = true,
        Required = false)]
    public bool RadioEffect { get; set; }

    [Option("fmRadioTone",
        HelpText =
            "FM Radio Tone",
        Default = true,
        Required = false)]
    public bool FMRadioTone { get; set; }

    [Option("fmRadioToneVolume",
        HelpText =
            "FM Radio Tone Volume",
        Default = 1.2f,
        Required = false)]
    public float FMRadioToneVolume { get; set; }

    [Option("radioBackgroundEffects",
        HelpText =
            "Background radio effects - UHF/VHF/HF and background Aircraft or ground noise",
        Default = true,
        Required = false)]
    public bool RadioBackgroundEffects { get; set; }

    [Option("uhfRadioEffectVolume",
        HelpText =
            "UHF Radio Effect Volume",
        Default = 0.15f,
        Required = false)]
    public float UHFRadioEffectVolume { get; set; }

    [Option("vhfRadioEffectVolume",
        HelpText =
            "VHF Radio Effect Volume",
        Default = 0.15f,
        Required = false)]
    public float VHFRadioEffectVolume { get; set; }

    [Option("hfRadioEffectVolume",
        HelpText =
            "HF Radio Effect Volume",
        Default = 0.15f,
        Required = false)]
    public float HFRadioEffectVolume { get; set; }

    [Option("fmRadioEffectVolume",
        HelpText =
            "FM Radio Effect Volume",
        Default = 0.4f,
        Required = false)]
    public float FMRadioEffectVolume { get; set; }
    
    public override string ToString()
    {
        return
            $"{nameof(RecordingFrequencies)}: {RecordingFrequencies}, \n" +
            $"{nameof(RadioClipping)}: {RadioClipping}, \n" +
            $"{nameof(RadioEffect)}: {RadioEffect}, \n" +
            $"{nameof(FMRadioTone)}: {FMRadioTone}, \n" +
            $"{nameof(FMRadioToneVolume)}: {FMRadioToneVolume}, \n" +
            $"{nameof(RadioBackgroundEffects)}: {RadioBackgroundEffects}, \n" +
            $"{nameof(UHFRadioEffectVolume)}: {UHFRadioEffectVolume}, \n" +
            $"{nameof(VHFRadioEffectVolume)}: {VHFRadioEffectVolume}, \n" +
            $"{nameof(HFRadioEffectVolume)}: {HFRadioEffectVolume}, \n" +
            $"{nameof(FMRadioEffectVolume)}: {FMRadioEffectVolume}, \n";
    }
}