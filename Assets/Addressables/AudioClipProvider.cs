using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SS.Resources {
  public class AudioClipProvider : ResourceProviderBase {
    public override Type GetDefaultType(IResourceLocation location) => typeof(AudioClip);

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

    public override void Provide(ProvideHandle provideHandle) {
      var location = provideHandle.Location;

      var resFile = provideHandle.GetDependency<ResourceFile>(0);
      if (resFile == null) {
        provideHandle.Complete<AudioClip>(null, false, new Exception($"Resource file failed to load for location {location.PrimaryKey}."));
        return;
      }

      var key = provideHandle.ResourceManager.TransformInternalId(location);
      ushort resId, block;
      if (!Utils.ExtractResourceIdAndBlock(key, out resId, out block)) {
        provideHandle.Complete<AudioClip>(null, false, new Exception($"Resource {location.InternalId} with key {key} is not valid."));
        return;
      }

      if (resFile.GetResourceInfo(resId).info.ContentType != ResourceFile.ContentType.Voc) {
        provideHandle.Complete<AudioClip>(null, false, new Exception($"Resource {location.InternalId} is not {nameof(ResourceFile.ContentType.Voc)}."));
        return;
      }

      byte[] rawResource = resFile.GetResourceData(resId, block);

      using (MemoryStream ms = new MemoryStream(rawResource), ams = new MemoryStream()) {
        BinaryReader msbr = new BinaryReader(ms);
        BinaryWriter amsbw = new BinaryWriter(ams);

        provideHandle.SetProgressCallback(() => msbr.BaseStream.Position / msbr.BaseStream.Length);

        SoundEffectState sfx = new SoundEffectState();

        SoundEffect soundEffect = msbr.Read<SoundEffect>();

        if (soundEffect.VersionValidation != (~soundEffect.Version + 0x1234)) {
          provideHandle.Complete<AudioClip>(null, false, new Exception("Sound validation failed."));
          return;
        }

        ReadSoundEffectBlocks(msbr, amsbw, sfx);

        AudioClip audioClip = AudioClip.Create(location.InternalId, (int)ams.Length, sfx.ChannelCount, (int)sfx.SampleRate, false);

        using var wavData = new NativeArray<byte>(ams.ToArray(), Allocator.TempJob);
        using var result = new NativeArray<float>(wavData.Length, Allocator.TempJob);
        ParallelConvert convertJob = new ParallelConvert {
          wavData = wavData,
          result = result,
        };
        var jobHandle = convertJob.ScheduleBatch(convertJob.result.Length, 64);
        jobHandle.Complete();

        audioClip.SetData(result.ToArray(), 0);

        /*
        ams.Position = 0;
        float[] wavData = new float[ams.Length];
        for (int i = 0; i < ams.Length; ++i)
          wavData[i] = (0x80 - ams.ReadByte()) / 128.0f;

        audioClip.SetData(wavData, 0);
        */

        provideHandle.Complete(audioClip, true, null);
      }
    }

    private void ReadSoundEffectBlocks(BinaryReader msbr, BinaryWriter amsbw, SoundEffectState sfx) {
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

          sfx.SampleRate = 1000000 / (256 - (uint)frequencyDivisor);

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

          uint sampleRate = 1000000 / (256 - (uint)frequencyDivisor);
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
          MemoryStream repeatms = new MemoryStream();
          BinaryWriter repeatmsbw = new BinaryWriter(repeatms);

          ushort repeatCount = (ushort)(1 + msbr.ReadUInt16());

          ReadSoundEffectBlocks(msbr, repeatmsbw, sfx);

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

  public class SoundEffectState {
    public byte BitsPerSample;
    public uint SampleRate;
    public byte ChannelCount;

    public SoundEffectState() {
      BitsPerSample = 8;
      SampleRate = 22050;
      ChannelCount = 1;
    }
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