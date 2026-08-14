using Android.Content;
using Android.Media;
using GabSynth.Interfaces;

namespace GabSynth.Platforms.Android.Services;

public class AndroidAudioEngine : IAudioEngine
{
    private readonly IAudioProcessor _audioProcessor;
    private AudioTrack? _audioTrack;
    private CancellationTokenSource? _cts;

    private int _sampleRate = 48000;
    private int _bufferFrameSize = 192;

    public bool IsRunning { get; private set; }

    // Inject IAudioProcessor directly!
    public AndroidAudioEngine(IAudioProcessor audioProcessor)
    {
        _audioProcessor = audioProcessor;
    }

    public void Start()
    {
        if (IsRunning) return;

        var context = global::Android.App.Application.Context;
        var audioManager = (AudioManager?)context.GetSystemService(Context.AudioService);

        if (audioManager != null)
        {
            if (int.TryParse(audioManager.GetProperty(AudioManager.PropertyOutputSampleRate), out int nativeRate))
                _sampleRate = nativeRate;

            if (int.TryParse(audioManager.GetProperty(AudioManager.PropertyOutputFramesPerBuffer), out int nativeFrames))
                _bufferFrameSize = nativeFrames;
        }

        _audioProcessor.Initialize(_sampleRate);

        var attributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)
            .SetContentType(AudioContentType.Music)
            .SetFlags(AudioFlags.LowLatency)
            .Build();

        var format = new AudioFormat.Builder()
            .SetEncoding(Encoding.Pcm16bit)
            .SetSampleRate(_sampleRate)
            .SetChannelMask(ChannelOut.Mono)
            .Build();

        int minBufferSizeInBytes = _bufferFrameSize * 2 * 2;

        _audioTrack = new AudioTrack.Builder()
            .SetAudioAttributes(attributes)
            .SetAudioFormat(format)
            .SetBufferSizeInBytes(minBufferSizeInBytes)
            .SetPerformanceMode(AudioTrackPerformanceMode.LowLatency)
            .Build();

        try { _audioTrack.SetBufferSizeInFrames(_bufferFrameSize); } catch { }

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
        // Forward MIDI straight to generic processor
        _audioProcessor.ProcessMidiMessage(status, note, velocity);
    }

    public void UpdateFilterCutoff(float cutoffFrequency) { }

    private void AudioRenderLoop(CancellationToken token)
    {
        float[] floatBuffer = new float[_bufferFrameSize];
        short[] pcmBuffer = new short[_bufferFrameSize];

        while (!token.IsCancellationRequested)
        {
            _audioProcessor.RenderAudio(floatBuffer);

            for (int i = 0; i < _bufferFrameSize; i++)
            {
                // CRITICAL FIX: Clamp float to [-1.0f, +1.0f] BEFORE casting to short!
                // This prevents integer wraparound explosions when audio clips.
                float clampedSample = Math.Clamp(floatBuffer[i], -1.0f, 1.0f);

                pcmBuffer[i] = (short)(clampedSample * 32767.0f);
            }

            _audioTrack?.Write(pcmBuffer, 0, pcmBuffer.Length);
        }
    }
}