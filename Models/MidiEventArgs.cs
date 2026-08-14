using System;
using System.Collections.Generic;
using System.Text;

namespace GabSynth.Models
{
    public class MidiEventArgs : EventArgs
    {
        public byte Command { get; }
        public byte Note { get; }
        public byte Velocity { get; }

        public MidiEventArgs(byte status, byte note, byte velocity)
        {
            Command = (byte)(status & 0xF0);
            Note = note;
            Velocity = velocity;
        }
    }
}
