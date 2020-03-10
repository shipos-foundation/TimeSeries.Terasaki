/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Dolittle. All rights reserved.
 *  Licensed under the MIT License. See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using RaaLabs.TimeSeries.Modules;
using Dolittle.Collections;
using Dolittle.Logging;
using RaaLabs.TimeSeries.Modules.Connectors;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RaaLabs.TimeSeries.Terasaki
{
    /// <summary>
    /// Represents an implementation for <see cref="IAmAStreamingConnector"/>
    /// </summary>
    public class Connector : IAmAStreamingConnector
    {
        /// <inheritdoc/>
        public event DataReceived DataReceived = (tag, ValueTask, timestamp) => { };

        readonly ILogger _logger;
        readonly ISentenceParser _parser;
        readonly IWE500Parser _we500Parser;

        readonly ConnectorConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of <see cref="Connector"/>
        /// </summary>
        /// <param name="configuration"><see cref="ConnectorConfiguration"/> holding all configuration</param>
        /// <param name="logger"><see cref="ILogger"/> for logging</param>
        /// <param name="parser"><see cref="ISentenceParser"/> for parsing the NMEA sentences</param>
        /// <param name="we500Parser"><see cref="IWE500Parser"/> for parsing the NMEA sentences</param>
        public Connector(
            ConnectorConfiguration configuration,
            ISentenceParser parser,
            IWE500Parser we500Parser,
            ILogger logger
)
        {
            _logger = logger;
            _parser = parser;
            _we500Parser = we500Parser;
            _configuration = configuration;
            _logger.Information($"Will connect to '{configuration.Ip}:{configuration.Port}'");
        }

        /// <inheritdoc/>
        public Source Name => "Terasaki";

        /// <inheritdoc/>
        /// 
        public void Connect()
        {
            switch(_configuration.ProtocolType)
            {
                case "WE22": ConnectWE22(); break;
                case "WE500": ConnectWE500(); break;
                default: _logger.Error("Protocol not defined"); break;
            }
        }

        void ConnectWE22()
        {
            while (true)
            {
                try
                {
                    var client = new TcpClient(_configuration.Ip, _configuration.Port);
                    using (var stream = client.GetStream())
                    {
                        var started = false;
                        var skip = false;
                        var sentenceBuilder = new StringBuilder();
                        for (; ; )
                        {
                            var result = stream.ReadByte();
                            if (result == -1) break;

                            var character = (char)result;
                            switch (character)
                            {
                                case '$':
                                    started = true;
                                    break;
                                case '\n':
                                    {
                                        skip = true;
                                        var sentence = sentenceBuilder.ToString();
                                        ParseSentence(sentence);
                                        sentenceBuilder = new StringBuilder();
                                    }
                                    break;
                            }
                            if (started && !skip) sentenceBuilder.Append(character);
                            skip = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error while connecting to TCP stream");
                    Thread.Sleep(2000);
                }
            }
        }

        void ConnectWE500()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                        socket.Connect(IPAddress.Parse(_configuration.Ip), _configuration.Port);

                        using (var stream = new NetworkStream(socket, FileAccess.Read, true))
                        {
                            _we500Parser.BeginParse(stream, channel =>
                            {
                                DataReceived(channel.Id.ToString(), channel.Value, Timestamp.UtcNow);
                            });
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error while connecting to TCP stream");
                    }

                    Thread.Sleep(10000);
                }
            });
        }

        void ParseSentence(string sentence)
        {
            if (_parser.CanParse(sentence))
            {
                try
                {
                    var output = _parser.Parse(sentence);
                    output.ForEach(_ =>
                    {
                        DataReceived(_.Tag, _.Data, Timestamp.UtcNow);
                        _logger.Information($"Tag: {_.Tag}, Value : {_.Data}");
                    });
                }
                catch (FormatException ex)
                {
                    _logger.Error(ex, $"Trouble parsing  {sentence}");
                }

            }
        }

    }
}