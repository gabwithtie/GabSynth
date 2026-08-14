using GabSynth.Services;

namespace GabSynth.Effects;

public class AudioEffectsChain
{
    public List<IAudioEffect> Effects { get; } = new();

    public AudioEffectsChain()
    {
        Effects.Add(new VelocityScalerEffect()); // 🎛️ MIDI Velocity Dynamics
        Effects.Add(new BandEqEffect());
        Effects.Add(new LowPassFilterEffect());
        Effects.Add(new OverdriveEffect());
        Effects.Add(new ChorusEffect());
        Effects.Add(new DelayEffect());
        Effects.Add(new ReverbEffect());
        Effects.Add(new MasterGainEffect());
    }

    public void Initialize(int sampleRate)
    {
        foreach (var effect in Effects)
        {
            effect.Initialize(sampleRate);
        }
    }

    public void Process(Span<float> buffer)
    {
        foreach (var effect in Effects)
        {
            if (effect.IsEnabled)
            {
                effect.Process(buffer);
            }
        }
    }

    public void HandleControlChange(byte ccNumber, byte ccValue)
    {
        // 1. Check if any parameter is in "MIDI Learn" mode
        foreach (var effect in Effects)
        {
            foreach (var param in effect.Parameters)
            {
                if (param.IsLearning)
                {
                    param.MappedCc = ccNumber;
                    param.IsLearning = false;
                    param.UpdateFromMidiCc(ccValue);
                    AppLogger.Log($"[MIDI Learn] Bound Parameter '{param.Name}' to CC #{ccNumber}");
                    return;
                }
            }
        }

        // 2. Otherwise, route CC message to all parameters mapped to this CC#
        foreach (var effect in Effects)
        {
            foreach (var param in effect.Parameters)
            {
                if (param.MappedCc.HasValue && param.MappedCc.Value == ccNumber)
                {
                    param.UpdateFromMidiCc(ccValue);
                }
            }
        }
    }
}