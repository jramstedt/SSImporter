using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public class AudioClipProvider : IResProvider<AudioClip> {
    private class AudioClipLoader : LoaderBase<AudioClip> {

      [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
      private struct ParallelConvert : IJobParallelForBatch {
        [ReadOnly] public NativeArray<byte>.ReadOnly wavData;
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
        var rawResource = resFile.GetResourceData(resInfo, blockIndex);

        SoundEffectState sfx = new() {
          BitsPerSample = 8,
          SampleRate = 22050,
          ChannelCount = 1
        };
        
        var wavData = new NativeList<byte>(32 * 1024, Allocator.TempJob);
        unsafe {
          fixed (byte* rawResourcePtr = rawResource) {
            Reader.LoadSoundEffect(rawResourcePtr, rawResource.Length, ref sfx, ref wavData);
          }
        }
        
        using var result = new NativeArray<float>(wavData.Length, Allocator.TempJob);

        ParallelConvert convertJob = new() {
          wavData = wavData.AsReadOnly(),
          result = result,
        };
        var jobHandle = convertJob.ScheduleBatch(result.Length, 64);
        wavData.Dispose(jobHandle);

        AudioClip audioClip = AudioClip.Create($"{resInfo.info.Id:X4}:{blockIndex:X4}", result.Length, sfx.ChannelCount, sfx.SampleRate, false);

        while (!jobHandle.IsCompleted)
          await Awaitable.NextFrameAsync();

        jobHandle.Complete();

        audioClip.SetData(result, 0);

        InvokeCompletionEvent(audioClip);
      }
    }

    public IResHandle<AudioClip> Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ContentType.Voc)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ContentType.Voc)}.");

      return new AudioClipLoader(resFile, resInfo, blockIndex);
    }
  }
  
  [BurstCompile]
  internal struct Reader {
    [BurstCompile]
    public static unsafe void LoadSoundEffect(byte* rawResourcePtr, long rawResourceLength, ref SoundEffectState sfx, ref NativeList<byte> sampleBuffer) {
      BufferBinaryReader bbr = new BufferBinaryReader(rawResourcePtr, rawResourceLength);
      SoundEffect soundEffect = bbr.Read<SoundEffect>();
      
      if (soundEffect.VersionValidation != (~soundEffect.Version + 0x1234))
        throw new Exception($"Sound validation failed. vv:{soundEffect.VersionValidation} v:{soundEffect.Version}.");

      bbr.Position = soundEffect.DataOffset;
      
      ReadSoundEffectBlocks(ref bbr, ref sfx, ref sampleBuffer);
    }

    [BurstCompile]
    private static unsafe void ReadSoundEffectBlocks(ref BufferBinaryReader bbr, ref SoundEffectState sfx, ref NativeList<byte> sampleBuffer) {
      var bbw = new ListBinaryWriter(sampleBuffer);
      
      var lengthBytes = stackalloc byte[3];
      
      while (bbr.Position < bbr.Length) {
        SoundEffect.BlockType blockType = (SoundEffect.BlockType)bbr.ReadByte();

        if (blockType == SoundEffect.BlockType.Terminator)
          break;

        bbr.ReadBytes(lengthBytes, 3);
        int dataLength = lengthBytes[2] << 16 | lengthBytes[1] << 8 | lengthBytes[0];

        if (blockType == SoundEffect.BlockType.SoundData) {
          var frequencyDivisor = bbr.ReadByte();
          var codecId = bbr.ReadByte();

          sfx.SampleRate = 1000000 / (256 - frequencyDivisor);
          sfx.CodecId = codecId;
          
          if (codecId == 0)
            bbw.CopyBytes(ref bbr, dataLength - 2);
#if UNITY_DOTS_DEBUG
          else {
            throw new Exception($"Unsupported coded id: {codecId}");
          }
#endif
        } else if (blockType == SoundEffect.BlockType.SoundDataContinuation) {
          if (sfx.CodecId == 0)
            bbw.CopyBytes(ref bbr, dataLength - 2);
#if UNITY_DOTS_DEBUG
          else {
            throw new Exception($"Unsupported coded id: {sfx.CodecId}");
          }
#endif
        } else if (blockType == SoundEffect.BlockType.Silence) {
          ushort lengthOfSilence = (ushort)(1 + bbr.Read<ushort>());
          byte frequencyDivisor = bbr.ReadByte();

          var sampleRate = 1000000 / (256 - frequencyDivisor);
          var totalLengthOfSamples = sfx.ChannelCount * (sfx.SampleRate * lengthOfSilence / sampleRate);

          bbw.WriteBytes(0, totalLengthOfSamples);
        } else if (blockType == SoundEffect.BlockType.Marker) {
          /*ushort markerId = bbr.Read<ushort>()*/
          bbr.Position += dataLength;
        } else if (blockType == SoundEffect.BlockType.Text) {
          /*string text = Encoding.UTF8.GetString(bbr.ReadBytes(dataLength));*/
          bbr.Position += dataLength;
        } else if (blockType == SoundEffect.BlockType.RepeatStart) {
          ushort repeatCount = (ushort)(1 + bbr.Read<ushort>());

          var tmpBytes = new NativeList<byte>(16 * 1024, Allocator.Temp); // Temp allocator, no need to dispose
          ReadSoundEffectBlocks(ref bbr, ref sfx, ref tmpBytes); 

          for (int i = 0; i < repeatCount; ++i)
            bbw.WriteBytes(tmpBytes.GetUnsafePtr(), tmpBytes.Length);
        } else if (blockType == SoundEffect.BlockType.RepeatEnd) {
          break;
        } else if (blockType == SoundEffect.BlockType.ExtraInfo) {
          ushort frequencyDivisor = bbr.Read<ushort>();
          byte codecId = bbr.ReadByte();
          byte channelCount = (byte)(1 + bbr.ReadByte());
          
          // TODO This should be resampled?
          
          sfx.SampleRate = 256000000 / (channelCount * (65536 - frequencyDivisor));
          sfx.CodecId = codecId;
          sfx.ChannelCount = channelCount;
        } else if (blockType == SoundEffect.BlockType.SoundDataNew) {
          sfx.SampleRate = bbr.Read<int>();
          sfx.BitsPerSample = bbr.ReadByte();
          sfx.ChannelCount = bbr.ReadByte();
          sfx.CodecId = bbr.Read<ushort>();
          
          /*uint reserved = bbr.Read<uint>();*/
          bbr.Position += sizeof(uint);

          bbw.CopyBytes(ref bbr, dataLength - 12); // Block header is 12 bytes.
        }
      }
    }
  }

  public struct SoundEffectState {
    public byte BitsPerSample;
    public int SampleRate;
    public byte ChannelCount;
    public ushort CodecId;
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
    
    private unsafe fixed byte MagicIdentifier[19];
    private byte MagicIdentifierEOF;

    public ushort DataOffset;
    public short Version;
    public short VersionValidation;
  }
}