using GabSynth.Audio;
using GabSynth.Config;
using GabSynth.Effects;
using GabSynth.Interfaces;
using GabSynth.Models;

namespace GabSynth.Services;

public class MidiControlRouter
{
    private readonly MixerChannel[] _channels;
    private readonly EffectsChain _masterEffectsChain;
    private readonly MeltyAudioProcessor? _meltyProcessor;

    // Track active raw notes (sorted so .Max gives the current top note)
    private readonly SortedSet<byte> _heldNotes = new();

    // Map: (MixerChannelIndex, RawNote) -> (InternalChannel, TransposedPlayedNote)
    private readonly Dictionary<(int ChannelIndex, byte RawNote), (byte InternalChannel, byte PlayedNote)> _activeNoteMap = new();

    // Per-MixerChannel pool of available internal MIDI channels (0-15, skipping GM Drums 9)
    private readonly Dictionary<int, List<byte>> _channelPools = new();

    private const int PitchBendCenter = 8192;
    private const int PitchBendDeadband = 300;
    private int _rawPitchBend = PitchBendCenter;
    private bool _isBendLocked = false;

    public MidiControlRouter(MixerChannel[] channels, EffectsChain masterEffectsChain, IAudioProcessor audioProcessor)
    {
        _channels = channels;
        _masterEffectsChain = masterEffectsChain;
        _meltyProcessor = audioProcessor as MeltyAudioProcessor;

        InitializeChannelPools();
    }

    private void InitializeChannelPools()
    {
        for (int i = 0; i < BuildSettings.MaxMixerChannels; i++)
        {
            _channelPools[i] = new List<byte> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 15 };
        }
    }

    public void ProcessRawMidi(byte status, byte note, byte velocity)
    {
        byte command = (byte)(status & 0xF0);

        // 1. Control Change (0xB0)
        if (command == 0xB0)
        {
            HandleControlChange(note, velocity);
            return;
        }

        // 2. Pitch Bend (0xE0) — Target Top Note Only
        if (command == 0xE0)
        {
            _rawPitchBend = (velocity << 7) | note; // 14-bit pitch bend value

            // Unlock pitch bend when wheel returns to center deadband
            if (_isBendLocked && Math.Abs(_rawPitchBend - PitchBendCenter) <= PitchBendDeadband)
            {
                _isBendLocked = false;
            }

            UpdatePitchBendsAcrossChannels();
            return;
        }

        // 3. Note On / Note Off Handling
        if (command == 0x90 || command == 0x80)
        {
            // Velocity <= 2 or command 0x80 are treated as Note Off
            bool isNoteOn = (command == 0x90) && (velocity > 2);

            if (isNoteOn)
            {
                // Lock pitch bend if wheel is off-center when a new note is struck
                if (Math.Abs(_rawPitchBend - PitchBendCenter) > PitchBendDeadband)
                {
                    _isBendLocked = true;
                }

                _heldNotes.Add(note);
                HandleNoteOn(status, note, velocity);
            }
            else
            {
                _heldNotes.Remove(note);
                HandleNoteOff(note);
            }

            UpdatePitchBendsAcrossChannels();
        }
    }

    private void HandleNoteOn(byte status, byte rawNote, byte velocity)
    {
        for (int i = 0; i < _channels.Length; i++)
        {
            var channel = _channels[i];
            if (!channel.IsEnabled || _meltyProcessor == null) continue;

            byte mStatus = status;
            byte mNote = rawNote;
            byte mVel = velocity;

            _masterEffectsChain.ProcessMidi(ref mStatus, ref mNote, ref mVel);
            channel.Effects.ProcessMidi(ref mStatus, ref mNote, ref mVel);

            byte internalCh = AllocateChannel(i);
            var key = (ChannelIndex: i, RawNote: rawNote);
            _activeNoteMap[key] = (internalCh, mNote);

            // Strike voice on assigned channel — note remains on this channel until released
            _meltyProcessor.ProcessChannelMidi(i, internalCh, 0x90, mNote, mVel);
        }
    }

    private void HandleNoteOff(byte rawNote)
    {
        for (int i = 0; i < _channels.Length; i++)
        {
            var channel = _channels[i];
            if (!channel.IsEnabled || _meltyProcessor == null) continue;

            var key = (ChannelIndex: i, RawNote: rawNote);

            if (_activeNoteMap.TryGetValue(key, out var activeInfo))
            {
                // Silence note strictly on its assigned internal channel
                _meltyProcessor.ProcessChannelMidi(i, activeInfo.InternalChannel, 0x80, activeInfo.PlayedNote, 0);

                ReleaseChannel(i, activeInfo.InternalChannel);
                _activeNoteMap.Remove(key);
            }
        }
    }

    private void UpdatePitchBendsAcrossChannels()
    {
        if (_meltyProcessor == null) return;

        byte? topNote = _heldNotes.Count > 0 ? _heldNotes.Max : null;
        int effectiveBend = _isBendLocked ? PitchBendCenter : _rawPitchBend;

        byte bendLsb = (byte)(effectiveBend & 0x7F);
        byte bendMsb = (byte)((effectiveBend >> 7) & 0x7F);

        byte centerLsb = 0x00;
        byte centerMsb = 0x40; // 8192 Center

        for (int i = 0; i < _channels.Length; i++)
        {
            if (!_channels[i].IsEnabled) continue;

            foreach (var kvp in _activeNoteMap)
            {
                if (kvp.Key.ChannelIndex != i) continue;

                byte rawNote = kvp.Key.RawNote;
                byte internalCh = kvp.Value.InternalChannel;

                // Send physical pitch bend only to the top note's assigned channel
                if (topNote.HasValue && rawNote == topNote.Value)
                {
                    _meltyProcessor.ProcessChannelMidi(i, internalCh, 0xE0, bendLsb, bendMsb);
                }
                else
                {
                    _meltyProcessor.ProcessChannelMidi(i, internalCh, 0xE0, centerLsb, centerMsb);
                }
            }
        }
    }

    private byte AllocateChannel(int mixerChannelIndex)
    {
        var pool = _channelPools[mixerChannelIndex];
        if (pool.Count > 0)
        {
            byte ch = pool[0];
            pool.RemoveAt(0);
            return ch;
        }
        return 0; // Fallback
    }

    private void ReleaseChannel(int mixerChannelIndex, byte internalChannel)
    {
        var pool = _channelPools[mixerChannelIndex];
        if (!pool.Contains(internalChannel) && internalChannel != 9)
        {
            pool.Add(internalChannel);
        }
    }

    private void HandleControlChange(byte ccNumber, byte ccValue)
    {
        int muteBaseCc = 44;
        if (ccNumber >= muteBaseCc && ccNumber < muteBaseCc + BuildSettings.MaxMixerChannels)
        {
            int chIndex = ccNumber - muteBaseCc;
            if (chIndex < _channels.Length)
                _channels[chIndex].IsEnabled = ccValue >= 64;
            return;
        }

        int volumeBaseCc = 21;
        if (ccNumber >= volumeBaseCc && ccNumber < volumeBaseCc + BuildSettings.MaxMixerChannels)
        {
            int chIndex = ccNumber - volumeBaseCc;
            if (chIndex < _channels.Length)
                _channels[chIndex].Volume = ccValue / 127.0f;
            return;
        }

        _masterEffectsChain.HandleControlChange(ccNumber, ccValue);
    }
}