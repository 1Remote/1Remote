using System;
using System.IO;
using Microsoft.Win32;
using Newtonsoft.Json;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using Shawn.Utils;
using _1RM.Utils.KiTTY.Model;
using _1RM.Model.ProtocolRunner.Default;

namespace _1RM.Utils.KiTTY
{
    public interface IKittyConnectable
    {
        /// <summary>
        /// Allowing implementing interface only for specific class 'ProtocolBase'
        /// </summary>
        [JsonIgnore]
        ProtocolBase ProtocolBase { get; }
        string ExternalKittySessionConfigPath { get; set; }
    }

    public static class PuttyConnectableExtension
    {
        /*
        [Obsolete]
        public static void ConfigKitty(this IKittyConnectable iKittyConnectable, string sessionName, KittyRunner kittyRunner, string sshPrivateKeyPath)
        {
            // install kitty if `kittyRunner.PuttyExePath` not exists
            if (string.IsNullOrEmpty(kittyRunner.ExePath) || File.Exists(kittyRunner.ExePath) == false)
            {
                kittyRunner.ExePath = KittyConfig.InstallKitty();
            }
            KittyConfig.WriteKittyDefaultConfig(kittyRunner.ExePath);

            // create session config
            var puttyOption = new KittyConfig(sessionName);
            puttyOption.Set(EnumKittyConfigKey.LineCodePage, kittyRunner.GetLineCodePageForIni());
            puttyOption.ApplyOverwriteSession(iKittyConnectable.ExternalKittySessionConfigPath);

            if (iKittyConnectable is SSH server)
            {
                if (!string.IsNullOrEmpty(sshPrivateKeyPath))
                {
                    // set key
                    puttyOption.Set(EnumKittyConfigKey.PublicKeyFile, sshPrivateKeyPath);
                }
                puttyOption.Set(EnumKittyConfigKey.HostName, server.Address);
                puttyOption.Set(EnumKittyConfigKey.PortNumber, server.GetPort());
                puttyOption.Set(EnumKittyConfigKey.Protocol, "ssh");
            }
            if (iKittyConnectable is Serial serial)
            {
                puttyOption.Set(EnumKittyConfigKey.BackspaceIsDelete, 0);
                puttyOption.Set(EnumKittyConfigKey.LinuxFunctionKeys, 4);

                //SerialLine\COM1\
                //SerialSpeed\9600\
                //SerialDataBits\8\
                //SerialStopHalfbits\2\
                //SerialParity\0\
                //SerialFlowControl\1\
                puttyOption.Set(EnumKittyConfigKey.Protocol, "serial");
                puttyOption.Set(EnumKittyConfigKey.SerialLine, serial.SerialPort);
                puttyOption.Set(EnumKittyConfigKey.SerialSpeed, serial.GetBitRate());
                puttyOption.Set(EnumKittyConfigKey.SerialDataBits, serial.DataBits);
                puttyOption.Set(EnumKittyConfigKey.SerialStopHalfbits, serial.StopBits);
                puttyOption.Set(EnumKittyConfigKey.SerialParity, serial.Parity);
                puttyOption.Set(EnumKittyConfigKey.SerialFlowControl, serial.FlowControl);
            }

            // set theme
            var options = PuttyThemes.Themes[kittyRunner.PuttyThemeName];
            foreach (var option in options)
            {
                try
                {
                    if (Enum.TryParse(option.Key, out EnumKittyConfigKey key))
                    {
                        if (option.ValueKind == RegistryValueKind.DWord)
                            puttyOption.Set(key, (int)(option.Value));
                        else
                            puttyOption.Set(key, (string)option.Value);
                    }
                }
                catch (Exception)
                {
                    SimpleLogHelper.Warning($"Putty theme error: can't set up key(value)=> {option.Key}({option.ValueKind})");
                }
            }

            puttyOption.Set(EnumKittyConfigKey.FontHeight, kittyRunner.PuttyFontSize);
            puttyOption.SaveToKittyConfig(kittyRunner.ExePath);
        }
        */

        public static void ConfigPutty(this IKittyConnectable iKittyConnectable, string sessionName, PuttyRunner puttyRunner, string sshPrivateKeyPath)
        {
            // install PUTTY if `puttyRunner.PuttyExePath` not exists
            if (string.IsNullOrEmpty(puttyRunner.ExePath) || File.Exists(puttyRunner.ExePath) == false)
            {
                puttyRunner.Install();
            }

            // create session config
            var puttyOption = new PuttyConfig(sessionName);
            puttyOption.Set(EnumKittyConfigKey.LineCodePage, puttyRunner.GetLineCodePageForIni());
            puttyOption.ApplyOverwriteSession(iKittyConnectable.ExternalKittySessionConfigPath);

            if (iKittyConnectable is SSH server)
            {
                if (!string.IsNullOrEmpty(sshPrivateKeyPath))
                {
                    // set key
                    puttyOption.Set(EnumKittyConfigKey.PublicKeyFile, sshPrivateKeyPath);
                }
                puttyOption.Set(EnumKittyConfigKey.HostName, server.Address);
                puttyOption.Set(EnumKittyConfigKey.PortNumber, server.GetPort());
                puttyOption.Set(EnumKittyConfigKey.Protocol, "ssh");
            }
            if (iKittyConnectable is Serial serial)
            {
                puttyOption.Set(EnumKittyConfigKey.BackspaceIsDelete, 0);
                puttyOption.Set(EnumKittyConfigKey.LinuxFunctionKeys, 4);

                //SerialLine\COM1\
                //SerialSpeed\9600\
                //SerialDataBits\8\
                //SerialStopHalfbits\2\
                //SerialParity\0\
                //SerialFlowControl\1\
                puttyOption.Set(EnumKittyConfigKey.Protocol, "serial");
                puttyOption.Set(EnumKittyConfigKey.SerialLine, serial.SerialPort);
                puttyOption.Set(EnumKittyConfigKey.SerialSpeed, serial.GetBitRate());
                puttyOption.Set(EnumKittyConfigKey.SerialDataBits, serial.DataBits);
                puttyOption.Set(EnumKittyConfigKey.SerialStopHalfbits, serial.StopBits);
                puttyOption.Set(EnumKittyConfigKey.SerialParity, serial.Parity);
                puttyOption.Set(EnumKittyConfigKey.SerialFlowControl, serial.FlowControl);
            }

            // set theme
            var options = PuttyThemes.Themes[puttyRunner.PuttyThemeName];
            foreach (var option in options)
            {
                try
                {
                    if (Enum.TryParse(option.Key, out EnumKittyConfigKey key))
                    {
                        if (option.ValueKind == RegistryValueKind.DWord)
                            puttyOption.Set(key, (int)(option.Value));
                        else
                            puttyOption.Set(key, (string)option.Value);
                    }
                }
                catch (Exception)
                {
                    SimpleLogHelper.Warning($"Putty theme error: can't set up key(value)=> {option.Key}({option.ValueKind})");
                }
            }

            puttyOption.Set(EnumKittyConfigKey.FontHeight, puttyRunner.PuttyFontSize);
            puttyOption.SaveToConfig(puttyRunner.ExePath);
        }
    }
}
