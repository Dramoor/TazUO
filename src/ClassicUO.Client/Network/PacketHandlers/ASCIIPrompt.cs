using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.IO;

namespace ClassicUO.Network.PacketHandlers;

internal static class ASCIIPrompt
{
    public static void Receive(World world, ref StackDataReader p)
    {
        if (!world.InGame)
            return;

        world.MessageManager.PromptData = new PromptData { Prompt = ConsolePrompt.ASCII, Data = p.ReadUInt64BE() };
    }
}
