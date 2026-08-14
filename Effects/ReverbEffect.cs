namespace GabSynth.Effects;

public class ReverbEffect : IAudioEffect
{
    public string Name => "Studio Reverb";
    public bool IsEnabled { get; set; } = false;

    // 🎛️ NEW ENHANCED PARAMETERS
    public EffectParameter ReverbTimeSec { get; } = new("Decay (s)", 0.5f, 10.0f, 2.5f);
    public EffectParameter PreDelayMs { get; } = new("Pre-Delay (ms)", 0.0f, 100.0f, 20.0f);
    public EffectParameter HighCutHz { get; } = new("High Cut (Hz)", 1000.0f, 16000.0f, 5000.0f);
    public EffectParameter Damping { get; } = new("Damping", 0.0f, 0.95f, 0.5f);
    public EffectParameter Mix { get; } = new("Mix", 0.0f, 1.0f, 0.3f);

    public IReadOnlyList<EffectParameter> Parameters => new[]
    {
        ReverbTimeSec, PreDelayMs, HighCutHz, Damping, Mix
    };

    private CombFilter[] _combs = Array.Empty<CombFilter>();
    private AllpassFilter[] _allpasses = Array.Empty<AllpassFilter>();

    // Pre-delay ring buffer
    private float[] _preDelayBuffer = Array.Empty<float>();
    private int _preDelayWritePos;

    // Smooth High-Cut Filter state
    private float _highCutState;
    private int _sampleRate = 48000;

    public void Initialize(int sampleRate)
    {
        _sampleRate = sampleRate;
        float scale = sampleRate / 44100.0f;

        // Allocate 100ms Pre-Delay Buffer
        _preDelayBuffer = new float[(int)(sampleRate * 0.1f)];
        _preDelayWritePos = 0;
        _highCutState = 0f;

        // Prime numbers for high-density diffusion
        _combs = new CombFilter[]
        {
            new((int)(1116 * scale)),
            new((int)(1188 * scale)),
            new((int)(1277 * scale)),
            new((int)(1356 * scale)),
            new((int)(1422 * scale)),
            new((int)(1491 * scale))
        };

        _allpasses = new AllpassFilter[]
        {
            new((int)(225 * scale), 0.5f),
            new((int)(556 * scale), 0.5f),
            new((int)(441 * scale), 0.5f)
        };
    }

    public void Process(Span<float> buffer)
    {
        if (!IsEnabled || _combs.Length == 0 || _preDelayBuffer.Length == 0) return;

        float mix = Mix.Value;
        float damp = Damping.Value;
        float decaySec = ReverbTimeSec.Value;

        // 1. Calculate High-Cut Lowpass Alpha (Softens harsh top end)
        float cutoff = Math.Clamp(HighCutHz.Value, 200f, _sampleRate * 0.49f);
        float g = MathF.Tan(MathF.PI * cutoff / _sampleRate);
        float highCutAlpha = g / (1.0f + g);

        // 2. Pre-Delay Sample Calculation
        int preDelayMax = _preDelayBuffer.Length;
        int preDelaySamples = Math.Clamp((int)(_sampleRate * (PreDelayMs.Value / 1000.0f)), 0, preDelayMax - 1);

        for (int i = 0; i < buffer.Length; i++)
        {
            float input = buffer[i];

            // --- Step A: Push to Pre-Delay Ring Buffer ---
            _preDelayBuffer[_preDelayWritePos] = input;
            int preDelayReadPos = (_preDelayWritePos - preDelaySamples + preDelayMax) % preDelayMax;
            float delayedInput = _preDelayBuffer[preDelayReadPos];
            _preDelayWritePos = (_preDelayWritePos + 1) % preDelayMax;

            // --- Step B: Parallel Comb Filters with RT60 Feedback ---
            float combAccum = 0f;
            for (int c = 0; c < _combs.Length; c++)
            {
                // Mathematical RT60 feedback: feedback = 10^(-3 * delay / (RT60 * fs))
                float feedback = MathF.Pow(10f, -3f * _combs[c].BufferLength / (decaySec * _sampleRate));
                feedback = Math.Clamp(feedback, 0.0f, 0.98f);

                combAccum += _combs[c].Process(delayedInput, feedback, damp);
            }

            combAccum /= _combs.Length; // Normalize summed energy

            // --- Step C: Series Allpass Diffusers ---
            for (int a = 0; a < _allpasses.Length; a++)
            {
                combAccum = _allpasses[a].Process(combAccum);
            }

            // --- Step D: Apply Output High-Cut Filter (Warmth Stage) ---
            _highCutState += highCutAlpha * (combAccum - _highCutState);
            float wetSignal = _highCutState;

            // --- Step E: Dry / Wet Output Mix ---
            buffer[i] = (input * (1.0f - mix)) + (wetSignal * mix);
        }
    }

    private class CombFilter
    {
        private readonly float[] _buffer;
        private int _index;
        private float _filterStore;

        public int BufferLength => _buffer.Length;

        public CombFilter(int size) => _buffer = new float[size];

        public float Process(float input, float feedback, float damp)
        {
            float output = _buffer[_index];
            _filterStore = (output * (1.0f - damp)) + (_filterStore * damp);
            _buffer[_index] = input + (_filterStore * feedback);

            _index = (_index + 1) % _buffer.Length;
            return output;
        }
    }

    private class AllpassFilter
    {
        private readonly float[] _buffer;
        private readonly float _feedback;
        private int _index;

        public AllpassFilter(int size, float feedback)
        {
            _buffer = new float[size];
            _feedback = feedback;
        }

        public float Process(float input)
        {
            float bufOut = _buffer[_index];
            float output = -input + bufOut;
            _buffer[_index] = input + (bufOut * _feedback);

            _index = (_index + 1) % _buffer.Length;
            return output;
        }
    }
}