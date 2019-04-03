/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Dolittle. All rights reserved.
 *  Licensed under the MIT License. See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.IO;
using System.Threading.Tasks;
using Dolittle.Edge.Modules;
using Dolittle.Scheduling;

namespace Dolittle.Edge.Terasaki
{
    /// <summary>
    /// Represents an implemention of <see cref="ICoordinator"/>
    /// </summary>
    public class Coordinator : ICoordinator
    {
        readonly ICommunicationClient _client;
        readonly IConnector _connector;

        /// <summary>
        /// Initializes an instance of <see cref="Coordinator"/>
        /// </summary>
        /// <param name="client"><see cref="ICommunicationClient"/> for communication</param>
        /// <param name="connector"></param>
        public Coordinator(
            ICommunicationClient client,
            IConnector connector)
        {
            _client = client;
            _connector = connector;
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            _connector.Subscribe(ChannelReceived);
        }


        void ChannelReceived(Channel channel)
        {          
            var dataPoint = new TagDataPoint<ChannelValue>
            {
                Tag = channel.Id.ToString(),
                System = "Terasaki",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Value = channel.Value
            };

            _client.SendAsJson("output", dataPoint);
        }
    }
}