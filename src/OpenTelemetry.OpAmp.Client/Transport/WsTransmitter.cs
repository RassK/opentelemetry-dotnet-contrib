// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net.WebSockets;
using Google.Protobuf;
using OpenTelemetry.OpAmp.Client.Utils;

namespace OpenTelemetry.OpAmp.Client.Transport;

internal class WsTransmitter
{
    private const int BufferSize = 4096;

    private readonly byte[] buffer = new byte[BufferSize];

    private readonly ClientWebSocket ws;

    public WsTransmitter(ClientWebSocket ws)
    {
        this.ws = ws ?? throw new ArgumentNullException(nameof(ws));
    }

    public async Task SendAsync(IMessage message, CancellationToken token = default)
    {
        var headerSize = OpAmpWsHeaderHelper.WriteHeader(this.buffer);
        var size = message.CalculateSize();

        // fits to the buffer, send completely
        if (size + headerSize <= BufferSize)
        {
            var segment = new ArraySegment<byte>(this.buffer, headerSize, size);
            message.WriteTo(segment);

            // resegment to include the header byte
            segment = new ArraySegment<byte>(this.buffer, 0, size + headerSize);

            await this.ws.SendAsync(segment, WebSocketMessageType.Binary, true, token).ConfigureAwait(false);
        }

        // Does not fit, need to chunk the message
        else
        {
            // It's expected that large messages are created rarely.
            var buffer = message.ToByteArray();
            var offset = 0;

            while (true)
            {
                var count = buffer.Length - offset < BufferSize
                    ? buffer.Length - offset
                    : BufferSize;

                var segment = new ArraySegment<byte>(buffer, offset, count);
                await this.ws.SendAsync(segment, WebSocketMessageType.Binary, true, token).ConfigureAwait(false);

                offset += count;

                if (offset >= buffer.Length)
                {
                    break;
                }
            }
        }
    }
}
