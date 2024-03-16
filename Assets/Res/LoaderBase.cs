
using System;
using System.IO;
using System.Runtime.ExceptionServices;

namespace SS.Resources {
  public class LoaderBase<T> : IResHandle<T> {
    public bool IsCompleted { get; private set; } = false;
    public ExceptionDispatchInfo Error { get; private set; }
    public T Result { get; private set; }

    private Action<IResHandle<T>> CompletedAction;
    public event Action<IResHandle<T>> Completed {
      add {
        if (IsCompleted)
          value(this);
        else
          CompletedAction += value;
      }

      remove {
        CompletedAction -= value;
      }
    }

    protected void InvokeCompletionEvent(T result) {
      Result = result;
      IsCompleted = true;

      CompletedAction?.Invoke(this);
    }

    protected void InvokeCompletionEvent(Exception exception) {
      if (exception != null)
        Error = ExceptionDispatchInfo.Capture(exception);

      IsCompleted = true;

      CompletedAction?.Invoke(this);
    }
  }

  public class CompletedLoader<T> : LoaderBase<T> {
    public CompletedLoader(T result) {
      InvokeCompletionEvent(result);
    }

    public CompletedLoader(Exception error) {
      InvokeCompletionEvent(error);
    }
  }

  public class PickResultLoader<T, K> : LoaderBase<T> {
    public PickResultLoader(IResHandle<K> loadOp, Func<K, T> pickResult) {
      loadOp.Completed += op => {
        try {
          InvokeCompletionEvent(pickResult(op.Result));
        } catch (Exception e) {
          InvokeCompletionEvent(e);
        }
      };
    }
  }

  public class FileByteLoader : LoaderBase<byte[]> {
    private readonly string filePath;
    public FileByteLoader(string filePath) {
      this.filePath = filePath;
      LoadAsync();
    }

    private async void LoadAsync() {
      using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

      if (stream.Length > int.MaxValue)
        throw new Exception("File too long");

      byte[] rawBytes = new byte[stream.Length];

      {
        int remaining = (int)stream.Length;
        int offset = 0;

        while (remaining != 0) {
          var bytesRead = await stream.ReadAsync(rawBytes, offset, remaining);

          if (bytesRead == 0) {
            goto result;
          }

          offset += bytesRead;
          remaining -= bytesRead;
        }
      }

    result:
      InvokeCompletionEvent(rawBytes);
    }
  }

  public class FileLoader<T> : LoaderBase<T> {
    public FileLoader(string filePath) {
      using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
      using var binaryReader = new BinaryReader(stream);
      InvokeCompletionEvent(binaryReader.Read<T>());
    }
  }
}
