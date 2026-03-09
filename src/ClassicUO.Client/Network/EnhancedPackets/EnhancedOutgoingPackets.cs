using System.Collections.Generic;
using ClassicUO.IO;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Network;

internal static class EnhancedOutgoingPackets
{
    public static HashSet<EnhancedPacketType> EnabledPackets = new();

    public static void Send_TazUO(this AsyncNetClient socket)
    {
        //Send 0xCE 2 VERSION STRING ASCII
        EnhancedPacketType id = EnhancedPacketType.TazUO_Identifier;

        using StackDataWriter writer = Extensions.GetWriter(id);

        writer.WriteASCII(CUOEnviroment.Version);

        writer.FinalLength();
        socket.Send(writer.BufferWritten, true);

        Log.TraceDebug($"Sent TazUO packet.");
    }

    public static void SendEnhancedPacket(this AsyncNetClient socket)
    {
        EnhancedPacketType id = EnhancedPacketType.EnableEnhancedPacket;

        if (!EnabledPackets.Contains(id))
            return;

        using StackDataWriter writer = Extensions.GetWriter(id);
        writer.FinalLength();
        socket.Send(writer.BufferWritten, true);
    }
}
