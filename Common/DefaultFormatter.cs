using System;
using System.IO;
using ZeroFormatter;

namespace ZeroPipeline
{
    public class DefaultFormatter : IFormatter
    {
        private bool _disposedValue;

        public T Deserialize<T>(byte[] data)
        {
            return ZeroFormatterSerializer.Deserialize<T>(data);
        }

        public object Deserialize(Type type, byte[] data)
        {
            return ZeroFormatterSerializer.NonGeneric.Deserialize(type, data);
        }

        public T Deserialize<T>(Stream stream)
        {
            return ZeroFormatterSerializer.Deserialize<T>(stream);
        }

        public byte[] Serialize<T>(T value)
        {
            return ZeroFormatterSerializer.Serialize(value);
        }

        public byte[] Serialize(Type type, object value)
        {
            return ZeroFormatterSerializer.NonGeneric.Serialize(type, value);
        }

        public void Serialize<T>(Stream stream, T value)
        {
            ZeroFormatterSerializer.Serialize(stream, value);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DefaultFormatter()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
