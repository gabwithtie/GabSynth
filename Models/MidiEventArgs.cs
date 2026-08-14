namespace GabSynth.Models;

public class MidiEventArgs : EventArgs
{
    public byte Status { get; }
    public byte Command { get; }
    public byte Channel { get; }
    public byte Note { get; }
    public byte Velocity { get; }

    public MidiEventArgs(byte status, byte note, byte velocity)
    {
        Status = status;
        Command = (byte)(status & 0xF0);
        Channel = (byte)(status & 0x0F);
        Note = note;
        Velocity = velocity;
    }
}