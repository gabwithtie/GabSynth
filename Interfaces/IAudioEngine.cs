using System;
using System.Collections.Generic;
using System.Text;

namespace GabSynth.Interfaces
{
    public interface IAudioEngine
    {
        bool IsRunning { get; }
        void Start();
        void Stop();
        void HandleMidiMessage(byte status, byte note, byte velocity);
        void UpdateFilterCutoff(float cutoffFrequency);
    }
}
