using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings.RadioChannels;

public class FilePresetChannelsStore : IPresetChannelsStore
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public IEnumerable<PresetChannel> LoadFromStore(string radioName)
    {
        var file = FindRadioFile(NormaliseString(radioName));

        if (file != null) return ReadFrequenciesFromFile(file);

        return new List<PresetChannel>();
    }

    private List<PresetChannel> ReadFrequenciesFromFile(string filePath)
    {
        var channels = new List<PresetChannel>();
        var lines = File.ReadAllLines(filePath);

        var channel = 1;
        const double MHz = 1000000;
        if (lines?.Length > 0)
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                    try
                    {
                        var frequencyText = "";
                        string channelName = null;
                        //spilt on | 
                        if (line.Contains("|"))
                        {
                            var spilt = line.Split('|');
                            frequencyText = spilt[1].Trim();
                            channelName = spilt[0].Trim();
                        }

                        var frequency = double.Parse(frequencyText, CultureInfo.InvariantCulture);
                        channels.Add(new PresetChannel
                        {
                            Text = channelName ?? frequencyText, //use channel name if not null
                            Value = frequency * MHz,
                            Channel = channel++
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Info, "Error parsing frequency  ");
                    }
            }

        return channels;
    }

    private string FindRadioFile(string radioName)
    {
        var files = Directory.GetFiles(Environment.CurrentDirectory);

        foreach (var fileAndPath in files)
        {
            var name = Path.GetFileNameWithoutExtension(fileAndPath);

            if (NormaliseString(name) == radioName) return fileAndPath;
        }

        return null;
    }

    private string NormaliseString(string str)
    {
        //only allow alphanumeric, remove all spaces etc
        return Regex.Replace(str, "[^a-zA-Z0-9]", "").ToLower();
    }
}