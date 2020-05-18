using System;
using System.Diagnostics;
using System.IO.Pipes;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    /// <summary>
    /// Server-side class representing a channel process.
    /// </summary>
    public class ChannelNodeProcess : IDisposable
    {
        public const int MillisecondsBeforeForcedKill = 20000;

        public string PipeName { get; private set; }
        public NamedPipeServerStream Pipe { get; private set; }
        public Process Process { get; private set; }

        public ChannelNodeProcess()
        {
            // The child will use the validity of this pipe to decide whether to terminate.
            this.PipeName = (Guid.NewGuid()).ToString().Replace("-", "");
            this.Pipe = new NamedPipeServerStream(this.PipeName, PipeDirection.Out);

            this.Process = new Process();
        }

        public void Dispose()
        {
            // This will break the pipe so that the child node will be prompted to shut itself down.
            this.Pipe.Dispose();

            if (!this.Process.WaitForExit(MillisecondsBeforeForcedKill))
                this.Process.Kill();
        }
    }
}
