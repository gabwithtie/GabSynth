using System;
using System.Collections.Generic;
using System.Text;
using Android.Media.Midi;

namespace GabSynth.Platforms.Android.Native
{
    public class AndroidMidiReceiver : MidiReceiver
    {
        public event Action<byte, byte, byte>? OnRawMidiMessage;

        public override void OnSend(byte[]? msg, int offset, int count, long timestamp)
        {
            if (msg == null || count < 3) return;

            byte status = msg[offset];
            byte note = msg[offset + 1];
            byte velocity = msg[offset + 2];

            OnRawMidiMessage?.Invoke(status, note, velocity);
        }
    }
}
