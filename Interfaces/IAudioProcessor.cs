namespace GabSynth.Interfaces;

public interface IAudioProcessor
{
    void Initialize(int sampleRate);
    void ProcessMidiMessage(byte status, byte note, byte velocity);

    /// <summary>
    /// Renders audio samples into a target buffer using zero-allocation Spans.
    /// Audio values should be normalized floating-point numbers between -1.0f and +1.0f.
    /// </summary>
    void RenderAudio(Span<float> outputBuffer);
}