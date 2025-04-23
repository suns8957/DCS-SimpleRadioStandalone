using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.FS3D.SimpleRadio.Standalone.ExternalAudioClient.Audio;
using Ciribob.SRS.Common.Network.Client;
using Ciribob.SRS.Common.Network.Models;
using Ciribob.SRS.Common.Network.Models.EventMessages;
using Ciribob.SRS.Common.Network.Singletons;
using NLog;
using LogManager = NLog.LogManager;
using Timer = Ciribob.FS3D.SimpleRadio.Standalone.ExternalAudioClient.Timers.Timer;

namespace Ciribob.FS3D.SimpleRadio.Standalone.ExternalAudioClient.Client;

internal class ExternalAudioClient : IHandle<TCPClientStatusMessage>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly byte[] encryptionBytes;

    private readonly CancellationTokenSource finished = new();

    private readonly double[] freq;

    private readonly string Guid = ShortGuid.NewGuid();
    private readonly Modulation[] modulation;
    private readonly byte[] modulationBytes;
    private readonly Program.Options opts;
    private readonly IPEndPoint endPoint;
    private PlayerUnitStateBase gameState;
    private UDPVoiceHandler udpVoiceHandler;

    public ExternalAudioClient(double[] freq, Modulation[] modulation, Program.Options opts)
    {
        this.freq = freq;
        this.modulation = modulation;
        this.opts = opts;
        modulationBytes = new byte[modulation.Length];
        for (var i = 0; i < modulationBytes.Length; i++) modulationBytes[i] = (byte)modulation[i];

        encryptionBytes = new byte[modulation.Length];
        for (var i = 0; i < encryptionBytes.Length; i++) encryptionBytes[i] = 0;

        EventBus.Instance.SubscribeOnUIThread(this);

        var resolvedAddresses = Dns.GetHostAddresses(opts.Server);
        var ip = resolvedAddresses.FirstOrDefault(xa =>
            xa.AddressFamily ==
            AddressFamily
                .InterNetwork); // Ensure we get an IPv4 address in case the host resolves to both IPv6 and IPv4

        endPoint = new IPEndPoint(ip, opts.Port);
    }

    public Task HandleAsync(TCPClientStatusMessage message, CancellationToken cancellationToken)
    {
        if (message.Connected)
            ReadyToSend();
        else
            Disconnected();

        return Task.CompletedTask;
    }

    public void Start()
    {
        gameState = new PlayerUnitStateBase();
        gameState.Name = opts.Name;
        gameState.UnitId = 100000000;
        gameState.Radios = new List<RadioBase>();
        gameState.Radios.Add(new RadioBase
        {
            Modulation = Modulation.DISABLED
        });
        gameState.Radios.Add(new RadioBase
        {
            Modulation = modulation[0],
            Freq = freq[0]
        });

        Logger.Info("Starting with params:");
        for (var i = 0; i < freq.Length; i++) Logger.Info($"Frequency: {freq[i]} Hz - {modulation[i]} ");


        var srsClientSyncHandler = new TCPClientHandler(Guid, gameState);

        srsClientSyncHandler.TryConnect(endPoint);

        //wait for it to end
        finished.Token.WaitHandle.WaitOne();
        Logger.Info("Finished - Closing");

        udpVoiceHandler?.RequestStop();
        srsClientSyncHandler?.Disconnect();
    }

    private void ReadyToSend()
    {
        if (udpVoiceHandler == null)
        {
            Logger.Info($"Connecting UDP VoIP {endPoint}");
            udpVoiceHandler = new UDPVoiceHandler(Guid, endPoint);
            udpVoiceHandler.Connect();
            new Thread(SendAudio).Start();
        }
    }

    private void Disconnected()
    {
        finished.Cancel();
    }

    private void SendAudio()
    {
        Logger.Info("Sending Audio... Please Wait");
        var audioGenerator = new AudioGenerator(opts);
        var opusBytes = audioGenerator.GetOpusBytes();
        var count = 0;

        var tokenSource = new CancellationTokenSource();

        uint _packetNumber = 1;
        //get all the audio as Opus frames of 40 ms
        //send on 40 ms timer 

        //when empty - disconnect
        //user timer for accurate sending
        var _timer = new Timer(() =>
        {
            if (!finished.IsCancellationRequested)
            {
                if (count < opusBytes.Count)
                {
                    var udpVoicePacket = new UDPVoicePacket
                    {
                        AudioPart1Bytes = opusBytes[count],
                        AudioPart1Length = (ushort)opusBytes[count].Length,
                        Frequencies = freq,
                        UnitId = 100000,
                        Encryptions = encryptionBytes,
                        Modulations = modulationBytes,
                        PacketNumber = _packetNumber++
                    };

                    udpVoiceHandler.Send(udpVoicePacket);
                    count++;

                    if (count % 50 == 0)
                        Logger.Info(
                            $"Playing audio - sent {count * 40}ms - {count / (float)opusBytes.Count * 100.0:F0}% ");
                }
                else
                {
                    tokenSource.Cancel();
                }
            }
            else
            {
                Logger.Error("Client Disconnected");
                tokenSource.Cancel();
            }
        }, TimeSpan.FromMilliseconds(40));
        _timer.Start();

        //wait for cancel
        tokenSource.Token.WaitHandle.WaitOne();
        _timer.Stop();

        Logger.Info("Finished Sending Audio");
        finished.Cancel();
    }
}