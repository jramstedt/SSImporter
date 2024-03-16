using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public class AudioClipProvider : IResProvider<AudioClip> {
    private class AudioClipLoader : LoaderBase<AudioClip> {

      [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
      public struct ParallelConvert : IJobParallelForBatch {
        [ReadOnly] public NativeArray<byte> wavData;
        [WriteOnly] public NativeArray<float> result;

        public void Execute(int startIndex, int count) {
          int lastIndex = startIndex + count;
          for (int index = startIndex; index < lastIndex; ++index)
            result[index] = (0x80 - wavData[index]) / 128.0f;
        }
      }

      public AudioClipLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        LoadAsync(resFile, resInfo, blockIndex);
      }

      private async void LoadAsync(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        byte[] rawResource = resFile.GetResourceData(resInfo, blockIndex);

        using MemoryStream ms = new(rawResource);
        using BinaryReader msbr = new(ms);

        SoundEffectState sfx = new() {
          BitsPerSample = 8,
          SampleRate = 22050,
          ChannelCount = 1
        };

        SoundEffect soundEffect = msbr.Read<SoundEffect>();

        if (soundEffect.VersionValidation != (~soundEffect.Version + 0x1234))
          throw new Exception("Sound validation failed.");

        using MemoryStream ams = new();
        using BinaryWriter amsbw = new(ams);
        ReadSoundEffectBlocks(msbr, amsbw, ref sfx);

        using var wavData = new NativeArray<byte>(ams.ToArray(), Allocator.TempJob);
        using var result = new NativeArray<float>(wavData.Length, Allocator.TempJob);
        ParallelConvert convertJob = new() {
          wavData = wavData,
          result = result,
        };
        var jobHandle = convertJob.ScheduleBatch(convertJob.result.Length, 64);

        AudioClip audioClip = AudioClip.Create($"{resInfo.info.Id:X4}:{blockIndex:X4}", result.Length, sfx.ChannelCount, sfx.SampleRate, false);

        while (!jobHandle.IsCompleted)
          await Awaitable.NextFrameAsync();

        jobHandle.Complete();

        audioClip.SetData(result, 0);

        InvokeCompletionEvent(audioClip);
      }

      private void ReadSoundEffectBlocks(BinaryReader msbr, BinaryWriter amsbw, ref SoundEffectState sfx) {
        while (msbr.BaseStream.Position < msbr.BaseStream.Length) {
          SoundEffect.BlockType blockType = (SoundEffect.BlockType)msbr.ReadByte();

          if (blockType == SoundEffect.BlockType.Terminator)
            break;

          byte[] lengthBytes = msbr.ReadBytes(3);
          int dataLength = lengthBytes[2] << 24 | lengthBytes[1] << 16 | lengthBytes[0];

          if (blockType == SoundEffect.BlockType.SoundData) {
            byte frequencyDivisor = msbr.ReadByte();
            /*byte codecId =*/
            msbr.ReadByte();

            sfx.SampleRate = 1000000 / (256 - frequencyDivisor);

            amsbw.Write(msbr.ReadBytes(dataLength - 2)); // Block header is 2 bytes.
          } else if (blockType == SoundEffect.BlockType.SoundDataContinuation) {
            /*byte frequencyDivisor =*/
            msbr.ReadByte();
            /*byte codecId =*/
            msbr.ReadByte();

            // Does audio need resampling?

            amsbw.Write(msbr.ReadBytes(dataLength - 2)); // Block header is 2 bytes.
          } else if (blockType == SoundEffect.BlockType.Silence) {
            ushort lengthOfSilence = (ushort)(1 + msbr.ReadUInt16());
            byte frequencyDivisor = msbr.ReadByte();

            // What if silence is in the beginning?

            int sampleRate = 1000000 / (256 - frequencyDivisor);
            float sampleRateFactor = sfx.SampleRate / sampleRate;

            uint totalLengthOfSamples = (uint)(lengthOfSilence * sampleRateFactor * sfx.ChannelCount);

            for (int i = 0; i < totalLengthOfSamples; ++i)
              amsbw.Write((byte)0);
          } else if (blockType == SoundEffect.BlockType.Marker) {
            /*ushort markerId =*/
            msbr.ReadUInt16();
          } else if (blockType == SoundEffect.BlockType.Text) {
            /*string text = Encoding.UTF8.GetString(*/
            msbr.ReadBytes(dataLength - 2)/*)*/;
          } else if (blockType == SoundEffect.BlockType.RepeatStart) {
            using MemoryStream repeatms = new();
            using BinaryWriter repeatmsbw = new(repeatms);

            ushort repeatCount = (ushort)(1 + msbr.ReadUInt16());

            ReadSoundEffectBlocks(msbr, repeatmsbw, ref sfx);

            for (int i = 0; i < repeatCount; ++i)
              repeatms.WriteTo(amsbw.BaseStream);
          } else if (blockType == SoundEffect.BlockType.RepeatEnd) {
            break;
          } else if (blockType == SoundEffect.BlockType.ExtraInfo) {
            /*ushort frequencyDivisor =*/
            msbr.ReadUInt16();
            /*byte codecId =*/
            msbr.ReadByte();
            /*byte channelCount = (byte)(1 +*/
            msbr.ReadByte()/*)*/;

            //uint sampleRate = 256000000 / (channelCount * (65536 - (uint)frequencyDivisor));

            // This should override next sound block
          } else if (blockType == SoundEffect.BlockType.SoundDataNew) {
            /*uint sampleRate =*/
            msbr.ReadUInt32();
            /*byte bitsPerSample =*/
            msbr.ReadByte();
            /*byte channelCount =*/
            msbr.ReadByte();
            /*ushort codecId =*/
            msbr.ReadUInt16();
            /*uint reserved =*/
            msbr.ReadUInt32();

            /*amsbw.Write(*/
            msbr.ReadBytes(dataLength - 12)/*)*/; // Block header is 12 bytes.
          }
        }
      }
    }

    public IResHandle<AudioClip> Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ResourceFile.ContentType.Voc)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ResourceFile.ContentType.Voc)}.");

      return new AudioClipLoader(resFile, resInfo, blockIndex);
    }
  }

  public struct SoundEffectState {
    public byte BitsPerSample;
    public int SampleRate;
    public byte ChannelCount;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct SoundEffect {
    public enum BlockType : byte {
      Terminator,
      SoundData,
      SoundDataContinuation,
      Silence,
      Marker,
      Text,
      RepeatStart,
      RepeatEnd,
      ExtraInfo,
      SoundDataNew
    }

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 19)]
    private byte[] MagicIdentifier;
    private byte MagicIdentifierTerminator;

    public ushort HeaderLength;
    public ushort Version;
    public ushort VersionValidation;
  }
}