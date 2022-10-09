using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTest.Infrastructure
{
    internal sealed class ObjectFactory
    {
        private ObjectFactory()
        {
        }

        private static readonly Lazy<ObjectFactory> lazy = new Lazy<ObjectFactory>(() => new ObjectFactory());

        public static ObjectFactory Instance { get { return lazy.Value; } }

        public TextWriter MakeSynchronizedTextWriter(string path)
        {
            return TextWriterManager.GetSynchronizedTextWriter(path);
        }

        private static class TextWriterManager
        {
            private static readonly object lockObject = new object();
            private static Dictionary<string, TextWriter> textWriters = new Dictionary<string, TextWriter>();
            public static TextWriter GetSynchronizedTextWriter(string path)
            {
                try
                {
                    lock (lockObject)
                    {
                        if (textWriters.ContainsKey(path))
                        {
                            return textWriters[path];
                        }
                        else
                        {
                            StreamWriter streamWriter = new StreamWriter(path)
                            {
                                AutoFlush = true
                            };
                            textWriters.Add(path, streamWriter);
                            return TextWriter.Synchronized(streamWriter);
                        }
                    }
                }
                catch (IOException exc)
                {
                    throw new IOException(exc.Message);
                }
            }
        }
    }



}
