//-----------------------------------------------------------------------------
// Filename: Program.cs
//
// Description: A getting started program to demonstrate how to use the SIPSorcery
// library to place a call.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 13 Oct 2019	Aaron Clauson	Created, Dublin, Ireland.
// 31 Dec 2019  Aaron Clauson   Changed from an OPTIONS example to a call example.
// 20 Feb 2020  Aaron Clauson   Switched to RtpAVSession and simplified.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Serilog;
using Serilog.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions.V1;
using SIPSorceryMedia.Windows;

namespace demo
{
    class Program
    {
        // ringcentral
        //private static string DOMAIN = "sip.devtest.ringcentral.com";
        //private static string DESTINATION = $"sip:16152828751@{DOMAIN}";
        //private static SIPEndPoint OUTBOUND_PROXY = SIPEndPoint.ParseSIPEndPoint($"udp:{Dns.GetHostAddresses("sip112-101.devtest.ringcentral.com")[0]}:8083");
        //private static string USERNAME = "14242600481*101";
        //private static string AUTHORIZATIONID = "801558731004";
        //private static string PASSWORD = "K0PXaz6Xx";

        // goto meeting
        private static string DOMAIN = "reg.jiveip.net";
        private static string DESTINATION = $"sip:17639576300@{DOMAIN}";
        private static SIPEndPoint OUTBOUND_PROXY = SIPEndPoint.ParseSIPEndPoint($"udp:{Dns.GetHostAddresses("optum.jive.rtcfront.net")[0]}");
        private static string USERNAME = "1YCtR1pCeEDADa8WfenJA9WTKzSDeA";
        private static string AUTHORIZATIONID = null;
        private static string PASSWORD = "6pEEciNH9ivUOUpn";

        private static readonly WaveFormat _waveFormat = new WaveFormat(8000, 16, 1);
        private static WaveFileWriter _waveFile = new WaveFileWriter("output.mp3", _waveFormat);

        static async Task Main()
        {
            Console.WriteLine("SIPSorcery Getting Started Demo");

            AddConsoleLogger();
            CancellationTokenSource exitCts = new CancellationTokenSource();

            var sipTransport = new SIPTransport();

            EnableTraceLogs(sipTransport);

            var userAgent = new SIPUserAgent(sipTransport, OUTBOUND_PROXY);
            userAgent.ClientCallFailed += (uac, error, sipResponse) => Console.WriteLine($"Call failed {error}.");
            userAgent.OnCallHungup += (dialog) => exitCts.Cancel();

            var windowsAudio = new WindowsAudioEndPoint(new AudioEncoder());
            var voipMediaSession = new VoIPMediaSession(windowsAudio.ToMediaEndPoints());
            voipMediaSession.AcceptRtpFromAny = true;
            voipMediaSession.OnRtpPacketReceived += OnRtpPacketReceived;
            
            string fromHeader = (new SIPFromHeader(USERNAME, new SIPURI(USERNAME, DOMAIN, null), null)).ToString();
            SIPCallDescriptor callDescriptor = new SIPCallDescriptor(USERNAME, PASSWORD, DESTINATION, fromHeader, null, null, null, null, SIPCallDirection.Out, SDP.SDP_MIME_CONTENTTYPE, null, null);
            //callDescriptor.CallId = "16152412565";
            //callDescriptor.AuthUsername = USERNAME;

            // Place the call and wait for the result.
            var callTask = userAgent.Call(callDescriptor, voipMediaSession);

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;

                if (userAgent != null)
                {
                    if (userAgent.IsCalling || userAgent.IsRinging)
                    {
                        Console.WriteLine("Cancelling in progress call.");
                        userAgent.Cancel();
                    }
                    else if (userAgent.IsCallActive)
                    {
                        Console.WriteLine("Hanging up established call.");
                        userAgent.Hangup();
                        _waveFile.Dispose();
                    }
                };

                exitCts.Cancel();
            };

            Console.WriteLine("press ctrl-c to exit...");

            bool callResult = await callTask;

            
            if (callResult)
            {
                Console.WriteLine("Enter digits one after another");
                string meetingNo = "1711622132";
                Console.WriteLine("Enter meetingno ?");
                Console.ReadLine();
                foreach (var item in meetingNo)
                {
                    await userAgent.SendDtmf(byte.Parse(item.ToString()));
                    Console.WriteLine("Sending DTMF - " + byte.Parse(item.ToString()));
                    Thread.Sleep(2000);
                }
                Thread.Sleep(2000);
                await userAgent.SendDtmf(Encoding.ASCII.GetBytes("#")[0]);

                Thread.Sleep(13000);
                Console.WriteLine("Sending AttendeeID ?");
                /*string attendeeId = "635619";
                foreach (var item in attendeeId)
                {
                    await userAgent.SendDtmf(byte.Parse(item.ToString()));
                    Console.WriteLine("Sending DTMF - " + byte.Parse(item.ToString()));
                    Thread.Sleep(2000);
                }*/
                await userAgent.SendDtmf(Encoding.ASCII.GetBytes("#")[0]);
                Console.ReadLine();
                await userAgent.SendDtmf(Encoding.ASCII.GetBytes("#")[0]);
                Console.WriteLine($"Call to {DESTINATION} succeeded.");
                exitCts.Token.WaitHandle.WaitOne();
            }
            else
            {
                Console.WriteLine($"Call to {DESTINATION} failed.");
            }

            Console.WriteLine("Exiting...");

            if(userAgent?.IsHangingUp == true)
            {
                Console.WriteLine("Waiting 1s for the call hangup or cancel to complete...");
                await Task.Delay(1000);
            }

            // Clean up.
            sipTransport.Shutdown();
        }

        /// <summary>
        /// Enable detailed SIP log messages.
        /// </summary>
        private static void EnableTraceLogs(SIPTransport sipTransport)
        {
            sipTransport.SIPRequestInTraceEvent += (localEP, remoteEP, req) =>
            {
                Console.WriteLine($"Request received: {localEP}<-{remoteEP}");
                Console.WriteLine(req.ToString());
            };

            sipTransport.SIPRequestOutTraceEvent += (localEP, remoteEP, req) =>
            {
                Console.WriteLine($"Request sent: {localEP}->{remoteEP}");
                Console.WriteLine(req.ToString());
            };

            sipTransport.SIPResponseInTraceEvent += (localEP, remoteEP, resp) =>
            {
                Console.WriteLine($"Response received: {localEP}<-{remoteEP}");
                Console.WriteLine(resp.ToString());
            };

            sipTransport.SIPResponseOutTraceEvent += (localEP, remoteEP, resp) =>
            {
                Console.WriteLine($"Response sent: {localEP}->{remoteEP}");
                Console.WriteLine(resp.ToString());
            };

            sipTransport.SIPRequestRetransmitTraceEvent += (tx, req, count) =>
            {
                Console.WriteLine($"Request retransmit {count} for request {req.StatusLine}, initial transmit {DateTime.Now.Subtract(tx.InitialTransmit).TotalSeconds.ToString("0.###")}s ago.");
            };

            sipTransport.SIPResponseRetransmitTraceEvent += (tx, resp, count) =>
            {
                Console.WriteLine($"Response retransmit {count} for response {resp.ShortDescription}, initial transmit {DateTime.Now.Subtract(tx.InitialTransmit).TotalSeconds.ToString("0.###")}s ago.");
            };
        }

        /// <summary>
        ///  Adds a console logger. Can be omitted if internal SIPSorcery debug and warning messages are not required.
        /// </summary>
        private static Microsoft.Extensions.Logging.ILogger AddConsoleLogger()
        {
            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                .WriteTo.Console()
                .CreateLogger();
            var factory = new SerilogLoggerFactory(serilogLogger);
            SIPSorcery.LogFactory.Set(factory);
            return factory.CreateLogger<Program>();
        }

        private static void OnRtpPacketReceived(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket)
        {
            if (mediaType == SDPMediaTypesEnum.audio)
            {
                var sample = rtpPacket.Payload;

                for (int index = 0; index < sample.Length; index++)
                {
                    if (rtpPacket.Header.PayloadType == (int)SDPWellKnownMediaFormatsEnum.PCMA)
                    {
                        short pcm = NAudio.Codecs.ALawDecoder.ALawToLinearSample(sample[index]);
                        byte[] pcmSample = new byte[] { (byte)(pcm & 0xFF), (byte)(pcm >> 8) };
                        _waveFile.Write(pcmSample, 0, 2);
                    }
                    else
                    {
                        short pcm = NAudio.Codecs.MuLawDecoder.MuLawToLinearSample(sample[index]);
                        byte[] pcmSample = new byte[] { (byte)(pcm & 0xFF), (byte)(pcm >> 8) };
                        _waveFile.Write(pcmSample, 0, 2);
                    }
                }
            }
        }
    }
}
