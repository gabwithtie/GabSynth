using GabSynth.Services;

namespace GabSynth.Effects;

public class EffectsChain
{
    public List<IMidiEffect> MidiEffects { get; } = new();
    public List<IAudioEffect> AudioEffects { get; } = new();

    public static object? CreateEffectByName(string name)
    {
        return name switch
        {
            "Velocity Dynamics" => new VelocityScalerEffect(),
            "3-Band Equalizer" => new BandEqEffect(),
            "Low-Pass Filter" => new LowPassFilterEffect(),
            // Add your other effects here...
            _ => null
        };
    }

    public void Initialize(int sampleRate)
    {
        foreach (var effect in AudioEffects)
        {
            effect.Initialize(sampleRate);
        }
    }

    public void HandleControlChange(byte ccNumber, byte ccValue)
    {
        // Route CCs to active MIDI and Audio effect parameters
        var allParameters = MidiEffects.SelectMany(e => e.Parameters)
            .Concat(AudioEffects.SelectMany(e => e.Parameters));

        foreach (var param in allParameters)
        {
            if (param.IsLearning)
            {
                param.MappedCc = ccNumber;
                param.IsLearning = false;
                param.UpdateFromMidiCc(ccValue);
                return;
            }

            if (param.MappedCc.HasValue && param.MappedCc.Value == ccNumber)
            {
                param.UpdateFromMidiCc(ccValue);
            }
        }
    }

    public void ProcessMidi(ref byte status, ref byte note, ref byte velocity)
    {
        foreach (var effect in MidiEffects)
        {
            if (effect.IsEnabled) effect.ProcessMidi(ref status, ref note, ref velocity);
        }
    }

    public void ProcessAudio(Span<float> buffer)
    {
        foreach (var effect in AudioEffects)
        {
            if (effect.IsEnabled) effect.Process(buffer);
        }
    }

    // Note: Control change routing omitted for brevity, but applies CC to both lists identically to the original implementation[cite: 8].
}