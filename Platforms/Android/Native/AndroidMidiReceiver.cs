using Android.Media.Midi;

namespace GabSynth.Platforms.Android.Native;

public class AndroidMidiReceiver : MidiReceiver
{
    public event Action<byte, byte, byte>? OnRawMidiMessage;

    // Running status persists across buffer calls
    private byte _runningStatus = 0;

    public override void OnSend(byte[]? msg, int offset, int count, long timestamp)
    {
        if (msg == null || count == 0) return;

        try
        {
            int i = offset;
            int end = offset + count;

            // Loop through all bytes in the buffer
            while (i < end)
            {
                byte b = msg[i];

                // 1. FILTER REAL-TIME NOISE: Ignore Active Sensing (0xFE), Timing Clock (0xF8), etc.
                if (b >= 0xF8)
                {
                    i++;
                    continue;
                }

                byte status;

                // Check if this byte is a Status Byte (MSB is 1) or Data Byte (MSB is 0)
                if (b >= 0x80)
                {
                    status = b;
                    _runningStatus = b; // Remember running status
                    i++;
                }
                else
                {
                    // 2. RUNNING STATUS: Use the last remembered status byte
                    if (_runningStatus == 0)
                    {
                        i++; // Skip invalid orphan data byte
                        continue;
                    }
                    status = _runningStatus;
                }

                byte command = (byte)(status & 0xF0);
                byte channel = (byte)(status & 0x0F);

                // 3-Byte Commands: Note Off (0x80), Note On (0x90), Poly Pressure (0xA0), CC (0xB0), Pitch Bend (0xE0)
                if (command == 0x80 || command == 0x90 || command == 0xA0 || command == 0xB0 || command == 0xE0)
                {
                    if (i + 1 >= end) break; // Buffer truncated mid-message

                    byte note = msg[i++];
                    byte velocity = msg[i++];

                    // 3. VELOCITY NORMALIZATION:
                    // Treat Note On with Velocity <= 2 as explicit Note Off
                    if (command == 0x90 && velocity <= 2)
                    {
                        status = (byte)(0x80 | channel);
                    }

                    OnRawMidiMessage?.Invoke(status, note, velocity);
                }
                // 2-Byte Commands: Program Change (0xC0), Channel Pressure (0xD0)
                else if (command == 0xC0 || command == 0xD0)
                {
                    if (i >= end) break;

                    byte data1 = msg[i++];
                    OnRawMidiMessage?.Invoke(status, data1, 0);
                }
                else
                {
                    // System Exclusive or unrecognized byte
                    i++;
                }
            }
        }
        catch
        {
            // Shield native thread from unexpected buffer alignment exceptions
        }
    }
}