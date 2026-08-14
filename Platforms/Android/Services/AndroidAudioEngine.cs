using Android.Media;
using GabSynth.Interfaces;

namespace GabSynth.Platforms.Android.Services;

public class AndroidAudioEngine : IAudioEngine
{
    private AudioTrack? _audioTrack;
    private CancellationTokenSource? _cts;

    private const int SampleRate = 44100;
    private float _phase = 0.0f;
    private float _frequency = 440.0f;
    private bool _isPlaying = false;
    private float _filterCutoff = 1000f;

    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning) return;

        int minBufferSize = AudioTrack.GetMinBufferSize(
            SampleRate,
            ChannelOut.Mono,
            Encoding.Pcm16bit);

        _audioTrack = new AudioTrack.Builder()
        .SetAudioAttributes(new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)
            .SetContentType(AudioContentType.Music)
            .Build())
        .SetAudioFormat(new AudioFormat.Builder()
            .SetEncoding(Encoding.Pcm16bit)
            .SetSampleRate(SampleRate)
            .SetChannelMask(ChannelOut.Mono)
            .Build())
        .SetBufferSizeInBytes(minBufferSize)
        .SetPerformanceMode(AudioTrackPerformanceMode.LowLatency)
        .Build();

        _audioTrack.Play();
        IsRunning = true;

        _cts = new CancellationTokenSource();
        Task.Run(() => AudioRenderLoop(_cts.Token), _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _audioTrack?.Stop();
        _audioTrack?.Release();
        IsRunning = false;
    }

    public void HandleMidiMessage(byte status, byte note, byte velocity)
    {
        byte command = (byte)(status & 0xF0);

        if (command == 0x90 && velocity > 0) // Note On
        {
            // Conversion equation: f = 440 * 2^((n - 69) / 12)
            _frequency = (float)(440.0 * Math.Pow(2.0, (note - 69) / 12.0));
            _isPlaying = true;
        }
        else if (command == 0x80 || (command == 0x90 && velocity == 0)) // Note Off
        {
            _isPlaying = false;
        }
    }

    public void UpdateFilterCutoff(float cutoffFrequency)
    {
        Volatile.Write(ref _filterCutoff, cutoffFrequency);
    }

    private void AudioRenderLoop(CancellationToken token)
    {
        // Reusable allocation buffer to avoid GC latency pops
        short[] pcmBuffer = new short[256];

        while (!token.IsCancellationRequested)
        {
            float phaseIncrement = (float)(2.0 * Math.PI * _frequency / SampleRate);

            for (int i = 0; i < pcmBuffer.Length; i++)
            {
                if (_isPlaying)
                {
                    _phase += phaseIncrement;
                    if (_phase >= 2.0 * Math.PI) _phase -= (float)(2.0 * Math.PI);

                    pcmBuffer[i] = (short)(Math.Sin(_phase) * short.MaxValue * 0.5);
                }
                else
                {
                    pcmBuffer[i] = 0;
                }
            }

            _audioTrack?.Write(pcmBuffer, 0, pcmBuffer.Length);
        }
    }
}
