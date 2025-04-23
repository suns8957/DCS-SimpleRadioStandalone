using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network;

internal class UDPPositionHandler
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ConcurrentDictionary<string, SRClientBase> _clientsList;

    private readonly BlockingCollection<OutgoingUDPPackets>
        _outGoing = new();

    private readonly CancellationTokenSource _outgoingCancellationToken = new();

    private readonly CancellationTokenSource _pendingProcessingCancellationToken = new();

    private readonly BlockingCollection<PendingPacket> _pendingProcessingPackets =
        new();

    private readonly ServerSettingsStore _serverSettings = ServerSettingsStore.Instance;
    private UdpClient _listener;

    private volatile bool _stop;

    public UDPPositionHandler(ConcurrentDictionary<string, SRClientBase> clientsList, IEventAggregator eventAggregator)
    {
        _clientsList = clientsList;
    }


    public void Listen()
    {
        //start threads
        //packets that need processing
        new Thread(ProcessPackets).Start();
        //outgoing packets
        new Thread(SendPendingPackets).Start();

        var port = _serverSettings.GetServerPort() + 1;
        _listener = new UdpClient();
        try
        {
            _listener.AllowNatTraversal(true);
        }
        catch
        {
        }

        _listener.DontFragment = true;
        _listener.Client.DontFragment = true;
        _listener.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        while (!_stop)
            try
            {
                var groupEP = new IPEndPoint(IPAddress.Any, 0);
                var rawBytes = _listener.Receive(ref groupEP);

                if (rawBytes?.Length == 22)
                    try
                    {
                        //lookup guid here
                        //22 bytes are guid!
                        var guid = Encoding.ASCII.GetString(
                            rawBytes, 0, 22);

                        if (_clientsList.ContainsKey(guid))
                        {
                            var client = _clientsList[guid];
                            client.VoipPort = groupEP;

                            //send back ping UDP
                            _listener.Send(rawBytes, rawBytes.Length, groupEP);
                        }
                    }
                    catch (Exception ex)
                    {
                        //dont log because it slows down thread too much...
                    }
                else if (rawBytes?.Length > 22)
                    _pendingProcessingPackets.Add(new PendingPacket
                    {
                        RawBytes = rawBytes,
                        ReceivedFrom = groupEP
                    });
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error receving audio UDP for client " + e.Message);
            }

        try
        {
            _listener.Close();
        }
        catch (Exception e)
        {
        }
    }

    public void RequestStop()
    {
        _stop = true;
        try
        {
            _listener.Close();
        }
        catch (Exception e)
        {
        }

        _outgoingCancellationToken.Cancel();
        _pendingProcessingCancellationToken.Cancel();
    }

    private void ProcessPackets()
    {
        while (!_stop)
            try
            {
                PendingPacket udpPacket = null;
                _pendingProcessingPackets.TryTake(out udpPacket, 100000, _pendingProcessingCancellationToken.Token);

                if (udpPacket != null)
                {
                    //last 22 bytes are guid!
                    var guid = Encoding.ASCII.GetString(
                        udpPacket.RawBytes, udpPacket.RawBytes.Length - 22, 22);

                    if (_clientsList.ContainsKey(guid))
                    {
                        var client = _clientsList[guid];
                        client.VoipPort = udpPacket.ReceivedFrom;


                        if (client.Muted)
                        {
                            // IGNORE THE AUDIO
                        }
                        else
                        {
                            try
                            {
                                //decode
                                var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(udpPacket.RawBytes);

                                if (udpVoicePacket != null)
                                {
                                    var outgoingVoice = GenerateOutgoingPacket(udpVoicePacket, udpPacket, client);

                                    if (outgoingVoice != null)
                                    {
                                        //Add to the processing queue
                                        _outGoing.Add(outgoingVoice);

                                        //mark as transmitting for the UI
                                        var mainFrequency = udpVoicePacket.Frequencies.FirstOrDefault();
                                        // Only trigger transmitting frequency update for "proper" packets (excluding invalid frequencies)
                                        if (mainFrequency > 0)
                                        {
                                            var mainModulation = (Modulation)udpVoicePacket.Modulations[0];
                                            if (mainModulation == Modulation.INTERCOM)
                                                client.TransmittingFrequency = "INTERCOM";
                                            else
                                                client.TransmittingFrequency =
                                                    $"{(mainFrequency / 1000000).ToString("0.000", CultureInfo.InvariantCulture)} {mainModulation}";

                                            client.LastTransmissionReceived = DateTime.Now;
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                //Hide for now, slows down loop to much....
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info("Failed to Process UDP Packet: " + ex.Message);
            }
    }

    private
        void SendPendingPackets()
    {
        //_listener.Send(bytes, bytes.Length, ip);
        while (!_stop)
            try
            {
                OutgoingUDPPackets udpPacket = null;
                _outGoing.TryTake(out udpPacket, 100000, _pendingProcessingCancellationToken.Token);

                if (udpPacket != null)
                {
                    var bytes = udpPacket.ReceivedPacket;
                    var bytesLength = bytes.Length;
                    foreach (var outgoingEndPoint in udpPacket.OutgoingEndPoints)
                        try
                        {
                            _listener.Send(bytes, bytesLength, outgoingEndPoint);
                        }
                        catch (Exception ex)
                        {
                            //dont log, slows down too much...
                        }
                }
            }
            catch (Exception ex)
            {
                Logger.Info("Error processing Sending Queue UDP Packet: " + ex.Message);
            }
    }

    private OutgoingUDPPackets GenerateOutgoingPacket(UDPVoicePacket udpVoice, PendingPacket pendingPacket,
        SRClientBase fromClient)
    {
        var outgoingList = new HashSet<IPEndPoint>();

        var guid = fromClient.ClientGuid;

        foreach (var client in _clientsList)
            if (!client.Key.Equals(guid))
            {
                var ip = client.Value.VoipPort;
                if (ip != null) outgoingList.Add(ip);
            }

        if (outgoingList.Count > 0)
            return new OutgoingUDPPackets
            {
                OutgoingEndPoints = outgoingList.ToList(),
                ReceivedPacket = pendingPacket.RawBytes
            };
        return null;
    }
}