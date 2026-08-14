using System;
using System.Collections.Generic;
using System.Text;

namespace GabSynth.Interfaces
{
    public interface IMidiService
    {
        event EventHandler<Models.MidiEventArgs>? MidiMessageReceived;
        void Initialize();
        void ScanAndConnect();
    }
}
