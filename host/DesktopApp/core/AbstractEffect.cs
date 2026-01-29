using System.Diagnostics;

namespace Leds.core
{
    public abstract class AbstractEffect(LedLine attachedLedLine)
    {
        private Thread? _thread;
        private bool _running;

        protected LedLine LedLine { get; private set; } = attachedLedLine;

        public virtual void StartLooping()
        {
            if (_running) throw new Exception("Looping already started");
            _running = true;
            _thread = new Thread(() =>
            {
                if (StabilizeFps() < 0) SimpleLoop();
                else FpsStableLoop(StabilizeFps());
            });

            _thread.Start();
        }

        private void SimpleLoop()
        {
            while (_running)
            {
                InternalMoveNext();
            }
        }

        private void FpsStableLoop(int fps)
        {
            double frameTimeMs = 1000.0 / fps;
            Stopwatch sw = Stopwatch.StartNew();
            
            double nextFrameTime = sw.Elapsed.TotalMilliseconds;

            while (_running)
            {
                InternalMoveNext();
                
                nextFrameTime += frameTimeMs;
                
                while (_running && sw.Elapsed.TotalMilliseconds < nextFrameTime)
                {
                    double remaining = nextFrameTime - sw.Elapsed.TotalMilliseconds;
                    
                    if (remaining > 2.0)
                    {
                        Thread.Sleep(1); 
                    }
                    else
                    {
                        Thread.SpinWait(10); 
                    }
                }
                
                if (sw.Elapsed.TotalMilliseconds > nextFrameTime + frameTimeMs)
                {
                    nextFrameTime = sw.Elapsed.TotalMilliseconds;
                }
            }
        }

        public void StopLooping()
        {
            _running = false;
            var t = _thread;
            if (t != null && t.Join(3000))
            {
                Console.WriteLine("Effect thread didn't stop in time, interrupting...");
                t.Interrupt();
            }
            
            _thread = null;
            
            LedLine.Clear();
            ClearResources();
        }

        private int _framesCount = 0;
        private long _lastFpsPrintTime = 0;
        private void InternalMoveNext()
        {
            var currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (currentTime - _lastFpsPrintTime > 10000)
            {
                Console.WriteLine($"Effect FPS: {_framesCount / 10.0}");
                _framesCount = 0;
                _lastFpsPrintTime = currentTime;
            }
            else
            {
                _framesCount++;
            }
            
            MoveNext();
        }

        protected virtual int StabilizeFps() => -1;
        protected virtual void ClearResources() { }
        protected abstract void MoveNext();
    }
}
