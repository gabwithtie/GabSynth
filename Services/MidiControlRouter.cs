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

    // Track active held raw input notes
    private readonly SortedSet<byte> _heldNotes = new();

    // Key: (ChannelIndex, RawNote) -> Value: (Exact Transposed Pitch, Struck Velocity)
    private readonly Dictionary<(int ChannelIndex, byte RawNote), (byte PlayedNote, byte Velocity)> _activeNoteMap = new();

    private int _rawPitchBend = 8192;
    private int _effectivePitchBend = 8192;
    private bool _isBendLocked = false;
    private byte? _previousTopNote = null;

    public MidiControlRouter(MixerChannel[] channels, EffectsChain masterEffectsChain, IAudioProcessor audioProcessor)
    {
        _channels = channels;
        _masterEffectsChain = masterEffectsChain;
        _meltyProcessor = audioProcessor as MeltyAudioProcessor;
    }

    public void ProcessRawMidi(byte status, byte note, byte velocity)
    {
        byte command = (byte)(status & 0xF0);

        // 1. Control Change (CC 0xB0)
        if (command == 0xB0)
        {
            HandleControlChange(note, velocity);
            return;
        }

        // 2. Pitch Bend (0xE0)
        if (command == 0xE0)
        {
            int lsb = note;
            int msb = velocity;
            _rawPitchBend = (msb << 7) | lsb;

            bool isAtCenter = Math.Abs(_rawPitchBend - 8192) <= 64;

            if (isAtCenter)
            {
                _isBendLocked = false;
                _effectivePitchBend = 8192;
            }
            else if (!_isBendLocked)
            {
                _effectivePitchBend = _rawPitchBend;
            }

            DispatchPitchBendToSynth(_effectivePitchBend);
            return;
        }

        // 3. Note On / Note Off Handling
        if (command == 0x90 || command == 0x80)
        {
            // Velocity 0 on 0x90 is treated as Note Off
            bool isNoteOn = (command == 0x90) && (velocity > 0);

            if (isNoteOn)
            {
                // Lock pitch bend if wheel is off-center when new note is struck
                if (Math.Abs(_rawPitchBend - 8192) > 64)
                {
                    _isBendLocked = true;
                    _effectivePitchBend = 8192;
                    DispatchPitchBendToSynth(8192);
                }

                _heldNotes.Add(note);
            }
            else
            {
                _heldNotes.Remove(note);
            }

            ReevaluateTopNoteAndRouteNotes(status, note, velocity, isNoteOn);
        }
    }

    private void ReevaluateTopNoteAndRouteNotes(byte status, byte rawNote, byte velocity, bool isNoteOn)
    {
        byte? newTopNote = _heldNotes.Count > 0 ? _heldNotes.Max : null;

        for (int i = 0; i < _channels.Length; i++)
        {
            var channel = _channels[i];
            if (!channel.IsEnabled || _meltyProcessor == null) continue;

            var key = (ChannelIndex: i, RawNote: rawNote);

            // Process MIDI effects for BOTH Note On and Note Off to keep effect state in sync
            byte mStatus = status;
            byte mNote = rawNote;
            byte mVel = velocity;

            _masterEffectsChain.ProcessMidi(ref mStatus, ref mNote, ref mVel);
            channel.Effects.ProcessMidi(ref mStatus, ref mNote, ref mVel);

            if (isNoteOn && mVel > 0)
            {
                // Store exact transposed pitch and original velocity
                _activeNoteMap[key] = (mNote, mVel);

                bool isTopNote = newTopNote.HasValue && (rawNote == newTopNote.Value);

                if (isTopNote)
                {
                    // Top Note -> Internal Synth Channel 1 (Bended)
                    _meltyProcessor.ProcessChannelMidi(i, 1, 0x90, mNote, mVel);

                    // Move previous top note to Internal Synth Channel 0 (Unbended)
                    if (_previousTopNote.HasValue && _previousTopNote.Value != rawNote && _heldNotes.Contains(_previousTopNote.Value))
                    {
                        var prevKey = (ChannelIndex: i, RawNote: _previousTopNote.Value);
                        if (_activeNoteMap.TryGetValue(prevKey, out var prevInfo))
                        {
                            _meltyProcessor.ProcessChannelMidi(i, 1, 0x80, prevInfo.PlayedNote, 0);
                            _meltyProcessor.ProcessChannelMidi(i, 0, 0x90, prevInfo.PlayedNote, prevInfo.Velocity);
                        }
                    }
                }
                else
                {
                    // Non-top Note -> Internal Synth Channel 0 (Unbended)
                    _meltyProcessor.ProcessChannelMidi(i, 0, 0x90, mNote, mVel);
                }
            }
            else
            {
                // Note Off: Kill precise pitch on both internal channels
                if (_activeNoteMap.TryGetValue(key, out var activeInfo))
                {
                    _meltyProcessor.ProcessChannelMidi(i, 0, 0x80, activeInfo.PlayedNote, 0);
                    _meltyProcessor.ProcessChannelMidi(i, 1, 0x80, activeInfo.PlayedNote, 0);
                    _activeNoteMap.Remove(key);
                }

                // Fallback: Ensure transformed pitch and raw pitch are silenced
                _meltyProcessor.ProcessChannelMidi(i, 0, 0x80, mNote, 0);
                _meltyProcessor.ProcessChannelMidi(i, 1, 0x80, mNote, 0);
                _meltyProcessor.ProcessChannelMidi(i, 0, 0x80, rawNote, 0);
                _meltyProcessor.ProcessChannelMidi(i, 1, 0x80, rawNote, 0);

                // Promote lower note to Internal Synth Channel 1 if it becomes new top note
                if (newTopNote.HasValue && newTopNote.Value != _previousTopNote)
                {
                    var promotedKey = (ChannelIndex: i, RawNote: newTopNote.Value);
                    if (_activeNoteMap.TryGetValue(promotedKey, out var promotedInfo))
                    {
                        _meltyProcessor.ProcessChannelMidi(i, 0, 0x80, promotedInfo.PlayedNote, 0);
                        _meltyProcessor.ProcessChannelMidi(i, 1, 0x90, promotedInfo.PlayedNote, promotedInfo.Velocity);
                    }
                }
            }
        }

        _previousTopNote = newTopNote;
    }

    private void DispatchPitchBendToSynth(int bendValue)
    {
        if (_meltyProcessor == null) return;

        byte lsb0 = (byte)(8192 & 0x7F);
        byte msb0 = (byte)((8192 >> 7) & 0x7F);

        byte lsb1 = (byte)(bendValue & 0x7F);
        byte msb1 = (byte)((bendValue >> 7) & 0x7F);

        for (int i = 0; i < _channels.Length; i++)
        {
            if (_channels[i].IsEnabled)
            {
                _meltyProcessor.ProcessChannelMidi(i, 0, 0xE0, lsb0, msb0);
                _meltyProcessor.ProcessChannelMidi(i, 1, 0xE0, lsb1, msb1);
            }
        }
    }

    public void AllNotesOff()
    {
        _heldNotes.Clear();
        _activeNoteMap.Clear();
        _previousTopNote = null;

        if (_meltyProcessor == null) return;

        for (int i = 0; i < _channels.Length; i++)
        {
            _meltyProcessor.ProcessChannelMidi(i, 0, 0xB0, 123, 0); // All Notes Off CC
            _meltyProcessor.ProcessChannelMidi(i, 1, 0xB0, 123, 0);
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